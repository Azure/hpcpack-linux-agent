param(
  # Directory of published artifacts
  [Parameter(Mandatory)]
  [string]
  $PublishDir,

  # Output directory
  [string]
  $OutDir
)

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

$tempDir = [System.IO.Path]::GetTempPath()
$pkgSrcDir = Join-Path $tempDir (New-Guid).ToString()
New-Item -Path $pkgSrcDir -Type Directory -Force | Out-Null

Write-Information "Copying published artifacts from $PublishDir to $pkgSrcDir"
Copy-Item -Path "$PublishDir/*" -Destination $pkgSrcDir -Recurse

$scriptsDir = Join-Path $PSScriptRoot "Scripts" -Resolve
Write-Information "Copying setup files from $scriptsDir to $pkgSrcDir"
Copy-Item -Path "$scriptsDir/*" -Destination $pkgSrcDir -Recurse

if (!$OutDir) {
  $OutDir = Join-Path (Get-Location).Path 'out'
}
New-Item -Path $OutDir -Type Directory -Force | Out-Null

$pkgFile = Join-Path $OutDir 'hpcnodeagent.tar.gz'
Write-Information "Making pacakge $pkgFile"
tar -cvzf $pkgFile -C $pkgSrcDir .

Write-Information "Copying setup.py to $OutDir"
Copy-Item -Path "$pkgSrcDir/setup.py" $OutDir -Force
