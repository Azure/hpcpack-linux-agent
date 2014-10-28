$script:HpcIaaSRegistryPath = "HKLM:\Software\Microsoft\HPC\IaaSInfo"

$script:AzureSizeInfo = @{"Small"=@{"Core"=1;"Socket"=1;"Memory"=1750}; `
                          "Medium"=@{"Core"=2;"Socket"=1;"Memory"=3500}; `
                          "Large"=@{"Core"=4;"Socket"=1;"Memory"=7000}; `
                          "ExtraLarge"=@{"Core"=8;"Socket"=1;"Memory"=14000};`
                          "A5"=@{"Core"=2;"Socket"=1;"Memory"=1};`
                          "A6"=@{"Core"=4;"Socket"=1;"Memory"=0};`
                          "A7"=@{"Core"=8;"Socket"=1;"Memory"=0};`
                          "A8"=@{"Core"=8;"Socket"=1;"Memory"=0};`
                          "A9"=@{"Core"=16;"Socket"=1;"Memory"=0}`
                         }

function TraceVerbose($log)
{
    Write-Host $log
    TraceToLogFile $log
}

function TraceInfo($log)
{
    Write-Verbose $log -verbose
    TraceToLogFile $log
}

function TraceWarning($log)
{
    Write-Warning $log
    TraceToLogFile $log
}

function TraceError($log)
{
    Write-Error $log
    TraceToLogFile $log
}

function TraceToLogFile($log)
{
    if ($script:LogFile -ne $null)
    {
        "$(Get-Date -format 'MM/dd/yyyy HH:mm:ss') $log" | Out-File -Confirm:$false -FilePath $script:LogFile -Append
    }    
}

function LoadAzureAndHpcModules($enableLog = $true)
{
    # The script has been tested on Powershell 3.0
    Set-StrictMode -Version 3

    # Following modifies the Write-Verbose behavior to turn the messages on globally for this session
    $VerbosePreference = "SilentlyContinue"

    if ($enableLog)
    {
        $StartTime = (Get-Date).ToUniversalTime()
        $datetimestr = $StartTime.ToString("yyyyMMddhhmmss")
        $script:LogFile = "$env:temp\HPCNodeLog-$datetimestr.txt"
        Write-Verbose "Log will be written to $LogFile" -Verbose        
    }
    else
    {
        $script:LogFile = $null
    }

    TraceVerbose "Initializing HPC and Azure environment"

    # Check if Windows Azure Powershell is avaiable
    $azureModule = Get-Module -ListAvailable -Name Azure 
    if ($azureModule -eq $null)
    {
        throw "Azure Powershell not found. Install the latest version from http://www.windowsazure.com/en-us/downloads/#cmd-line-tools"
    }

    if ($azureModule.Version -lt "0.8.2")
    {
        $ver = $azureModule.Version
        throw "The Azure Powershell version $ver is too old. Install the latest version from http://www.windowsazure.com/en-us/downloads/#cmd-line-tools"
    }

    Import-Module -Name Azure
    Add-PSSnapIn Microsoft.HPC    
}

function SelectAzureSubscription($subscriptionId)
{
    $subs = Get-AzureSubscription | where-object {$_.SubscriptionId -eq $subscriptionId}
    if ($subs -ne $null)
    {
        Select-AzureSubscription -SubscriptionName $subs.SubscriptionName | Out-Null
        return $subs.SubscriptionName 
    }
    else
    {
        throw "Cannot find profile for Azure subscription $subscriptionId. Ensure that the subscription ID is correct and first import the Azure profile."
    }
}

<#
.Synopsis
   check whether the VNet is existed
   check whether the VNet is regional VNet
   check whether the VNet is in $location
   check whether the VNet contains $subnet   
#>
function ValidateAzureVNet($vNet, $subNet, $location)
{
    $uniqueID = [Guid]::NewGuid().ToString()
    $vnetCfgFile = "$env:temp\VNetCfg_$uniqueID.xml"

    InvokeAzureCmdWithRetry -Command "Get-AzureVNetConfig -ExportToFile $vnetCfgFile" | Out-Null
    if (Test-Path -Path $vnetCfgFile)
    {
        Try{
            $xmlDoc = [xml](Get-Content $vnetCfgFile)
            Remove-Item $vnetCfgFile -Force -Confirm:$false

            $vnetSite = $xmlDoc.GetElementsByTagName("VirtualNetworkSite") | Where-Object {$_.name -eq $vNet}

            if ($vnetSite -eq $null)
            {
                throw "Virtual network $vNet doesn't exist. Ensure that the virtual network is configured properly."
            }

            if ($vnetSite.HasAttribute("Location"))
            {
                if ($vnetSite.Location -ne $location)
                {
                    throw "Virtual network $vNet isn't in region $location. Ensure that the virtual network and region are configured properly."
                }

                if ( ($vnetSite.Subnets -ne $null) -and ($vnetSite.Subnets.Subnet -ne $null))
                {
                    foreach($sub in $vnetSite.Subnets.Subnet)
                    {
                        if($sub.name -eq $subNet)
                        {
                            return
                        }
                    }
                }

                throw "Subnet $subNet doesn't exist in virtual network $vNet. Ensure that the subnet is configured properly." 
            }
            else
            {
                throw "Virtual network $vNet is not a regional Virtual network. Ensure that the virtual network is configured properly." 
            }
        }
        Finally
        {
            Remove-Item $vnetCfgFile -Force -Confirm:$false -ErrorAction SilentlyContinue
        }
    }    
}

function ValidateCloudService($ServiceName, $VNetName, $Location, $AffinityGroupName, $DeploymentLabel, $Quantity)
{
    $service = InvokeAzureCmdWithRetry -Command "Get-AzureService -ServiceName $ServiceName -ErrorAction SilentlyContinue"
    if ($service -ne $null)
    {
        $serviceLocation = $service.Location
        if ($serviceLocation -eq $null)
        {
            $affinity = InvokeAzureCmdWithRetry -Command "Get-AzureAffinityGroup -Name $($service.AffinityGroup) -ErrorAction SilentlyContinue"
            $serviceLocation = $affinity.Location
        }

        if ($serviceLocation -ne $Location)
        {
            throw "The cloud service $ServiceName must be in region $location"
        }
        elseif ((-not [String]::IsNullOrEmpty($AffinityGroupName)) -and ($service.AffinityGroup -ne $AffinityGroupName))
        {
            TraceWarning "It is better to put the cloud service in the same AffinityGroup $AffinityGroupName as Headnode, it may get better performance!" 
        }

        $deployment = InvokeAzureCmdWithRetry -Command "Get-AzureDeployment -ServiceName $ServiceName -Slot Production -ErrorAction SilentlyContinue -WarningAction SilentlyContinue"
        if ($deployment -ne $null)
        {
            # Check VNet
            if ($deployment.VNetName -ne $VNetName)
            {
                throw ("The existing production deployment of cloud service $ServiceName must be in virtual network $VNetName")
            }
            
            if ((DecodeLabelIfBase64Encoded $deployment.Label) -ne $DeploymentLabel)
            {
                 throw "The cloud service $ServiceName is already in use by another production deployment."
            }

            if (($Quantity + $deployment.RoleInstanceList.Count) -gt 50)
            {
                throw "You cann't deploy $Quantity virtual machines to cloud service $ServiceName which already deployed with $($deployment.RoleInstanceList.Count) virutal machines. Cannot deploy more than 50 virtual machines under one cloud service."
            }

            return "Deployed"
        }

        return "Created"
    }
    else
    {
        return "NotCreated"
    }
}

function GetIaaSInfo()
{
    $source = @"
    public class HpcIaasInfo
    {
        public string SubscriptionId { get; set; }
        public string Location { get; set; }
        public string AffinityGroup { get; set; }
        public string VNet { get; set; }
        public string SubNet { get; set; }

        public HpcIaasInfo(string subscriptionId, string location, string affinityGroup, string vNet, string subNet)
        {
            this.SubscriptionId = subscriptionId;
            this.Location = location;
            this.AffinityGroup = affinityGroup;
            this.VNet = vNet;
            this.SubNet = subNet;
        }
    }
"@

    Add-Type -TypeDefinition $source

    if (Test-Path $script:HpcIaaSRegistryPath)
    {
        $item = Get-Item -LiteralPath $script:HpcIaaSRegistryPath | Get-ItemProperty
        if ([String]::IsNullOrEmpty($item.SubscriptionId))
        {
            throw "SubscriptionId does not exist in the Registry. You must first configure subscription related information."
        }

        if ([String]::IsNullOrEmpty($item.Location))
        {
            throw "Location does not exist in the Registry. You must first configure subscription related information."
        }

        if ([String]::IsNullOrEmpty($item.VNet))
        {
            throw "VNet does not exist in the Registry. You must first configure subscription related information."
        }

        if ([String]::IsNullOrEmpty($item.SubNet))
        {
            throw "SubNet does not exist in the Registry. You must first configure subscription related information."
        }

        return New-Object HpcIaasInfo -argumentList $item.SubscriptionId, $item.Location, $item.AffinityGroup, $item.VNet, $item.SubNet
    }
    else
    {
        throw "Cannot load subscription id related info from the Registry. You must first configure subscription related information."
    }
}

function ValidateVMNaming($VMName)
{
    $regex = "^[a-zA-Z][a-zA-Z0-9-]{1,13}[a-zA-Z0-9]$"
    if($VMName -cnotmatch $regex)
    {
        throw "The generated node naming pattern $VMName is invalid. It must contain between 3 and 15 characters."
    }
}

function ValidateNodeNameSeries($VMNamePattern)
{
    $matched = $false
    do 
    {
        $regex = "^[a-zA-Z][a-zA-Z0-9-]{0,13}%[0-9]{1,14}%$"
        if($VMNamePattern -cnotmatch $regex)
        {
            break
        }

        if(($VMNamePattern.Length -gt 17) -or ($VMNamePattern.Length -lt 5))
        {
            break
        }

        $matched = $true
    } while ($false)

    if(-not $matched)
    {
        throw "The node naming pattern $VMNamePattern is invalid. It should be <root_name>%<start_number>% and meet the following criterions:`n 1. It must contain between 5 and 17 characters including the percent signs;`n 2. The <root_name> can contain only letters, numbers, and hyphens, and it must start with a letter `n 3. The start_number can contain only numbers.Example: AzureVM%1000%."
    }
}

function GenerateHpcNodeName($nodeSeries, $quantity, $usingHpcNamingSeries, $existedAzureNodes)
{
    $Regex = [System.Text.RegularExpressions.Regex]
    $match = $Regex::Match($nodeSeries, "%[0-9]+%")
    if (($match -eq $null) -or
        (-not $match.Success) -or
        ($match.ToString() -eq $null))
    {
        throw "The node naming pattern format is not correct. Use a pattern similar to NodeName%1000%"
    }

    $baseNum = $match.ToString().Replace("%", "")
    $digits = $baseNum.Length;
    
    $nodeList = @()
    
    TraceToLogFile "Trying to get file lock to generate node names"
    $maxRetries = 10
    $retry = 0
    $num = $baseNum -as [int]

    while($true)
    {
        try
        {
            $lockFile = [System.io.File]::Open("$PSScriptRoot\nodeseries", 'OpenOrCreate', 'ReadWrite', 'None')        
            break
        }
        catch
        {
            if($retry -lt $maxRetries)
            {
                TraceToLogFile "Failed to get node naming series file lock. There may be other process generating node names at same time, retry after 5 seconds"
                Start-Sleep -Seconds 5
                $retry++
            }
            else
            {
                throw "Failed to get node naming series file lock to generate node names after $maxRetries retries"
            }
        }    
    }

    try
    {
        $sequence = @{}
        if ($usingHpcNamingSeries)
        {
            $step = (Get-HpcClusterProperty -NodeNamingSequenceCount).Value -as [int]
        }
        else
        {
            $reader = New-Object System.IO.StreamReader($lockFile)
            $content = $reader.ReadToEnd() -split '[\r\n]'
            foreach($s in $content)
            {
                $tmp = $s.split("`t")
                $sequence[$tmp[0]] = $tmp[1] -as [int]
            }

            $step = $sequence[$nodeSeries]
        }

        if ($step -gt $num)
        {
            $num = $step
        }

        while ($nodeList.count -lt $quantity)
        {
            $num ++
            if ($num -ge [int]::MaxValue)
            {
                $num = 0
            }

            $nodeName = $Regex::Replace($nodeSeries, "%[0-9]+%", $num.ToString().PadLeft($digits, '0'))
            <#
            if ((Get-HpcNode -name $nodeName 2>$null) -ne $null)
            {
                TraceToLogFile "Node with name $nodeName has beed already existed in HPC cluster, will continue to generate node name"
                continue
            }
            #>
            if (($existedAzureNodes -ne $null) -and ($existedAzureNodes.Contains($nodeName) -eq $true))
            {
                TraceToLogFile "Node with name $nodeName has beed already existed in Azure, will continue to generate node name"
                continue
            }

            ValidateVMNaming $nodeName
            $nodeList += $nodeName
        }

        if ($usingHpcNamingSeries)
        {
            TraceToLogFile "Update node sequence count to $num"
            Set-HpcClusterProperty -NodeNamingSequenceCount $num
        }
        else
        {
            $sequence[$nodeSeries] = $num
            $pos = $lockFile.Seek(0, 0)
            $writer = New-Object System.IO.StreamWriter($lockFile)
            $length = 0
            foreach($key in $sequence.keys)
            {
                $line = $key + "`t" + $sequence[$key]
                $length += $line.Length
                $writer.WriteLine($line)
            }

            $writer.Flush()
            $lockFile.SetLength($length)
        }
    }
    finally
    {
        if ($lockFile -ne $null)
        {
            $lockFile.Close()
        }
    }

    return $nodeList
}

function GetHpcDeploymentLabel($vNet)
{
    return "HpcDeploy-" + $vNet + "-" + $env:CCP_SCHEDULER
}

function FindAzureNodes($hpcNodes, $iaasInfo, $filterStatus)
{
    $servicesForBatchOp = @{}    
    $nodeMapping = @{}
    $filteredNodes = @()

    $services =  InvokeAzureCmdWithRetry -Command "Get-AzureService -ErrorAction SilentlyContinue"
    $services = $services  | Where-Object { ($_.Location -eq $null) -or ($_.Location -eq $iaasInfo.Location)}

    foreach ($service in $services.ServiceName)
    {
        $deployment = InvokeAzureCmdWithRetry -Command "Get-AzureDeployment -ServiceName $service -Slot Production -ErrorAction SilentlyContinue"
        if ($deployment -ne $null)
        {
            if ($deployment.VNetName -eq $iaasInfo.VNet)
            {
                $vmList = $deployment.RoleInstanceList
                $cnt = 0
                $validVmList = @()
                foreach ($vm in $vmList)
                {
                    $node = $hpcNodes | Where-Object {$_.NetBiosName -eq $vm.InstanceName}
                    if ($node -ne $null)
                    {
                        if ($nodeMapping.ContainsKey($node))
                        {
                            throw "More than one Azure node is named $($node.NetBiosName) in virtual network $($iaasInfo.VNet)"
                        }

                        $nodeMapping[$node] = @{"VM"=$vm; "ServiceName"=$service}
                        
                        if ($filterStatus -ne $null -and $filterStatus -eq $vm.InstanceStatus)
                        {
                            $filteredNodes += $vm.InstanceName
                        }
                        else
                        {
                            $cnt ++
                            $validVmList += $vm.InstanceName
                        }
                    }
                }

                if ($cnt -eq 0)
                {
                    continue
                }

                $wholeDeployment = $false
                if ($cnt -eq $vmList.Count)
                {
                    $wholeDeployment = $true
                }

                $servicesForBatchOp[$service] = @{"DeploymentName"=$deployment.DeploymentName; "VM"=$validVmList; "WholeDeployment"=$wholeDeployment}
            }
        }
    }

    return @{"MappingNodes"=$nodeMapping; "Services"=$servicesForBatchOp; "FilterdNodes"=$filteredNodes}
}

function GetAzureVMs($iaasInfo)
{
    $namelist = @()

    $services = InvokeAzureCmdWithRetry -Command "Get-AzureService -ErrorAction SilentlyContinue"
    $services = $services | Where-Object { ($_.Location -eq $null) -or ($_.Location -eq $iaasInfo.Location) }
    foreach ($service in $services.ServiceName)
    {
        $deployment = InvokeAzureCmdWithRetry -Command "Get-AzureDeployment -ServiceName $service -Slot Production -ErrorAction SilentlyContinue"
        if ($deployment -ne $null)
        {
            if ($deployment.VNetName -eq $iaasInfo.VNet)
            {
                $vmList = InvokeAzureCmdWithRetry -Command "Get-AzureVM -ServiceName $service -ErrorAction SilentlyContinue"
                foreach ($vm in $vmList)
                {
                    $namelist += $vm.Name
                }
            }
        }
    }    

    return $namelist
}

function ValidateCredentials($userDomain, $userName, $password)
{
    Add-Type -AssemblyName System.DirectoryServices.AccountManagement    
    # create instance for domian principle context for input user
    $ct = [System.DirectoryServices.AccountManagement.ContextType]::Domain
    try
    {
        $pc = New-Object System.DirectoryServices.AccountManagement.PrincipalContext($ct,$userDomain)
    }
    catch
    {
        throw "Error occurred trying to connect to the domain controler. Ensure that the domain user name is correct. Exception: $_"
    }

    # validate user credential for user with password against domain
    if ($pc.ValidateCredentials($userName,$password) -eq $false)
    {
        throw "Invalid domain user name and password"
    }
    else
    {
        # return domain Fqdn
        $sepidx = $pc.ConnectedServer.IndexOf('.')
        return $pc.ConnectedServer.SubString($sepidx+1, ($pc.ConnectedServer.Length-$sepidx)-1)
    }
}

function CreateDomainJoinedVMWithCustomScript
{
    param
    (
        [Parameter(Mandatory=$true)]
        [String] $VMName, 

        [Parameter(Mandatory=$true)]
        [String] $ServiceName, 

        [Parameter(Mandatory=$true)]
        [String] $VMSize, 

        [Parameter(Mandatory=$true)]
        [String] $ImageName, 
        
        [Parameter(Mandatory=$true)]
        [String] $VNetName, 
        
        [Parameter(Mandatory=$true)]
        [String] $SubnetName, 

        [Parameter(Mandatory=$true)]
        [PSCredential] $LocalAdminCred, 
        
        [Parameter(Mandatory=$true)]
        [PSCredential] $DomainUserCred,

        [Parameter(Mandatory=$true)]
        [String] $DomainFQDN, 

        [Parameter(Mandatory=$true)]
        [String] $DeploymentLabel, 

        [Parameter(Mandatory=$true)]
        [String] $ClusterName, 

        [Parameter(Mandatory=$false)]
        [ValidateSet("ReadOnly","ReadWrite")]
        [String] $HostCaching = "",

        [Parameter(Mandatory=$false)]
        [Switch] $Wait
    )

    TraceInfo "Creating a domain joined VM $VMName in Service $ServiceName `n Size:  `t$VMSize`n Image: `t$ImageName`n VNet:  `t$VNetName`n Subnet:`t$SubnetName`n Domain:`t$DomainFQDN"

    $netbios = $DomainFQDN.Split(".")[0].ToUpper()
    $domainUserName = $DomainUserCred.UserName
    if($DomainUserCred.UserName.Contains("\"))
    {
        $domainUserName = $domainUserName.Split("\")[1]
    }

    try
    {
        if([String]::IsNullOrEmpty($HostCaching))
        {
            $vm = New-AzureVMConfig -Name $VMName -InstanceSize $VMSize -ImageName $ImageName
        }
        else
        {
            $vm = New-AzureVMConfig -Name $VMName -InstanceSize $VMSize -HostCaching $HostCaching -ImageName $ImageName
        }    

        $vm = $vm | `
              Add-AzureProvisioningConfig -AdminUsername $LocalAdminCred.UserName -Password $LocalAdminCred.GetNetworkCredential().Password `
                  -Domain $netbios -DomainUserName $domainUserName -DomainPassword $DomainUserCred.GetNetworkCredential().Password `
                  -JoinDomain $DomainFQDN -WindowsDomain | `
              Set-AzureVMBGInfoExtension | `
              Set-AzureSubnet -SubnetNames $SubnetName |`
              Set-AzureVMCustomScriptExtension -FileUri http://yongjun.blob.core.windows.net/scripts/UpdateHPCClusterName.ps1 -Run UpdateHPCClusterName.ps1 -Argument "-ClusterName $ClusterName"

        if(($VMSize -eq "A8") -or ($VMSize -eq "A9"))
        {
            $vm = $vm | Set-AzureVMExtension -ExtensionName "HpcVmDrivers" -Publisher "Microsoft.HpcCompute" -Version "1.*"
        }

        New-AzureVMWithRetry -VM $vm -ServiceName $ServiceName -VNetName $VNetName -DeploymentLabel $DeploymentLabel
    }
    catch
    {
        TraceError "Failed to create a VM $VMName joined to domain $DomainFQDN in cloud service $ServiceName"
        throw
    }

    if ($Wait.IsPresent)
    {
        WaitForVMReady -VMName $VMName -ServiceName $ServiceName
    }
}

function AddHpcLinuxGroup()
{
    $retry = 0
    $maxRetry = 120
    while($true)
    {
        try
        {
            $group = Get-HpcGroup -Name LinuxNodes -ErrorAction SilentlyContinue
            if ($group -eq $null)
            {
                New-HpcGroup -Name LinuxNodes -ErrorAction SilentlyContinue
            }

            break
        }
        catch
        {
            if ($retry -lt $maxRetry)
            {
                Start-Sleep -Seconds 5
                $retry ++
            }
            else
            {
                throw "Failed to add unmanaged node $nodeName to the HPC cluster"
            }
        }
    }
}

function AddUnManagedNodeWithRetry($nodeName, $core, $socket, $memory)
{
    $retry = 0
    $maxRetry = 120
    while($true)
    {
        try
        {
            $node = Get-HpcNode -Name "test" -ErrorAction SilentlyContinue
            if ($node -eq $null)
            {
                Add-HpcUnManagedNode -Name $nodeName -SubscribedCores $core -SubscribedSockets $socket -Memory $memory -GroupName "LinuxNodes"
            }

            break
        }
        catch
        {
            if ($retry -lt $maxRetry)
            {
                Start-Sleep -Seconds 5
                $retry ++
            }
            else
            {
                throw "Failed to add unmanaged node $nodeName to the HPC cluster"
            }
        }
    }
}

$NodeManageInitScript = 
{
    param 
    (
        [Parameter(Mandatory=$true)]
        [String[]] $Name,

        [Parameter(Mandatory=$false)]
        [String] $FilterStatus = $null
    )

    Add-Type -Path "$env:CCP_HOME/bin/ccppsh.dll"  

    # Get and validate pre-config information: subscription id
    $script:iaasInfo = GetIaaSInfo
    TraceVerbose "Selecting Azure subscription $($script:iaasInfo.SubscriptionId)"
    $script:subscriptionName = SelectAzureSubscription $script:iaasInfo.SubscriptionId

    # Get hpc nodes filtered by $Name, we don't remove headnode
    TraceVerbose "Retrieving HPC node information"
    $script:hpcNodes = Get-HpcNode -Name $Name -ErrorAction SilentlyContinue | Where-Object {$_.IsHeadNode -eq $false}
    if (($script:hpcNodes -eq $null) -or ($script:hpcNodes.Count -eq 0))
    {
        throw "Cannot find HPC node with name matching $($Name -join ',')"
    }

    # go through each depoyment under all cloud service, mapping to azure nodes (using deployment label)
    TraceVerbose "Scanning Azure virtual machine mapping to HPC nodes ......"
    $script:mapping = FindAzureNodes $script:hpcNodes $script:iaasInfo $FilterStatus
    $script:nodeMapping = $mapping["MappingNodes"]
    $script:servicesForBatchOp = $mapping["Services"]
    $script:filteredNodes = $mapping["FilterdNodes"]
}
