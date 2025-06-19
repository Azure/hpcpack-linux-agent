param(
  # .NET Runtime ID
  [ValidateSet("linux-x64", "linux-arm64")]
  [string]
  $Rid = "linux-x64",

  # Build Configuration
  [ValidateSet("Release", "Debug")]
  [string]
  $Config = "Release",

  # Directory for end user package
  [string]
  $OutDir
)

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

$srcDir = Join-Path $PSScriptRoot ".." "src" -Resolve
$agentProjFile = Join-Path $srcDir "NodeAgent" "NodeAgent.csproj" -Resolve
$agentSetupBuildFile = Join-Path $srcDir "NodeAgentSetup" "Build.ps1" -Resolve

$tempDir = [System.IO.Path]::GetTempPath()
$publishDir = Join-Path $tempDir (New-Guid).ToString()
New-Item -Path $publishDir -Type Directory -Force | Out-Null

Write-Information "Publishing to $publishDir"
dotnet publish $agentProjFile -c $Config -r $Rid --sc -o $publishDir

if (!$OutDir) {
  $OutDir = Join-Path (Get-Location).Path 'out'
}
New-Item -Path $OutDir -Type Directory -Force | Out-Null

Write-Information "Making setup package into $OutDir"
Invoke-Expression "$agentSetupBuildFile $publishDir $OutDir"
