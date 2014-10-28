# ----------------------------------------------------------------------------- 
# Script :  IaaSAzureCmdLib.ps1
# Author :  Microsoft HPC Pack team
# Version:  4.3.0
# Summary:  The wrapper library for Azure Cmdlets
# ----------------------------------------------------------------------------- 

<#
.SYNOPSIS
  Invoke an Azure cmdlet with retries
.DESCRIPTION
  Invoke an Azure cmdlet with retries. The Azure cmdlet must be a "query" command (typically, Get-Azurexxx or Test-Azurexxx), must not modify any Azure resources. 
.EXAMPLE
  InvokeAzureCmdWithRetry -Command "Get-AzureVMImage"
  InvokeAzureCmdWithRetry -Command "Get-AzureDeployment -ServiceName testservice" -RetryTimes 10
#>
function InvokeAzureCmdWithRetry
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [String] $Command,

        [Parameter(Mandatory=$false)]
        [Int] $RetryTimes = 20
    )

    # if ErrorAction is not specified, specify it as Stop so that the error can be thrown by "Invoke-Expression" command.
    if(($Command -notmatch " -ErrorAction ") -and ($Command -notmatch " -EA "))
    {
        $newCommand = $Command + " -ErrorAction Stop"
    }
    else
    {
        $newCommand = $Command
    }
    $retry = 0
    while($true)
    {
        try
        {
            $ret = Invoke-Expression -Command $newCommand
            return $ret
        }
        catch
        {
            $caughtException = $_.Exception
            $errMsg = "Failed to execute the command '$Command': " + ($_ | Out-String)
            $shouldThrow = $true
            if($caughtException -is [Microsoft.WindowsAzure.CloudException])
            {
                if($_.Exception.ErrorCode -eq "ResourceNotFound")
                {
                    TraceInfo "The command '$Command' executed: ResourceNotFound"
                    return
                }

                $shouldThrow = $false
            }
            elseif(($caughtException -is [System.Net.Http.HttpRequestException]) -or ($caughtException -is [System.Threading.Tasks.TaskCanceledException]))
            {
                $shouldThrow = $false
            }
        }

        if($shouldThrow -or ($retry -ge $RetryTimes))
        {
            TraceError $errMsg
            throw $caughtException
        }
        else
        {
            TraceWarning $errMsg
            TraceInfo "Retry to execute the command '$Command' after 10 seconds"
            Start-Sleep -Seconds 10
            $retry++
        }
    }
}

function New-AzureServiceWithRetry
{
    Param
    (
        # The Name
        [Parameter(Mandatory=$true)]
        [String] $ServiceName,

        [Parameter(Mandatory=$true)]
        [String] $AffinityGroupName,

        [Parameter(Mandatory=$false)]
        [Int] $RetryTimes = 20
    )

    $retry = 0
    while($true)
    {
        try
        {
            TraceInfo "Creating the cloud service $ServiceName in the affinity group $AffinityGroupName"
            New-AzureService -ServiceName $ServiceName -AffinityGroup $AffinityGroupName -ErrorAction Stop | Out-Null
            TraceInfo "The cloud service $ServiceName created in the affinity group $AffinityGroupName"
            return
        }
        catch
        {
            TraceWarning ("Failed to create cloud service $ServiceName :" + ($_ | Out-String))
        }
        
        $service = InvokeAzureCmdWithRetry -Command "Get-AzureService -ServiceName $ServiceName"
        if($service -ne $null)
        {
            TraceInfo "Cloud service $ServiceName Created"
            return
        }

        if($retry -lt $RetryTimes)
        {
            TraceInfo "Retry to create cloud service $ServiceName after 10 seconds"
            Start-Sleep -Seconds 10
            $retry++
        }
        else
        {
            throw "Failed to create cloud service $ServiceName  after $RetryTimes retries"
        }
  
    }
}

function New-AzureVMWithRetry
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [String] $ServiceName,

        [Parameter(Mandatory=$true)]
        [Microsoft.WindowsAzure.Commands.ServiceManagement.Model.PersistentVM] $VM,

        [Parameter(Mandatory=$true)]
        [String] $VNetName,

        [Parameter(Mandatory=$true)]
        [String] $DeploymentLabel,

        [Parameter(Mandatory=$false)]
        [Switch] $WaitForBoot,

        [Parameter(Mandatory=$false)]
        [Int] $RetryTimes = 20
    )

    $retry = 0
    $vmname = $VM.RoleName
    while($true)
    {
        try
        {
            TraceInfo "Creating the virtual machine $vmname in cloud service $ServiceName"
            if($WaitForBoot.IsPresent)
            {
                New-AzureVM -VMs $VM -ServiceName $ServiceName -DeploymentLabel $DeploymentLabel -VNetName $VNetName -WaitForBoot -ErrorAction Stop -WarningAction SilentlyContinue | Out-Null
            }
            else
            {
                New-AzureVM -VMs $VM -ServiceName $ServiceName -DeploymentLabel $DeploymentLabel -VNetName $VNetName -ErrorAction Stop -WarningAction SilentlyContinue | Out-Null
            }

            TraceInfo "The virtual machine $vmname Created in cloud service $ServiceName"           
            return
        }
        catch
        {
            TraceWarning ("Failed to create the virtual machine $vmname : " + ($_ | Out-String))
        }
        
        $newvm = InvokeAzureCmdWithRetry -Command "Get-AzureVM -ServiceName $ServiceName -Name $vmname"
        if($newvm -ne $null)
        {
            TraceInfo "The virtual machine $vmname Created in cloud service $ServiceName"
            return
        }

        if($retry -lt $RetryTimes)
        {
            TraceInfo "Retry to create the virtual machine $vmname after 10 seconds"
            Start-Sleep -Seconds 10
            $retry++
        }
        else
        {
            throw "Failed to create the virtual machine $vmname after $RetryTimes retries"
        }
    }
}

function Update-AzureVMWithRetry
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [String] $ServiceName,

        [Parameter(Mandatory=$true)]
        [String] $Name,

        [Parameter(Mandatory=$true)]
        [Microsoft.WindowsAzure.Commands.ServiceManagement.Model.PersistentVM] $VM,

        [Parameter(Mandatory=$false)]
        [Int] $RetryTimes = 20
    )

    $retry = 0
    while($true)
    {
        try
        {
            TraceInfo "Updating the virtual machine $Name in cloud service $ServiceName ..."
            Update-AzureVM -Name $Name -ServiceName $ServiceName -VM $VM -ErrorAction Stop -WarningAction SilentlyContinue | Out-Null
            TraceInfo "The virtual machine $Name updated."           
            return
        }
        catch
        {
            TraceWarning ("Failed to update the virtual machine $Name : " + ($_ | Out-String))
        }

        if($retry -lt $RetryTimes)
        {
            TraceInfo "Retry to update the virtual machine $Name after 10 seconds"
            Start-Sleep -Seconds 10
            $retry++
        }
        else
        {
            throw "Failed to update the virtual machine $Name after $RetryTimes retries"
        }
    }
}

function New-AzureAffinityGroupWithRetry
{
    Param
    (
        # The Name
        [Parameter(Mandatory=$true)]
        [String] $Name,

        [Parameter(Mandatory=$true)]
        [String] $Location,

        [Parameter(Mandatory=$false)]
        [Int] $RetryTimes = 20
    )

    $retry = 0
    while($true)
    {
        try
        {
            TraceInfo "Creating the affinity group $Name at region $Location"
            New-AzureAffinityGroup -Name $Name -Location $Location -Label $Name -ErrorAction Stop | Out-Null
            TraceInfo "The affinity group $Name created at region $Location"
            return
        }
        catch
        {
            TraceWarning ("Failed to create affinity group $Name : " + ($_ | Out-String))
        }
        
        $ag = InvokeAzureCmdWithRetry -Command "Get-AzureAffinityGroup -Name $Name"
        if($ag -ne $null)
        {
            TraceInfo "The affinity group $Name Created"
            return
        }

        if($retry -lt $RetryTimes)
        {
            TraceInfo "Retry to create the affinity group $Name after 10 seconds"
            Start-Sleep -Seconds 10
            $retry++
        }
        else
        {
            throw "Failed to create the affinity group $Name after $RetryTimes retries"
        }
    }
}

function New-AzureStorageAccountWithRetry
{
    Param
    (
        # The Name
        [Parameter(Mandatory=$true)]
        [String] $StorageAccountName,

        [Parameter(Mandatory=$true)]
        [String] $Location,

        [Parameter(Mandatory=$false)]
        [Int] $RetryTimes = 20
    )

    $retry = 0
    while($true)
    {
        try
        {
            TraceInfo "Creating the storage account $StorageAccountName at region $Location"
            New-AzureStorageAccount -StorageAccountName $StorageAccountName -Location $Location -ErrorAction Stop | Out-Null
            TraceInfo "Storage account $StorageAccountName created at region $Location"
            return
        }
        catch
        {
            TraceWarning ("Failed to create the storage account $StorageAccountName : " + ($_ | Out-String))
        }
        
        $storage = InvokeAzureCmdWithRetry -Command "Get-AzureStorageAccount -StorageAccountName $StorageAccountName"
        if($storage -ne $null)
        {
            TraceInfo "Storage account $StorageAccountName Created"
            return
        }

        if($retry -lt $RetryTimes)
        {
            TraceInfo "Retry to create the storage account $StorageAccountName after 10 seconds"
            Start-Sleep -Seconds 10
            $retry++  
        }
        else
        {
            throw "Failed to create the storage account $StorageAccountName  after $RetryTimes retries"
        }
    }
}

function Save-AzureVMImageWithRetry
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [String] $Name,

        [Parameter(Mandatory=$true)]
        [String] $ServiceName,

        [Parameter(Mandatory=$true)]
        [String] $ImageName,
        
        [Parameter(Mandatory=$false)]
        [String] $ImageLabel = "HPC Image",
        
        [Parameter(Mandatory=$false)]
        [Int] $RetryTimes = 20
    )

    $retry = 0
    while($true)
    {
        $service = $null
        try
        {
            TraceInfo "Saving Azure VM Image $ImageName"
            Save-AzureVMImage -Name $Name -ServiceName $ServiceName -ImageName $ImageName -ImageLabel $ImageLabel -OSState Generalized -ErrorAction Stop | Out-Null
            TraceInfo "The VM image $ImageName was saved"
            return
        }
        catch
        {
            TraceWarning ("Failed to save the VM image $ImageName :" + ($_ | Out-String))
        }

        $image = InvokeAzureCmdWithRetry -Command "Get-AzureVMImage -ImageName $ImageName"
        if($image -ne $null)
        {
            TraceInfo "The VM image $ImageName already saved"
            return
        }

        if($retry -lt $RetryTimes)
        {
            TraceInfo "Retry to save the VM image $ImageName after 10 seconds"
            Start-Sleep -Seconds 10
            $retry++
        }
        else
        {
            throw "Failed to save the VM image $ImageName after $RetryTimes retries"
        }
    }
}

function Get-AzureVNetSiteWithRetry
{
    Param
    (
        # The Name
        [Parameter(Mandatory=$true)]
        [String] $VNetName
    )
    
    $vnetSite = $null
    try
    {
        $vnetSite = InvokeAzureCmdWithRetry -Command "Get-AzureVNetSite -VNetName $VNetName"
    }
    catch
    {
    }

    return $vnetSite
}

   
function Remove-AzureVMWithRetry
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [String] $ServiceName,

        [Parameter(Mandatory=$true)]
        [String] $VMName,

        [Parameter(Mandatory=$false)]
        [Int] $RetryTimes = 3
    )

    $retry = 0
    TraceInfo "Trying to remove the virtual machine $VMName from the cloud service $ServiceName"
    while($true)
    {
        try
        {
            TraceInfo "Removing the virtual machine $VMName from the cloud service $ServiceName"
            Remove-AzureVM -Name $VMName -ServiceName $ServiceName -DeleteVHD -ErrorAction Stop -WarningAction SilentlyContinue
            TraceInfo "The virtual machine $VMName removed from the cloud service $ServiceName"
            return
        }
        catch
        {
            TraceWarning ("Failed to remove the virtual machine $VMName : " + ($_ | Out-String))
        }

        $vm = InvokeAzureCmdWithRetry -Command "Get-AzureVM -ServiceName $ServiceName -Name $VMName -WarningAction SilentlyContinue"
        if($vm -eq $null)
        {
            TraceInfo "The virtual machine $VMName already removed from the cloud service $ServiceName"
            return
        }
        
        if($retry -lt $RetryTimes)
        {
            TraceInfo "Retry to remove the virtual machine $VMName after 5 seconds"
            Start-Sleep -Seconds 5
            $retry++
        }
        else
        {
            throw "Failed to remove the virtual machine $VMName after $RetryTimes retries"
        }
    }
}

function Remove-AzureServiceWithRetry
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [String] $ServiceName,

        [Parameter(Mandatory=$false)]
        [Int] $RetryTimes = 3
    )

    $retry = 0
    TraceInfo "Trying to remove the cloud service $ServiceName"
    while($true)
    {
        try
        {
            TraceInfo "Removing the cloud service $ServiceName"
            Remove-AzureService -ServiceName $ServiceName -DeleteAll -Force -ErrorAction Stop -WarningAction SilentlyContinue
            TraceInfo "The cloud service $ServiceName removed"
            return
        }
        catch
        {
            TraceWarning ("Failed to remove the cloud service $ServiceName : " + ($_ | Out-String))
        }

        $svc = InvokeAzureCmdWithRetry -Command "Get-AzureService -ServiceName $ServiceName"
        if($svc -eq $null)
        {
            TraceInfo "The cloud service $ServiceName already removed"
            return
        }
                
        if($retry -lt $RetryTimes)
        {
            TraceInfo "Retry to remove the cloud service $ServiceName after 5 seconds"
            Start-Sleep -Seconds 5
            $retry++
        }
        else
        {
            throw "Failed to remove the cloud service $ServiceName after $RetryTimes retries"
        }
    }
}

function GetStorageAccountFromMediaLink
{
    param
    (
        [Parameter(Mandatory=$true)]
        [String] $MediaLink
    )

    if($MediaLink -ne $null)
    {
        $startIndex = $MediaLink.IndexOf("//") + 2
        $endIndex = $MediaLink.IndexOf(".")
        if(($endIndex -gt ($startIndex + 2)) -and ($startIndex -gt 0))
        {
            return $MediaLink.Substring($startIndex, $endIndex - $startIndex)
        }
    }

    return ""
}


function DecodeLabelIfBase64Encoded
{
    param($orgLabel)

    try
    {
        $bytes = [System.Convert]::FromBase64String($orgLabel)
        return [System.Text.Encoding]::UTF8.GetString($bytes)
    }
    catch
    {
        return $orgLabel
    }
}


<#
.SYNOPSIS
  Check whether a VM exists in the given cloud service
.DESCRIPTION
  Check whether a VM exists in the given cloud service
.EXAMPLE
  VMExists -Name "vmname" -ServiceName "servicename"
#>
function VMExists 
{
    param
    (
        #The name of VM
        [Parameter(Mandatory=$true)]
        [String] $Name, 

        #The name of the host cloud service
        [Parameter(Mandatory=$true)]
        [String] $ServiceName
    )

    $vm = InvokeAzureCmdWithRetry -Command "Get-AzureVM -Name $Name -ServiceName $ServiceName"
    if ($vm -ne $null) 
    {
        return $true
    }
    
    return $false
}

<#
.SYNOPSIS
  Wait the specified VMs in a cloud service to become the target status
.DESCRIPTION
  Wait the specified VMs to become the target status in a given maximum waiting time, throw if the timer expires
.EXAMPLE
  The following example will wait the VM "mytestvm" in cloud service "mytestservice" to become 'ReadyRole' for a maximum time 900 seconds
    WaitForVMReady -VMName @("testvm1", "testvm2") -ServiceName "mytestservice" -MaxWaitSeconds 900
#>
function WaitForVMReady 
{
    param 
    (
        [Parameter(Mandatory=$true)]
        [String[]] $VMName, 

        [Parameter(Mandatory=$true)]
        [String] $ServiceName, 

        [Parameter(Mandatory=$false)]
        [String] $TargetStatus = "ReadyRole", 

        [Parameter(Mandatory=$false)]
        [ValidateRange(0, 3600)]
        [int] $MaxWaitSeconds = 1800
    )

    TraceInfo "Waiting for the following VM(s) to become $TargetStatus status: $VMName"
    $noReadyVMNames = $VMName
    $beginTime = Get-Date
    while ($noReadyVMNames.Count -gt 0)
    {
        $vms = @(InvokeAzureCmdWithRetry -Command "Get-AzureVM -ServiceName $ServiceName -WarningAction SilentlyContinue" | ?{($noReadyVMNames -contains $_.InstanceName) -and ($_.InstanceStatus -eq $TargetStatus)})
        if($vms.Count -gt 0)
        {
            TraceInfo "The following VM(s) are now in $TargetStatus status: $($vms.InstanceName)"
            if($vms.Count -eq $noReadyVMNames.Count)
            {
                break
            }

            $noReadyVMNames = @($noReadyVMNames | ?{$vms.InstanceName -notcontains $_})
        }

        $timeSpan = (Get-Date) - $beginTime
        $elapsedSeconds = [int]$timeSpan.TotalSeconds
        if ($elapsedSeconds -ge $MaxWaitSeconds)
        {
            throw "Timed out waiting for the following virtual machine(s) to become $TargetStatus : $noReadyVMNames"
        }

        Start-Sleep -Seconds 10
    }
}

function CreateDomainJoinedVM
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

        [Parameter(Mandatory=$false)]
        [ValidateSet("ReadOnly","ReadWrite")]
        [String] $HostCaching = "",

        [Parameter(Mandatory=$false)]
        [String] $HnNameFile = "",

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

        if ([String]::IsNullOrEmpty($HnNameFile))
        {
            $vm = $vm | `
                  Add-AzureProvisioningConfig -AdminUsername $LocalAdminCred.UserName -Password $LocalAdminCred.GetNetworkCredential().Password `
                      -Domain $netbios -DomainUserName $domainUserName -DomainPassword $DomainUserCred.GetNetworkCredential().Password `
                      -JoinDomain $DomainFQDN -WindowsDomain
        }
        else
        {
            $vm = $vm | `
                  Add-AzureProvisioningConfig -AdminUsername $LocalAdminCred.UserName -Password $LocalAdminCred.GetNetworkCredential().Password `
                      -Domain $netbios -DomainUserName $domainUserName -DomainPassword $DomainUserCred.GetNetworkCredential().Password `
                      -JoinDomain $DomainFQDN -WindowsDomain -CustomDataFile $HnNameFile
        }

        $vm = $vm | `
              Set-AzureVMBGInfoExtension | `
              Set-AzureSubnet -SubnetNames $SubnetName

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


function ConvertSecureStrToPlain 
{
    param
    ( 
        [Parameter(Mandatory=$true)]
        [System.Security.SecureString] $SecurePassword
    )
    
    $pswPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePassword)
    $plainPsw = [Runtime.InteropServices.Marshal]::PtrToStringAuto($pswPointer)
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pswPointer)
    return $plainPsw
}
