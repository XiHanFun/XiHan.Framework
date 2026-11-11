<#
.SYNOPSIS
    性能门禁：按预算与对照提交检查基准结果

.DESCRIPTION
    CI 的「性能门禁」作业执行（开发计划第九节、总体方案 11.2，ADR-0173）。任一检查不过即以非零退出：
    1. 绝对预算：framework/benchmarks/budgets.json 里列出的基准都要有结果，带 maxMeanNanoseconds 的平均耗时不超过它；
       列出的程序集合计不超过 assemblies.maxBytes。
    2. 相对回退（给了 -Base 时）：同一台机器上先测对照提交、再测当前提交。
       - 耗时：逐个基准比较每次操作的耗时，慢了超过 regressionThreshold 且单侧 Mann-Whitney 检验的 p 值小于 significance 才算回退；
         有回退时两边各重测一次这几个基准，复测仍回退才失败——共享运行器的噪声会偶发一次，很难连续两次同向。
       - 分配：每次操作分配的字节数增加超过 regressionThreshold 即失败；分配在同一台机器上是确定的，不复测。
         不在仓库里记绝对字节数：启动路径里有随操作系统变化的分配，Windows 上量的数到 Linux 上对不上。
       - 冷启动与常驻内存：全新进程里装配空应用并启动（ResidentMemoryProbe），各跑 7 次取中位数；
         冷启动耗时、框架额外常驻内存（装配之后减去运行时自己）增加超过 regressionThreshold 时复测一次，仍超过才失败。
    有意的回退（新增能力本来就要多分配、多花时间）用 -AcceptRegression 放行：相对回退记为「已接受」不阻断，绝对预算照常；
    CI 在最后一个提交的说明带 [perf-accepted] 或 PR 带 perf-accepted 标签时传它。对照提交随每次推送前移，接受只管这一次。
    冷启动 coldStart.targetMilliseconds 与常驻内存 residentMemory.targetBytes 两个绝对目标只报告不阻断：
    JIT 模式下实测都达不到（Linux 上运行时自己就占约 27 MB），口径待定（ADR-0173）。
    结果写成 Markdown，CI 里追加到作业摘要。

.PARAMETER Head
    当前提交的基准程序目录（含 XiHan.Framework.Benchmarks.dll）

.PARAMETER Base
    对照提交的基准程序目录；空则跳过相对回退

.PARAMETER Artifacts
    基准结果与报告的输出目录

.PARAMETER AcceptRegression
    相对回退只报告不阻断

.PARAMETER Budgets
    预算文件，缺省是本脚本旁边的 ../benchmarks/budgets.json

.EXAMPLE
    pwsh -File framework/scripts/Test-Benchmarks.ps1 -Head framework/benchmarks/XiHan.Framework.Benchmarks/bin/Release/net10.0 -Artifacts artifacts/benchmarks
#>
param(
    [Parameter(Mandatory)][string]$Head,
    [string]$Base = '',
    [Parameter(Mandatory)][string]$Artifacts,
    [string]$Budgets = (Join-Path $PSScriptRoot '../benchmarks/budgets.json'),
    [switch]$AcceptRegression
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$program = 'XiHan.Framework.Benchmarks.dll'
$probeRuns = 7
$budget = Get-Content $Budgets -Raw -Encoding utf8 | ConvertFrom-Json
$threshold = [double]$budget.regressionThreshold
$significance = [double]$budget.significance
$failures = [System.Collections.Generic.List[string]]::new()
# 相对回退单独收集：-AcceptRegression 时只报告
$regressions = [System.Collections.Generic.List[string]]::new()
$report = [System.Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null

# 跑一组基准，返回 全名 → 结果；$filters 是按「命名空间.类型.方法」的过滤，参数化的基准整组跑
function Invoke-Benchmarks([string]$dir, [string]$name, [string[]]$filters) {
    $out = Join-Path $Artifacts $name
    & dotnet (Join-Path $dir $program) --filter @filters --exporters fulljson --artifacts $out | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Benchmark run '$name' exited with code $LASTEXITCODE." }
    $results = @{}
    foreach ($file in Get-ChildItem (Join-Path $out 'results') -Filter '*-report-full.json' -ErrorAction SilentlyContinue) {
        foreach ($benchmark in (Get-Content $file.FullName -Raw -Encoding utf8 | ConvertFrom-Json).Benchmarks) {
            $results[$benchmark.FullName] = $benchmark
        }
    }
    $results
}

# 每次操作的耗时（纳秒）：只取正式测量（Workload / Result）
function Get-Samples($benchmark) {
    @($benchmark.Measurements |
        Where-Object { $_.IterationMode -eq 'Workload' -and $_.IterationStage -eq 'Result' } |
        ForEach-Object { [double]$_.Nanoseconds / [double]$_.Operations })
}

function Get-Median([double[]]$values) {
    $sorted = @($values | Sort-Object)
    $middle = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) { $sorted[$middle] } else { ($sorted[$middle - 1] + $sorted[$middle]) / 2 }
}

# 标准正态分布的上尾概率（Abramowitz–Stegun 7.1.26，误差 < 1.5e-7）
function Get-UpperTail([double]$z) {
    $x = [math]::Abs($z) / [math]::Sqrt(2)
    $t = 1 / (1 + 0.3275911 * $x)
    $erf = 1 - (((((1.061405429 * $t - 1.453152027) * $t) + 1.421413741) * $t - 0.284496736) * $t + 0.254829592) * $t * [math]::Exp(-$x * $x)
    1 - 0.5 * (1 + [math]::Sign($z) * $erf)
}

# 单侧 Mann-Whitney U 检验：$slower 是否整体大于 $faster；正态近似，并列取平均秩，带连续性校正
function Get-PValue([double[]]$slower, [double[]]$faster) {
    $n1 = $slower.Count; $n2 = $faster.Count; $n = $n1 + $n2
    if ($n1 -lt 3 -or $n2 -lt 3) { return 1.0 }
    $all = @(@($slower | ForEach-Object { [pscustomobject]@{ V = $_; S = $true } }) + @($faster | ForEach-Object { [pscustomobject]@{ V = $_; S = $false } }) | Sort-Object V)
    $rankSum = 0.0; $tieTerm = 0.0; $i = 0
    while ($i -lt $n) {
        $j = $i
        while ($j + 1 -lt $n -and $all[$j + 1].V -eq $all[$i].V) { $j++ }
        $rank = ($i + $j + 2) / 2
        $ties = $j - $i + 1
        $tieTerm += [math]::Pow($ties, 3) - $ties
        for ($k = $i; $k -le $j; $k++) { if ($all[$k].S) { $rankSum += $rank } }
        $i = $j + 1
    }
    $u = $rankSum - $n1 * ($n1 + 1) / 2
    $sigma = [math]::Sqrt($n1 * $n2 / 12 * (($n + 1) - $tieTerm / ($n * ($n - 1))))
    if ($sigma -eq 0) { return 1.0 }
    Get-UpperTail (($u - $n1 * $n2 / 2 - 0.5) / $sigma)
}

# 逐个基准比较耗时与分配，返回耗时回退的基准全名；分配回退直接记失败
function Compare-Runs($headResults, $baseResults, [string]$label, [bool]$checkAllocations) {
    $regressed = [System.Collections.Generic.List[string]]::new()
    foreach ($name in ($headResults.Keys | Sort-Object)) {
        if (-not $baseResults.ContainsKey($name)) { $report.Add("| $name | $label | 对照提交没有这个基准 | | |"); continue }
        $headSamples = Get-Samples $headResults[$name]; $baseSamples = Get-Samples $baseResults[$name]
        $ratio = (Get-Median $headSamples) / (Get-Median $baseSamples)
        $p = Get-PValue $headSamples $baseSamples
        $headBytes = [double]$headResults[$name].Memory.BytesAllocatedPerOperation
        $baseBytes = [double]$baseResults[$name].Memory.BytesAllocatedPerOperation
        $report.Add(('| {0} | {1} | {2:+0.0%;-0.0%;0.0%} | {3:G3} | {4:N0} → {5:N0} B |' -f $name, $label, ($ratio - 1), $p, $baseBytes, $headBytes))
        if ($ratio -gt (1 + $threshold) -and $p -lt $significance) { $regressed.Add($name) }
        if ($checkAllocations -and $headBytes -gt $baseBytes * (1 + $threshold)) {
            $regressions.Add("Benchmark '$name' allocates $headBytes B per operation, up from $baseBytes B on the base commit.")
        }
    }
    $regressed
}

# 全新进程里的冷启动与常驻内存，跑 $probeRuns 次取中位数；探针不存在（对照提交还没有它）时返回 $null
function Measure-Probe([string]$dir, [string]$name) {
    $samples = [System.Collections.Generic.List[object]]::new()
    foreach ($run in 1..$probeRuns) {
        $file = Join-Path $Artifacts "$name-probe-$run.json"
        & dotnet (Join-Path $dir $program) --resident-memory $file | Out-Null
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $file)) { return $null }
        $probe = Get-Content $file -Raw | ConvertFrom-Json
        if ($null -eq $probe.coldStartMilliseconds) { return $null }
        $samples.Add([pscustomobject]@{
                Resident  = [double]$probe.residentBytes
                Overhead  = [double]$probe.residentBytes - [double]$probe.runtimeBytes
                ColdStart = [double]$probe.coldStartMilliseconds
            })
    }
    [pscustomobject]@{
        Resident  = Get-Median @($samples.Resident)
        Overhead  = Get-Median @($samples.Overhead)
        ColdStart = Get-Median @($samples.ColdStart)
    }
}

# 冷启动与额外常驻内存相对对照提交的变化
function Compare-Probes($headProbe, $baseProbe) {
    [pscustomobject]@{
        ColdStart = $headProbe.ColdStart / $baseProbe.ColdStart - 1
        Overhead  = $headProbe.Overhead / $baseProbe.Overhead - 1
    }
}

$headResults = Invoke-Benchmarks $Head 'head' @('*')

# 1：绝对预算
$report.Add('### 预算'); $report.Add(''); $report.Add('| 基准 | 平均耗时 | 预算 | 每次分配 |'); $report.Add('| --- | --- | --- | --- |')
foreach ($property in $budget.benchmarks.PSObject.Properties) {
    $name = $property.Name
    if (-not $headResults.ContainsKey($name)) { $failures.Add("Benchmark '$name' has no result."); continue }
    $mean = [double]$headResults[$name].Statistics.Mean
    $limit = $property.Value.maxMeanNanoseconds
    $report.Add(('| {0} | {1:N1} ns | {2} | {3:N0} B |' -f $name, $mean, $(if ($null -ne $limit) { '{0:N0} ns' -f [double]$limit } else { '—' }), [double]$headResults[$name].Memory.BytesAllocatedPerOperation))
    if ($null -ne $limit -and $mean -gt [double]$limit) { $failures.Add("Benchmark '$name' mean $([math]::Round($mean, 1)) ns exceeds the budget $limit ns.") }
}

$size = 0L
foreach ($file in $budget.assemblies.files) { $size += (Get-Item (Join-Path $Head $file)).Length }
$report.Add('')
$report.Add(('程序集 {0} 合计 {1:N0} B，上限 {2:N0} B。' -f ($budget.assemblies.files -join ' + '), $size, [long]$budget.assemblies.maxBytes))
if ($size -gt [long]$budget.assemblies.maxBytes) { $failures.Add("Assemblies total $size B, over the budget $($budget.assemblies.maxBytes) B.") }

$headProbe = Measure-Probe $Head 'head'
if ($null -eq $headProbe) { throw 'The resident memory probe of the head commit produced no result.' }
$coldTarget = [double]$budget.coldStart.targetMilliseconds
$memoryTarget = [double]$budget.residentMemory.targetBytes
$report.Add(('冷启动 {0:N1} ms，目标 {1:N0} ms{2}。' -f $headProbe.ColdStart, $coldTarget, $(if ($headProbe.ColdStart -gt $coldTarget) { '（未达到，只报告）' } else { '' })))
$report.Add(('空应用常驻内存 {0:N1} MB，其中框架额外 {1:N1} MB，目标 {2:N0} MB{3}。' -f ($headProbe.Resident / 1MB), ($headProbe.Overhead / 1MB), ($memoryTarget / 1MB), $(if ($headProbe.Resident -gt $memoryTarget) { '（未达到，只报告）' } else { '' })))

# 2：相对回退
if ($Base) {
    $report.Add(''); $report.Add('### 相对对照提交'); $report.Add(''); $report.Add('| 基准 | 轮次 | 耗时变化（中位数） | p | 每次分配 |'); $report.Add('| --- | --- | --- | --- | --- |')
    $baseResults = Invoke-Benchmarks $Base 'base' @('*')
    $regressed = Compare-Runs $headResults $baseResults '首测' $true
    if ($regressed.Count -gt 0) {
        # 复测：两边只重跑回退的基准；先跑对照再跑当前，与首测同序
        $filters = @($regressed | ForEach-Object { $_ -replace '\(.*$', '' } | Sort-Object -Unique)
        $baseAgain = Invoke-Benchmarks $Base 'base-confirm' $filters
        $headAgain = Invoke-Benchmarks $Head 'head-confirm' $filters
        $retest = @{}; foreach ($name in $regressed) { if ($headAgain.ContainsKey($name)) { $retest[$name] = $headAgain[$name] } }
        foreach ($name in (Compare-Runs $retest $baseAgain '复测' $false)) {
            $regressions.Add("Benchmark '$name' is slower than the base commit by more than $($threshold * 100)% in two runs.")
        }
    }

    $baseProbe = Measure-Probe $Base 'base'
    $report.Add('')
    if ($null -eq $baseProbe) {
        $report.Add('对照提交还没有冷启动与常驻内存探针，跳过这两项的相对比较。')
    }
    else {
        $change = Compare-Probes $headProbe $baseProbe
        $report.Add(('冷启动相对对照提交 {0:+0.0%;-0.0%;0.0%}，框架额外常驻内存 {1:+0.0%;-0.0%;0.0%}。' -f $change.ColdStart, $change.Overhead))
        if ($change.ColdStart -gt $threshold -or $change.Overhead -gt $threshold) {
            $baseProbe = Measure-Probe $Base 'base-confirm'; $headProbe = Measure-Probe $Head 'head-confirm'
            $change = Compare-Probes $headProbe $baseProbe
            $report.Add(('复测：冷启动 {0:+0.0%;-0.0%;0.0%}，框架额外常驻内存 {1:+0.0%;-0.0%;0.0%}。' -f $change.ColdStart, $change.Overhead))
            if ($change.ColdStart -gt $threshold) { $regressions.Add("Cold start is slower than the base commit by $([math]::Round($change.ColdStart * 100, 1))% in two runs.") }
            if ($change.Overhead -gt $threshold) { $regressions.Add("Framework resident memory grew by $([math]::Round($change.Overhead * 100, 1))% over the base commit in two runs.") }
        }
    }
}

if ($AcceptRegression) {
    $notes = @($regressions | ForEach-Object { "- 已接受的回退：$_" })
}
else {
    foreach ($regression in $regressions) { $failures.Add($regression) }
    $notes = @()
}

$markdown = (@('## 性能门禁', '') + $report + @('') + $notes + @($failures | ForEach-Object { "- 失败：$_" })) -join "`n"
Set-Content -Path (Join-Path $Artifacts 'report.md') -Value $markdown -Encoding utf8
if ($env:GITHUB_STEP_SUMMARY) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $markdown -Encoding utf8 }
Write-Host $markdown
if ($failures.Count -gt 0) { exit 1 }
