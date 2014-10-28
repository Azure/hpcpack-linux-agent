<#
.Synopsis
   Add IaaS nodes to Microsoft HPC Cluster in Windows Azure as IaaS.
.DESCRIPTION
   This script add new IaaS nodes to Microsoft HPC Cluster in Windows Azure as IaaS. 
.EXAMPLE
   Add-HPCIaasNode.ps1 -ServiceName "cloud service name" -ImageName "vm image name" -Quantity "1" -InstanceSize "Large"  -DomainUserName "user" -DomainUserPassword "password"
#>
Param
(
    [Parameter(Mandatory=$true)]
    [String] $ServiceName,

    [Parameter(Mandatory=$true)]
    [String] $ImageName,

    [Parameter(Mandatory=$true)]
    [Int] $Quantity,

    [Parameter(Mandatory=$true)]
    [String] $InstanceSize,

    [Parameter(Mandatory=$true)]
    [String] $DomainUserName,

    [Parameter(Mandatory=$false)]
    [String] $DomainUserPassword = "",

    [Parameter(Mandatory=$false)]
    [String] $NodeNameSeries = ""
)

. "$PSScriptRoot\IaaSAzureCmdLib.ps1"
. "$PSScriptRoot\HPCIaaSNodeMgmtUtil.ps1"

if ($Quantity -le 0)
{
    Write-Error "Quantity must be larger than 0"
    return
}
elseif ($Quantity -gt 50)
{
    Write-Error "You cann't deploy $Quantity virtual machines to one cloud service. Cannot deploy more than 50 virtual machines under one cloud service."
    return
}

LoadAzureAndHpcModules
try
{
    # Get and validate pre-config information: subscription id, location, vnet, subnet, affinity group
    # get from registry key
    $iaasInfo = GetIaaSInfo
    
    TraceInfo "PROGRESS:5"
    # check whether the subscription is existed by Get-AzureSubscription, if yes, Select-AzureSubscription
    TraceInfo "Selecting Azure subscription $($iaasInfo.SubscriptionId)"
    $subscriptionName = SelectAzureSubscription $iaasInfo.SubscriptionId

    # check vnet, subnet is existed, vnet is in location
    TraceInfo "Validating virtual network $($iaasInfo.VNet) and subnet $($iaasInfo.SubNet)"
    ValidateAzureVNet $iaasInfo.VNet $iaasInfo.SubNet $iaasInfo.Location

    # validate service, image is correct
    $deloymentLabel = GetHpcDeploymentLabel $iaasInfo.VNet
    TraceInfo "Validating cloud service $ServiceName"
    $serviceState = ValidateCloudService $ServiceName $iaasInfo.VNet $iaasInfo.Location $iaasInfo.AffinityGroup $deloymentLabel $Quantity
    if ($serviceState -eq "NotCreated")
    {
        throw "Cloud service $ServiceName doesn't exist in current subscription $subscriptionName. You must create it first."
    }

    TraceInfo "Validating Azure virtual machine image $ImageName"
    $image = InvokeAzureCmdWithRetry -Command "Get-AzureVMImage" | where-object {$_.ImageName -eq $ImageName}

    $isLinux = $false
    if ($image -ne $null)
    {
        if ($image.Location.Split(";").Contains($iaasInfo.Location) -eq $false)
        {
            throw "Azure image $ImageName must be in region $($iaasInfo.Location)"
        }

        if ($image.OS -eq "Linux")
        {
            $isLinux = $true
        }        

        if ($image.Category -ne "User")
        {
            throw "The Azure image must be user customized and the HPC Pack compute node role or broker node role must already be installed."
        }

        # set current Azure storage account
        $mediaLink = $image.MediaLink
        if (($mediaLink -eq $null) -and ($image.OSDiskConfiguration -ne $null))
        {
            $mediaLink = $image.OSDiskConfiguration.MediaLink
        }

        if ($mediaLink -eq $null)
        {
            throw "Invalid virtual machine image. Cannot find the MediaLink for image $ImageName."
        }

        $storageAccount = GetStorageAccountFromMediaLink $mediaLink
        Set-AzureSubscription -SubscriptionName $subscriptionName -CurrentStorageAccountName $storageAccount
    }
    else
    {
        throw "Cannot find Azure virtual machine image $ImageName. Check the image name."
    }

    # validate user name and password
    TraceInfo "Validating domain user name and password"
    if ([String]::IsNullOrEmpty($DomainUserPassword))
    {
        $secPsw = Read-Host -Prompt "Please input the password for the domain user $DomainUserName" -AsSecureString
        $DomainUserPassword = ConvertSecureStrToPlain -SecurePassword $secPsw
    }

    $sepidx = $DomainUserName.IndexOf('\')
    $userDomain = $null
    if ($sepidx -gt 1)
    {
        $userDomain = $DomainUserName.Substring(0, $sepidx)
    }

    if ([String]::IsNullOrEmpty($userDomain))
    {
        # using domain of head node
        $userDomain = (Get-WmiObject Win32_ComputerSystem).Domain
    }

    $userName = $DomainUserName.Substring($sepidx+1, ($DomainUserName.Length - $sepidx)-1)
    $userDomain = ValidateCredentials $userDomain $userName $DomainUserPassword

    TraceInfo "PROGRESS:10"
    # new Azure VMs
    # get node name series
    $usingHpcNamingSeries = $false
    if([String]::IsNullOrEmpty($NodeNameSeries))
    {
        $NodeNameSeries = "AzureVMCN-%0000%"
        if([String]::IsNullOrEmpty($NodeNameSeries))
        {
            throw "Specify the node naming series or configure the naming of new nodes in the HPC cluster."
        }        
    }

    TraceInfo "Validating node naming series ......"
    ValidateNodeNameSeries $NodeNameSeries    
    TraceInfo "Generating node names ......"
    $existedAzureNodes = GetAzureVMs $iaasInfo

    $retry=0
    while($true)
    {
       try
       {
         $nodeList = GenerateHpcNodeName $NodeNameSeries $Quantity $usingHpcNamingSeries $existedAzureNodes
         break;
       }
       catch
       {
            if($retry -lt 100)
            {
                $retry++
                Start-Sleep -Seconds 6
            }
            else
            {
                thrown
            }
       }
    }

    $localAdminCred = New-Object -TypeName System.Management.Automation.PSCredential `
        -ArgumentList @($userName, (ConvertTo-SecureString -String $DomainUserPassword -AsPlainText -Force))

    $domainUserCred = New-Object -TypeName System.Management.Automation.PSCredential `
        -ArgumentList @("$userDomain\$userName", (ConvertTo-SecureString -String $DomainUserPassword -AsPlainText -Force))
    TraceInfo "PROGRESS:15"
    Write-Host
    TraceInfo "Starting to create $Quantity Azure virtual machines one by one"
    $step = 0
    $clusterName = $env:COMPUTERNAME

    $AzureSizeInfo = @{"Small"=@{"Core"=1;"Socket"=1;"Memory"=1750}; `
                          "Medium"=@{"Core"=2;"Socket"=1;"Memory"=3500}; `
                          "Large"=@{"Core"=4;"Socket"=1;"Memory"=7000}; `
                          "ExtraLarge"=@{"Core"=8;"Socket"=1;"Memory"=14000};`
                          "A5"=@{"Core"=2;"Socket"=1;"Memory"=14000};`
                          "A6"=@{"Core"=4;"Socket"=1;"Memory"=28000};`
                          "A7"=@{"Core"=8;"Socket"=1;"Memory"=56000};`
                          "A8"=@{"Core"=8;"Socket"=1;"Memory"=56000};`
                          "A9"=@{"Core"=16;"Socket"=1;"Memory"=112000}`
                         }
    
    $linuxNodes = @{}
    
    $stepPercent = [math]::Floor(75/$Quantity)
    foreach($node in $nodeList)
    {
        $step ++
        TraceInfo "($step / $Quantity) Creating Azure virtual machine $node ......"
        if ($isLinux)
        {
            if ($AzureSizeInfo.Keys -notcontains $InstanceSize)
            {
                throw "$InstanceSize is not supported!"
            }

            $vm = New-AzureVMConfig -Name $node -InstanceSize $InstanceSize -ImageName $ImageName
            $vm = $vm | `
              Add-AzureProvisioningConfig -Linux -LinuxUser $localAdminCred.UserName -Password $localAdminCred.GetNetworkCredential().Password |`
              Set-AzureSubnet -SubnetNames $iaasInfo.SubNet

            if(($InstanceSize -eq "A8") -or ($InstanceSize -eq "A9"))
            {
                $vm = $vm | Set-AzureVMExtension -ExtensionName "HpcVmDrivers" -Publisher "Microsoft.HpcCompute" -Version "1.*"
            }

            New-AzureVMWithRetry -VM $vm -ServiceName $ServiceName -VNetName $iaasInfo.VNet -DeploymentLabel $deloymentLabel

            $linuxNodes[$node] = $InstanceSize            
        }
        else
        {
            CreateDomainJoinedVMWithCustomScript -VMName $node `
                                 -ServiceName $ServiceName `
                                 -VMSize $InstanceSize `
                                 -ImageName $ImageName `
                                 -VNetName $iaasInfo.VNet `
                                 -SubnetName $iaasInfo.SubNet `
                                 -LocalAdminCred $localAdminCred `
                                 -DomainUserCred $domainUserCred `
                                 -DomainFQDN $userDomain `
                                 -DeploymentLabel $deloymentLabel `
                                 -ClusterName $clusterName *>$null
        }

        $percent = $stepPercent*$step + 15
        TraceInfo "PROGRESS:$percent"
    }

    if ($isLinux)
    {
        Set-ItemProperty -Path HKLM:\SOFTWARE\Microsoft\HPC\IaaSInfo -Name NodeType -Value Linux
        TraceInfo "Checking HPC nodes group for linux nodes"
        AddHpcLinuxGroup *>$null
    }

    if ($linuxNodes.Count -gt 0)
    {
        TraceInfo "PROGRESS:95"
        $nodeStatus = @{}
        foreach ($node in $linuxNodes.Keys)
        {
            # add unmanagement node to HPC
            $size = $linuxNodes[$node]
            $info = $AzureSizeInfo[$size]
            AddUnManagedNodeWithRetry $node $info.Core $info.Socket $info.Memory *>$null
            $nodeStatus[$node] = $false
        }

        TraceInfo "Waiting for linux nodes ready ......"

        # add ip to host file        
        while ($true)
        {
            $ipToAdd = @{}
            $deployment = InvokeAzureCmdWithRetry -Command "Get-AzureDeployment -ServiceName $ServiceName -ErrorAction SilentlyContinue"
            foreach ($role in $deployment.RoleInstanceList)
            {
                if ($linuxNodes.Keys -ccontains $role.HostName)
                {
                    $ipToAdd[$role.IPAddress] = $role.HostName
                }
            }

            if ($ipToAdd.Keys.Count -lt $linuxNodes.Keys.Count)
            {
                Start-Sleep -Seconds 5
            }
            else
            {
                break
            }
        }

        $existingHostEntry = Get-HostNames.ps1
        foreach ($entry in $existingHostEntry)
        {
            if ($ipToAdd.Keys -ccontains $entry.IpAddress)
            {
                Remove-HostNames.ps1 $entry.Hostname
            }
        }

        foreach ($ip in $ipToAdd.Keys)
        {
            Add-HostNames.ps1 $ip $ipToAdd[$ip]
        }

        while ($true)
        {
            $snapshotNodeStatus = @{}
            foreach ($node in $nodeStatus.Keys)
            {
                $snapshotNodeStatus[$node] = $nodeStatus[$node]
            }

            $falseNodes = 0
            foreach ($node in $snapshotNodeStatus.Keys)
            {
                if ($snapshotNodeStatus[$node] -eq $false)
                {
                    $pingInfo = C:\ConfigureLinux\TestStream.exe $node
                    if (-not $?)
                    {
                        $falseNodes++ 
                        TraceWarning "Linux node $node is still not ready!"
                    }
                    else
                    {
                        $info = $pingInfo -join ","
                        if ($info -like "*Exception*")
                        {
                            TraceWarning "Linux node $node is still not ready!"
                            $falseNodes++
                        }
                        else
                        {
                            TraceInfo "Linux node $node is ready"
                            $nodeStatus[$node] = $true
                        }
                    }
                }
            }

            if($falseNodes -gt 0)
            {
                Start-Sleep -Seconds 5
            }
            else
            {
                break
            }
        }
    }
}
catch
{
    TraceToLogFile $_
    throw
}

Write-Host
TraceInfo "All Azure virtual machines have been created. It may take several minutes to start the virtual machines; please wait. The nodes will be automatically added to the HPC cluster."