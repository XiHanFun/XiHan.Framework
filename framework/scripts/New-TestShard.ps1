<#
.SYNOPSIS
    按 framework/test/shards.json 生成一个测试分片的解决方案筛选器，输出它的路径

.DESCRIPTION
    分片表只列要外部服务或工具链的测试项目；没列进任何分片的测试项目归 core
    筛选器写到 framework/artifacts/shards/<分片>.slnf，只含这一片的测试项目，被引用的正式包与示例由项目引用带出
    CI 的各测试作业与本机用同一份：dotnet build <筛选器> -c Release，再 dotnet test --solution <筛选器> -c Release --no-build

.PARAMETER Shard
    分片名：core，或分片表里的名字

.EXAMPLE
    pwsh -File framework/scripts/New-TestShard.ps1 -Shard database
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Shard
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$framework = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $framework 'test'
$table = Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'shards.json') | ConvertFrom-Json

# 测试项目 = test 下有同名项目文件的目录，与架构测试的口径一致
$all = @(Get-ChildItem $testRoot -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).csproj") } |
    ForEach-Object { $_.Name } |
    Sort-Object)

$listed = @($table.shards | ForEach-Object { $_.projects } | Sort-Object -Unique)
$unknown = @($listed | Where-Object { $_ -notin $all })
if ($unknown.Count -gt 0) {
    throw "framework/test/shards.json lists test projects that do not exist: $($unknown -join ', ')."
}

if ($Shard -eq 'core') {
    # core 是其余全部：新加的测试项目不用登记就会被跑到
    $projects = @($all | Where-Object { $_ -notin $listed })
}
else {
    $entry = @($table.shards | Where-Object { $_.name -eq $Shard })
    if ($entry.Count -ne 1) {
        throw "Unknown shard '$Shard'. Known shards: core, $(($table.shards | ForEach-Object { $_.name }) -join ', ')."
    }

    $projects = @($entry[0].projects)
}

if ($projects.Count -eq 0) {
    throw "Shard '$Shard' has no test projects."
}

$directory = Join-Path $framework 'artifacts/shards'
New-Item -ItemType Directory -Force $directory | Out-Null
$path = Join-Path $directory "$Shard.slnf"

# 筛选器里解决方案的路径相对筛选器自身，项目的路径相对解决方案
$filter = [ordered]@{
    solution = [ordered]@{
        path     = '../../XiHan.Framework.slnx'
        projects = @($projects | ForEach-Object { "test/$_/$_.csproj" })
    }
}
$filter | ConvertTo-Json -Depth 4 | Set-Content -Path $path -Encoding utf8NoBOM

Write-Output $path
