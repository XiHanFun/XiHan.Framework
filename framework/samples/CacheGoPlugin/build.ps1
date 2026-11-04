# 构建 cache-go 并把清单放进产物目录 bin/
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$output = Join-Path $root 'bin'
$executable = if ($IsWindows -or $env:OS -eq 'Windows_NT') { 'cache-go.exe' } else { 'cache-go' }

New-Item -ItemType Directory -Force $output | Out-Null
Push-Location $root
try {
    go build -o (Join-Path $output $executable) .
    if ($LASTEXITCODE -ne 0) { throw "go build failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}
Copy-Item (Join-Path $root 'xihan.manifest.json') (Join-Path $output 'xihan.manifest.json') -Force
Write-Host "Built $(Join-Path $output $executable)"
