<#
.SYNOPSIS
    按 PublicAPI 分析器的诊断同步 PublicAPI.Unshipped.txt：RS0016 的符号追加，RS0017 的符号移除。

.DESCRIPTION
    以英文界面语言构建指定项目（Release，不还原），解析 RS0016 / RS0017 诊断中的符号签名，写回项目目录下的 PublicAPI.Unshipped.txt。
    已发布文件 PublicAPI.Shipped.txt 永不修改；若 RS0017 指向已发布符号，脚本失败，因为那是破坏性变更。

.PARAMETER Project
    项目文件路径。

.EXAMPLE
    pwsh -File next/scripts/Sync-PublicApi.ps1 -Project next/src/XiHan.Framework.Kernel/XiHan.Framework.Kernel.csproj
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Project
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$env:DOTNET_CLI_UI_LANGUAGE = 'en'

$projectPath = (Resolve-Path $Project).Path
$directory = Split-Path -Parent $projectPath
$unshippedPath = Join-Path $directory 'PublicAPI.Unshipped.txt'
$shippedPath = Join-Path $directory 'PublicAPI.Shipped.txt'

if (-not (Test-Path $unshippedPath)) {
    throw "Missing $unshippedPath"
}

$output = & dotnet build $projectPath -c Release --no-restore 2>&1 | ForEach-Object { "$_" }

$toAdd = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$toRemove = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

foreach ($line in $output) {
    # 依赖项目的诊断也会出现在输出里，只取属于本项目的行（MSBuild 在行尾标注 [项目路径]）
    if (-not $line.Contains("[$projectPath]")) { continue }
    $add = [regex]::Match($line, "RS0016: Symbol '(.+?)' is not part of the declared public API")
    if ($add.Success) { [void]$toAdd.Add($add.Groups[1].Value); continue }
    $remove = [regex]::Match($line, "RS0017: Symbol '(.+?)' is part of the declared API")
    if ($remove.Success) { [void]$toRemove.Add($remove.Groups[1].Value) }
}

$shipped = if (Test-Path $shippedPath) { @(Get-Content $shippedPath) } else { @() }
$removedShipped = @($toRemove | Where-Object { $shipped -contains $_ })
if ($removedShipped.Count -gt 0) {
    Write-Error ("Shipped symbols cannot be removed (breaking change):`n" + ($removedShipped -join "`n"))
}

$lines = @(Get-Content $unshippedPath)
$header = @($lines | Where-Object { $_ -like '#*' })
$body = [System.Collections.Generic.List[string]]::new()
foreach ($line in $lines) {
    if ($line -like '#*' -or [string]::IsNullOrWhiteSpace($line)) { continue }
    if ($toRemove.Contains($line)) { continue }
    $body.Add($line)
}
foreach ($symbol in $toAdd) {
    if (-not $body.Contains($symbol) -and -not ($shipped -contains $symbol)) { $body.Add($symbol) }
}

$sorted = @($body | Sort-Object -Unique -Culture ([System.Globalization.CultureInfo]::InvariantCulture))
$content = @($header) + $sorted
[System.IO.File]::WriteAllText($unshippedPath, (($content -join "`n") + "`n"), [System.Text.UTF8Encoding]::new($false))

Write-Host ("{0}: +{1} -{2} = {3} symbols" -f (Split-Path -Leaf $directory), $toAdd.Count, $toRemove.Count, $sorted.Count)
