<#
.SYNOPSIS
    发版时把各包 PublicAPI.Unshipped.txt 的符号移进 PublicAPI.Shipped.txt

.DESCRIPTION
    在版本提交里执行一次（ADR-0131、ADR-0167）：每个包的未发布符号并入已发布文件，未发布文件只留表头。
    *REMOVED* 行表示撤下一个已发布符号：从已发布文件删去该符号，不再保留这一行。
    两份文件按序数排序、去重，与 PublicAPI 分析器和 Sync-PublicApi.ps1 的写法一致。
    已发布文件里找不到某个 *REMOVED* 行指向的符号时脚本失败，因为那说明未发布文件与已发布文件不一致。

.PARAMETER Root
    要处理的目录，缺省是本脚本旁边的 ../src；其下每个同时有两份 PublicAPI 文件的目录各处理一次

.PARAMETER Check
    只报告每个包要移动与撤下的符号数，不写文件

.EXAMPLE
    pwsh -File framework/scripts/Ship-PublicApi.ps1 -Check

.EXAMPLE
    pwsh -File framework/scripts/Ship-PublicApi.ps1
#>
param(
    [string]$Root = (Join-Path $PSScriptRoot '../src'),
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$utf8 = [System.Text.UTF8Encoding]::new($false)
$removedPrefix = '*REMOVED*'

# 表头是 # 开头的行（#nullable enable）；正文是其余非空行
function Read-ApiFile([string]$path) {
    $lines = if (Test-Path $path) { @(Get-Content $path -Encoding utf8) } else { @() }
    [pscustomobject]@{
        Header = @($lines | Where-Object { $_ -like '#*' })
        Body   = @($lines | Where-Object { $_ -notlike '#*' -and -not [string]::IsNullOrWhiteSpace($_) })
    }
}

function Write-ApiFile([string]$path, [string[]]$header, [System.Collections.Generic.IEnumerable[string]]$body) {
    # 序数排序：与分析器及 Sync-PublicApi.ps1 一致，文化排序会把 const / static / override 行挪位制造整文件噪音
    $sorted = [System.Collections.Generic.List[string]]::new([System.Collections.Generic.HashSet[string]]::new($body, [System.StringComparer]::Ordinal))
    $sorted.Sort([System.StringComparer]::Ordinal)
    $content = @($header) + @($sorted)
    [System.IO.File]::WriteAllText($path, (($content -join "`n") + "`n"), $utf8)
}

$rootPath = (Resolve-Path $Root).Path
$failures = [System.Collections.Generic.List[string]]::new()
$plans = [System.Collections.Generic.List[object]]::new()
$total = 0

foreach ($directory in Get-ChildItem $rootPath -Directory | Sort-Object Name) {
    $unshippedPath = Join-Path $directory.FullName 'PublicAPI.Unshipped.txt'
    $shippedPath = Join-Path $directory.FullName 'PublicAPI.Shipped.txt'
    if (-not (Test-Path $unshippedPath) -or -not (Test-Path $shippedPath)) { continue }

    $unshipped = Read-ApiFile $unshippedPath
    $shipped = Read-ApiFile $shippedPath
    $result = [System.Collections.Generic.HashSet[string]]::new([string[]]$shipped.Body, [System.StringComparer]::Ordinal)

    $added = 0
    $removed = 0
    foreach ($line in $unshipped.Body) {
        if ($line.StartsWith($removedPrefix, [System.StringComparison]::Ordinal)) {
            $symbol = $line.Substring($removedPrefix.Length)
            if (-not $result.Remove($symbol)) {
                $failures.Add("$($directory.Name): '$symbol' is marked removed but is not in PublicAPI.Shipped.txt")
                continue
            }

            $removed++
            continue
        }

        if ($result.Add($line)) { $added++ }
    }

    # 两份文件的表头取并集：任何一份声明了 #nullable enable，合并后的已发布文件也要声明
    $header = @(@($shipped.Header) + @($unshipped.Header) | Select-Object -Unique)
    $plans.Add([pscustomobject]@{ Name = $directory.Name; Shipped = $shippedPath; Unshipped = $unshippedPath; Header = $header; Result = $result; Added = $added; Removed = $removed })
    $total += $added
}

# 先核对全部包再写：任何一个包不一致就一个文件都不改，不留下一半已转移的状态
if ($failures.Count -gt 0) {
    Write-Error ("PublicAPI files are inconsistent:`n" + ($failures -join "`n"))
}

foreach ($plan in $plans) {
    if (-not $Check) {
        Write-ApiFile $plan.Shipped $plan.Header $plan.Result
        Write-ApiFile $plan.Unshipped $plan.Header ([string[]]@())
    }

    if ($plan.Added -gt 0 -or $plan.Removed -gt 0) {
        Write-Host ("{0}: +{1} -{2} = {3} shipped symbols" -f $plan.Name, $plan.Added, $plan.Removed, $plan.Result.Count)
    }
}

Write-Host ("{0} {1} symbols" -f ($(if ($Check) { 'Would ship' } else { 'Shipped' })), $total)
