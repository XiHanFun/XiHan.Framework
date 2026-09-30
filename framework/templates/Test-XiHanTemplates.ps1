#Requires -Version 7.2
<#
.SYNOPSIS
    验证 dotnet new 模板 xihan-web / xihan-module 的生成产物可还原、构建、测试并启动。

.DESCRIPTION
    全程在隔离工作目录内进行：
    1. 把框架打包到隔离的本地源，把模板打包成 XiHan.Framework.Templates；
    2. 模板装进隔离的模板库（--debug:custom-hive），还原用隔离的 NUGET_PACKAGES 与 NuGet.Config；
    3. 逐个场景生成、Release 构建、MTP 测试，Web 场景再以随机端口启动并请求示例端点；
    4. 校验拒绝覆盖已有文件、无样板残留、用户模板库未被改动，最后卸载模板。
    不修改全局 NuGet 配置、用户模板库或仓库内文件。

.PARAMETER Configuration
    构建配置，默认 Release。

.PARAMETER WorkRoot
    隔离工作目录，默认在系统临时目录下新建。

.PARAMETER NoFrameworkBuild
    框架已按同一配置构建过时跳过重复构建，直接打包。

.PARAMETER KeepWorkRoot
    结束后保留工作目录（默认成功时删除）。

.EXAMPLE
    pwsh -NoProfile -File framework/templates/Test-XiHanTemplates.ps1
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $WorkRoot,
    [switch] $NoFrameworkBuild,
    [switch] $KeepWorkRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$solutionPath = Join-Path $repoRoot 'framework' 'XiHan.Framework.slnx'
$templateProject = Join-Path $PSScriptRoot 'XiHan.Framework.Templates.csproj'

if (-not $WorkRoot) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) "xihan-templates-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
}
$WorkRoot = [System.IO.Path]::GetFullPath($WorkRoot)
if ((Test-Path $WorkRoot) -and (Get-ChildItem $WorkRoot -Force | Select-Object -First 1)) {
    throw "工作目录非空：$WorkRoot"
}

$feedDir = Join-Path $WorkRoot 'feed'
$templatePackageDir = Join-Path $WorkRoot 'template-package'
$hiveDir = Join-Path $WorkRoot 'hive'
$outputRoot = Join-Path $WorkRoot 'out dir'
$logDir = Join-Path $WorkRoot 'logs'
New-Item -ItemType Directory -Force $feedDir, $templatePackageDir, $hiveDir, $outputRoot, $logDir | Out-Null

$env:NUGET_PACKAGES = Join-Path $WorkRoot 'packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_UI_LANGUAGE = 'en-US'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:UseSharedCompilation = 'false'

$script:Results = [System.Collections.Generic.List[object]]::new()
$script:Stage = '准备'

# 构建产物允许出现的警告：XiHan.Framework.Analyzers 在 SDK 10.0.1xx 下不可加载时的 CS9057
$AllowedWarningPatterns = @(
    'warning CS9057: .*XiHan\.Framework\.Analyzers'
)

function Write-Stage([string] $Name) {
    $script:Stage = $Name
    Write-Host ''
    Write-Host "==== $Name ====" -ForegroundColor Cyan
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory)] [string] $FilePath,
        [Parameter(Mandatory)] [string[]] $ArgumentList,
        [string] $WorkingDirectory = $repoRoot,
        [int[]] $AllowedExitCodes = @(0),
        [switch] $AllowAnyExitCode,
        [string] $LogName
    )

    $display = "$FilePath $($ArgumentList -join ' ')"
    Write-Host "> $display" -ForegroundColor DarkGray
    Push-Location $WorkingDirectory
    try {
        $output = & $FilePath @ArgumentList 2>&1 | ForEach-Object { "$_" -replace "`e\[[0-9;]*[A-Za-z]", '' }
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($LogName) {
        $output | Set-Content -Path (Join-Path $logDir "$LogName.log") -Encoding utf8
    }

    if (-not $AllowAnyExitCode -and $AllowedExitCodes -notcontains $exitCode) {
        $output | Select-Object -Last 60 | ForEach-Object { Write-Host $_ }
        throw "阶段「$($script:Stage)」命令失败（退出码 $exitCode）：$display"
    }

    return [pscustomobject]@{ ExitCode = $exitCode; Output = $output }
}

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) {
        throw "阶段「$($script:Stage)」断言失败：$Message"
    }
}

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function New-TemplateOutput {
    param(
        [Parameter(Mandatory)] [string] $Template,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $Directory,
        [string[]] $Options = @()
    )

    $arguments = @('new', $Template, '-n', $Name, '-o', $Directory, '--debug:custom-hive', $hiveDir) + $Options
    Invoke-Checked -FilePath 'dotnet' -ArgumentList $arguments -LogName "new-$(Split-Path $Directory -Leaf)" | Out-Null
}

function Invoke-BuildAndTest {
    param(
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [string] $Scenario,
        [int] $ExpectedTests
    )

    $build = Invoke-Checked -FilePath 'dotnet' -ArgumentList @('build', '-c', $Configuration, '-nologo') -WorkingDirectory $Directory -LogName "build-$Scenario"
    $warnings = @($build.Output |
        Where-Object { $_ -match 'warning [A-Z]+[0-9]+' } |
        Where-Object { $line = $_; -not ($AllowedWarningPatterns | Where-Object { $line -match $_ }) } |
        Sort-Object -Unique)
    Assert-True ($warnings.Count -eq 0) "构建出现未允许的警告：`n$($warnings -join "`n")"

    $test = Invoke-Checked -FilePath 'dotnet' -ArgumentList @('test', '-c', $Configuration, '--no-build') -WorkingDirectory $Directory -LogName "test-$Scenario"
    $summary = ($test.Output -join "`n")
    $total = if ($summary -match '(?m)^\s*total:\s*(\d+)') { [int]$Matches[1] } else { -1 }
    $failed = if ($summary -match '(?m)^\s*failed:\s*(\d+)') { [int]$Matches[1] } else { -1 }
    Assert-True ($total -gt 0) '测试数量为 0 或无法解析测试摘要'
    Assert-True ($failed -eq 0) "有 $failed 个测试失败"
    if ($ExpectedTests -gt 0) {
        Assert-True ($total -eq $ExpectedTests) "测试数量 $total 与预期 $ExpectedTests 不符"
    }

    return $total
}

function Start-WebApp {
    param(
        [Parameter(Mandatory)] [string] $ProjectDirectory,
        [Parameter(Mandatory)] [string] $AssemblyName,
        [Parameter(Mandatory)] [string] $LogName
    )

    $port = Get-FreeTcpPort
    $baseUrl = "http://127.0.0.1:$port"
    $dll = Join-Path $ProjectDirectory 'bin' $Configuration 'net10.0' "$AssemblyName.dll"
    Assert-True (Test-Path $dll) "找不到构建产物 $dll"

    $stdout = Join-Path $logDir "$LogName.out.log"
    $stderr = Join-Path $logDir "$LogName.err.log"
    $process = Start-Process -FilePath 'dotnet' -ArgumentList @("`"$dll`"", '--urls', $baseUrl) -WorkingDirectory $ProjectDirectory `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru -NoNewWindow

    $lastStatus = '无响应'
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) {
            Get-Content $stdout, $stderr -ErrorAction SilentlyContinue | Select-Object -Last 60 | ForEach-Object { Write-Host $_ }
            throw "阶段「$($script:Stage)」应用启动后退出（退出码 $($process.ExitCode)）"
        }
        try {
            $response = Invoke-WebRequest -Uri "$baseUrl/api/Hello/Greeting?name=smoke" -SkipHttpErrorCheck -TimeoutSec 5
            if ($response.StatusCode -eq 200) {
                return [pscustomobject]@{ Process = $process; BaseUrl = $baseUrl }
            }
            $lastStatus = "HTTP $($response.StatusCode)"
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }

    Stop-WebApp ([pscustomobject]@{ Process = $process; BaseUrl = $baseUrl })
    Get-Content $stdout, $stderr -ErrorAction SilentlyContinue | Select-Object -Last 60 | ForEach-Object { Write-Host $_ }
    throw "阶段「$($script:Stage)」应用 90 秒内未就绪：$baseUrl（最后一次：$lastStatus）"
}

function Stop-WebApp($App) {
    if ($null -eq $App) {
        return
    }
    if (-not $App.Process.HasExited) {
        Stop-Process -Id $App.Process.Id -Force -ErrorAction SilentlyContinue
        $App.Process.WaitForExit(15000) | Out-Null
    }
    Assert-True $App.Process.HasExited "应用进程 $($App.Process.Id) 未能终止"
}

function Invoke-Api {
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Uri,
        [hashtable] $Headers = @{},
        [object] $Body
    )

    $parameters = @{ Method = $Method; Uri = $Uri; Headers = $Headers; SkipHttpErrorCheck = $true; TimeoutSec = 30 }
    if ($null -ne $Body) {
        $parameters.Body = ($Body | ConvertTo-Json -Compress)
        $parameters.ContentType = 'application/json'
    }
    $response = Invoke-WebRequest @parameters
    $data = $null
    if ($response.Content -and $response.Headers['Content-Type'] -match 'json') {
        $json = $response.Content | ConvertFrom-Json
        if ($json -and $json.PSObject.Properties['data']) {
            $data = $json.data
        }
    }
    return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Content = $response.Content; Data = $data }
}

function Test-WebSmoke {
    param(
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [string] $Name,
        [switch] $Data,
        [switch] $MultiTenancy,
        [switch] $Observability,
        [Parameter(Mandatory)] [string] $Scenario
    )

    $projectDirectory = Join-Path $Directory 'src' $Name
    $app = $null
    try {
        $app = Start-WebApp -ProjectDirectory $projectDirectory -AssemblyName $Name -LogName "run-$Scenario"

        $hello = Invoke-Api -Method Get -Uri "$($app.BaseUrl)/api/Hello/Greeting?name=XiHan"
        Assert-True ($hello.StatusCode -eq 200 -and $hello.Data -eq '你好，XiHan！') "问候端点返回异常：$($hello.StatusCode) $($hello.Content)"

        $docs = Invoke-Api -Method Get -Uri "$($app.BaseUrl)/openapi/v1.json"
        Assert-True ($docs.StatusCode -eq 200) "OpenAPI 文档不可用：$($docs.StatusCode)"

        $health = Invoke-Api -Method Get -Uri "$($app.BaseUrl)/health"
        if ($Observability) {
            Assert-True ($health.StatusCode -eq 200 -and $health.Content -eq 'Healthy') "健康检查返回异常：$($health.StatusCode) $($health.Content)"
        }
        else {
            Assert-True ($health.StatusCode -eq 404) "未选 observability 时不应映射 /health，实际 $($health.StatusCode)"
        }

        if ($MultiTenancy) {
            $tenant = Invoke-Api -Method Get -Uri "$($app.BaseUrl)/api/Tenant/Current" -Headers @{ 'X-Tenant-Id' = 'demo' }
            Assert-True ($tenant.StatusCode -eq 200 -and "$($tenant.Data.id)" -eq '1') "租户解析异常：$($tenant.StatusCode) $($tenant.Content)"
            $unknown = Invoke-Api -Method Get -Uri "$($app.BaseUrl)/api/Tenant/Current" -Headers @{ 'X-Tenant-Id' = '999' }
            Assert-True ($unknown.StatusCode -eq 400) "未登记租户应被拒绝，实际 $($unknown.StatusCode)"
        }

        $title = $null
        if ($Data) {
            $title = "smoke-$([guid]::NewGuid().ToString('N'))"
            $created = Invoke-Api -Method Post -Uri "$($app.BaseUrl)/api/TodoItem/Create" -Body @{ title = $title }
            Assert-True ($created.StatusCode -eq 200 -and $created.Data.title -eq $title) "创建待办失败：$($created.StatusCode) $($created.Content)"
        }

        Stop-WebApp $app
        $app = $null

        if ($Data) {
            # 再次启动：已存在的库表不被删除，数据仍在
            $app = Start-WebApp -ProjectDirectory $projectDirectory -AssemblyName $Name -LogName "rerun-$Scenario"
            $list = Invoke-Api -Method Get -Uri "$($app.BaseUrl)/api/TodoItem/List"
            Assert-True ($list.StatusCode -eq 200 -and (@($list.Data | Where-Object { $_.title -eq $title }).Count -eq 1)) "重启后数据丢失：$($list.StatusCode) $($list.Content)"
            Stop-WebApp $app
            $app = $null
        }
    }
    finally {
        Stop-WebApp $app
    }
}

function Assert-NoTemplateResidue([string] $Directory) {
    $nameTokens = @('XiHanWebApp', 'WebAppHost', 'webapphost-db', 'XiHanModuleLib', 'FeatureName')
    $contentTokens = $nameTokens + @('localhost:5959', 'XIHAN_FRAMEWORK_VERSION', '__XIHAN_TEMPLATE_PACKAGE_VERSION__', '#if', '#endif', $repoRoot, $repoRoot.Replace('\', '/'))
    $files = Get-ChildItem -Path $Directory -Recurse -File -Force |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|TestResults)[\\/]' -and $_.Extension -notin '.db', '.db-shm', '.db-wal' }
    foreach ($file in $files) {
        $relativePath = [System.IO.Path]::GetRelativePath($Directory, $file.FullName)
        foreach ($token in $nameTokens) {
            Assert-True (-not $relativePath.Contains($token, [StringComparison]::OrdinalIgnoreCase)) "路径残留样板命名「$token」：$relativePath"
        }
        $content = Get-Content -Path $file.FullName -Raw
        if (-not $content) {
            continue
        }
        foreach ($token in $contentTokens) {
            Assert-True (-not $content.Contains($token, [StringComparison]::OrdinalIgnoreCase)) "内容残留「$token」：$relativePath"
        }
    }
}

function Get-UserTemplateList {
    $result = Invoke-Checked -FilePath 'dotnet' -ArgumentList @('new', 'list', 'xihan') -AllowedExitCodes @(0, 103)
    return ($result.Output | Where-Object { $_ -match 'xihan-' }) -join "`n"
}

$succeeded = $false
try {
    Write-Stage '记录用户模板库现状'
    $userTemplatesBefore = Get-UserTemplateList

    if (-not $NoFrameworkBuild) {
        Write-Stage '构建框架'
        Invoke-Checked -FilePath 'dotnet' -ArgumentList @('build', $solutionPath, '-c', $Configuration, '-nologo', '-p:GeneratePackageOnBuild=false') -LogName 'build-framework' | Out-Null
    }

    Write-Stage '打包框架到隔离源'
    Invoke-Checked -FilePath 'dotnet' -ArgumentList @('pack', $solutionPath, '-c', $Configuration, '-o', $feedDir, '-nologo', '--no-build') -LogName 'pack-framework' | Out-Null
    [xml] $versionProps = Get-Content (Join-Path $repoRoot 'framework' 'props' 'version.props')
    $frameworkVersion = @($versionProps.Project.PropertyGroup.Version)[0].Trim()
    Assert-True (Test-Path (Join-Path $feedDir "XiHan.Framework.Web.Api.$frameworkVersion.nupkg")) "隔离源中没有 $frameworkVersion 版框架包"

    Write-Stage '打包模板'
    Invoke-Checked -FilePath 'dotnet' -ArgumentList @('pack', $templateProject, '-c', $Configuration, '-o', $templatePackageDir, '-nologo') -LogName 'pack-templates' | Out-Null
    $templatePackage = Join-Path $templatePackageDir "XiHan.Framework.Templates.$frameworkVersion.nupkg"
    Assert-True (Test-Path $templatePackage) "未产出模板包 $templatePackage"

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($templatePackage)
    try {
        $entries = @($zip.Entries | ForEach-Object FullName)
        Assert-True (-not ($entries | Where-Object { $_ -match '(^|/)(bin|obj)/' })) '模板包含有 bin/obj'
        Assert-True (-not ($entries | Where-Object { $_ -match '\.(dll|pdb|db|user)$' })) '模板包含有二进制或本机文件'
        foreach ($entry in $zip.Entries | Where-Object { $_.FullName -like 'content/*/.template.config/template.json' }) {
            $reader = [System.IO.StreamReader]::new($entry.Open())
            try {
                $templateJson = $reader.ReadToEnd() | ConvertFrom-Json
            }
            finally {
                $reader.Dispose()
            }
            Assert-True ($templateJson.symbols.FrameworkVersion.defaultValue -eq $frameworkVersion) "$($entry.FullName) 的默认框架版本不是 $frameworkVersion"
        }
    }
    finally {
        $zip.Dispose()
    }

    Write-Stage '安装模板到隔离模板库'
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="xihan-local" value="$feedDir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="xihan-local">
      <package pattern="XiHan.Framework.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -Path (Join-Path $WorkRoot 'NuGet.Config') -Encoding utf8
    Invoke-Checked -FilePath 'dotnet' -ArgumentList @('new', 'install', $templatePackage, '--debug:custom-hive', $hiveDir) -LogName 'install' | Out-Null
    $installed = Invoke-Checked -FilePath 'dotnet' -ArgumentList @('new', 'list', 'xihan', '--debug:custom-hive', $hiveDir)
    Assert-True (($installed.Output -join "`n") -match 'xihan-web' -and ($installed.Output -join "`n") -match 'xihan-module') '隔离模板库中未找到 xihan-web / xihan-module'

    $webScenarios = @(
        @{ Scenario = 'web-minimal'; Options = @(); Data = $false; MultiTenancy = $false; Observability = $false; Tests = 1 },
        @{ Scenario = 'web-data'; Options = @('--data'); Data = $true; MultiTenancy = $false; Observability = $false; Tests = 3 },
        @{ Scenario = 'web-multi-tenancy'; Options = @('--multi-tenancy'); Data = $false; MultiTenancy = $true; Observability = $false; Tests = 5 },
        @{ Scenario = 'web-observability'; Options = @('--observability'); Data = $false; MultiTenancy = $false; Observability = $true; Tests = 2 },
        @{ Scenario = 'web-data-multi-tenancy'; Options = @('--data', '--multi-tenancy'); Data = $true; MultiTenancy = $true; Observability = $false; Tests = 8 },
        @{ Scenario = 'web-all'; Options = @('--data', '--multi-tenancy', '--observability'); Data = $true; MultiTenancy = $true; Observability = $true; Tests = 9 }
    )

    foreach ($web in $webScenarios) {
        Write-Stage "xihan-web：$($web.Scenario)"
        $name = 'MyCompany.Billing'
        $directory = Join-Path $outputRoot $web.Scenario
        New-TemplateOutput -Template 'xihan-web' -Name $name -Directory $directory -Options $web.Options
        Assert-True (Test-Path (Join-Path $directory 'src' $name 'BillingModule.cs')) '启动模块未按名称末段命名为 BillingModule'
        Assert-True ((Test-Path (Join-Path $directory 'src' $name 'Data')) -eq $web.Data) 'Data 目录与 --data 选项不一致'
        Assert-True ((Test-Path (Join-Path $directory 'src' $name 'MultiTenancy')) -eq $web.MultiTenancy) 'MultiTenancy 目录与 --multi-tenancy 选项不一致'
        $props = Get-Content (Join-Path $directory 'Directory.Build.props') -Raw
        Assert-True ($props -match "<XiHanFrameworkVersion>$([regex]::Escape($frameworkVersion))</XiHanFrameworkVersion>") '生成的框架版本与模板包版本不一致'
        $tests = Invoke-BuildAndTest -Directory $directory -Scenario $web.Scenario -ExpectedTests $web.Tests
        Test-WebSmoke -Directory $directory -Name $name -Scenario $web.Scenario -Data:$web.Data -MultiTenancy:$web.MultiTenancy -Observability:$web.Observability
        Assert-NoTemplateResidue $directory
        $script:Results.Add([pscustomobject]@{ Scenario = $web.Scenario; Tests = $tests; Result = '通过' })
    }

    Write-Stage 'xihan-module：MyCompany.Billing.Inventory'
    $moduleDirectory = Join-Path $outputRoot 'module'
    New-TemplateOutput -Template 'xihan-module' -Name 'MyCompany.Billing.Inventory' -Directory $moduleDirectory
    Assert-True (Test-Path (Join-Path $moduleDirectory 'src' 'MyCompany.Billing.Inventory' 'InventoryModule.cs')) '模块类未按名称末段命名为 InventoryModule'
    Assert-True (Test-Path (Join-Path $moduleDirectory 'src' 'MyCompany.Billing.Inventory' 'Extensions' 'DependencyInjection' 'InventoryServiceCollectionExtensions.cs')) '缺少服务注册扩展'
    Assert-True (Test-Path (Join-Path $moduleDirectory 'src' 'MyCompany.Billing.Inventory' 'README.md')) '缺少模块 README'
    $tests = Invoke-BuildAndTest -Directory $moduleDirectory -Scenario 'module' -ExpectedTests 3
    Assert-NoTemplateResidue $moduleDirectory
    $script:Results.Add([pscustomobject]@{ Scenario = 'module'; Tests = $tests; Result = '通过' })

    Write-Stage '模块加入 Web 解决方案'
    $hostDirectory = Join-Path $outputRoot 'web-observability'
    $moduleRelative = Join-Path 'modules' 'MyCompany.Billing.Inventory'
    New-TemplateOutput -Template 'xihan-module' -Name 'MyCompany.Billing.Inventory' -Directory (Join-Path $hostDirectory $moduleRelative)
    foreach ($project in @(
            (Join-Path $moduleRelative 'src' 'MyCompany.Billing.Inventory' 'MyCompany.Billing.Inventory.csproj'),
            (Join-Path $moduleRelative 'test' 'MyCompany.Billing.Inventory.Tests' 'MyCompany.Billing.Inventory.Tests.csproj'))) {
        Invoke-Checked -FilePath 'dotnet' -ArgumentList @('sln', 'MyCompany.Billing.slnx', 'add', $project) -WorkingDirectory $hostDirectory | Out-Null
    }
    $tests = Invoke-BuildAndTest -Directory $hostDirectory -Scenario 'module-in-solution' -ExpectedTests 5
    Assert-NoTemplateResidue $hostDirectory
    $script:Results.Add([pscustomobject]@{ Scenario = 'module-in-solution'; Tests = $tests; Result = '通过' })

    Write-Stage '名称含非法标识符字符'
    $oddDirectory = Join-Path $outputRoot 'odd-name'
    New-TemplateOutput -Template 'xihan-web' -Name '1st-billing' -Directory $oddDirectory
    $oddModule = Join-Path $oddDirectory 'src' '_1st_billing' '_1st_billingModule.cs'
    Assert-True (Test-Path $oddModule) '非法标识符字符未被规整为合法类名'
    Assert-True ((Get-Content $oddModule -Raw) -match 'namespace _1st_billing;') '非法标识符字符未被规整为合法命名空间'
    $tests = Invoke-BuildAndTest -Directory $oddDirectory -Scenario 'odd-name' -ExpectedTests 1
    Assert-NoTemplateResidue $oddDirectory
    $script:Results.Add([pscustomobject]@{ Scenario = 'odd-name'; Tests = $tests; Result = '通过' })

    Write-Stage '拒绝覆盖已有文件'
    $existingDirectory = Join-Path $outputRoot 'web-minimal'
    $programPath = Join-Path $existingDirectory 'src' 'MyCompany.Billing' 'Program.cs'
    Set-Content -Path $programPath -Value '// 用户修改' -Encoding utf8
    $before = (Get-FileHash $programPath).Hash
    $overwrite = Invoke-Checked -FilePath 'dotnet' -ArgumentList @('new', 'xihan-web', '-n', 'MyCompany.Billing', '-o', $existingDirectory, '--debug:custom-hive', $hiveDir) -AllowAnyExitCode -LogName 'new-overwrite'
    Assert-True ($overwrite.ExitCode -ne 0) '向已有目录生成未被拒绝'
    Assert-True ((Get-FileHash $programPath).Hash -eq $before) '已有文件被覆盖'
    $script:Results.Add([pscustomobject]@{ Scenario = 'no-overwrite'; Tests = 0; Result = "通过（退出码 $($overwrite.ExitCode)）" })

    Write-Stage '引用不存在的框架版本'
    $missingDirectory = Join-Path $outputRoot 'missing-version'
    New-TemplateOutput -Template 'xihan-module' -Name 'MyCompany.Missing' -Directory $missingDirectory -Options @('--framework-version', '0.0.1-missing')
    $restore = Invoke-Checked -FilePath 'dotnet' -ArgumentList @('restore') -WorkingDirectory $missingDirectory -AllowAnyExitCode -LogName 'restore-missing-version'
    $restoreOutput = $restore.Output -join "`n"
    Assert-True ($restore.ExitCode -ne 0) '引用不存在的框架版本时还原未失败'
    Assert-True ($restoreOutput -match 'NU110[12]' -and $restoreOutput -match 'xihan-local') "还原失败信息未指出缺失的包与包源：`n$restoreOutput"
    $script:Results.Add([pscustomobject]@{ Scenario = 'missing-version'; Tests = 0; Result = "通过（退出码 $($restore.ExitCode)）" })

    Write-Stage '更新模板包'
    $updateDirectory = Join-Path $WorkRoot 'template-package-update'
    $updateVersion = "$frameworkVersion-update.1"
    Invoke-Checked -FilePath 'dotnet' -ArgumentList @('pack', $templateProject, '-c', $Configuration, '-o', $updateDirectory, '-nologo', "-p:Version=$updateVersion") -LogName 'pack-templates-update' | Out-Null
    Invoke-Checked -FilePath 'dotnet' -ArgumentList @('new', 'uninstall', 'XiHan.Framework.Templates', '--debug:custom-hive', $hiveDir) -LogName 'uninstall-before-update' | Out-Null
    Invoke-Checked -FilePath 'dotnet' -ArgumentList @('new', 'install', (Join-Path $updateDirectory "XiHan.Framework.Templates.$updateVersion.nupkg"), '--debug:custom-hive', $hiveDir) -LogName 'install-update' | Out-Null
    $installedPackages = Invoke-Checked -FilePath 'dotnet' -ArgumentList @('new', 'uninstall', '--debug:custom-hive', $hiveDir)
    $installedText = $installedPackages.Output -join "`n"
    Assert-True ($installedText -match [regex]::Escape($updateVersion)) "更新后模板包版本不是 $updateVersion"
    $updatedDirectory = Join-Path $outputRoot 'module-updated'
    New-TemplateOutput -Template 'xihan-module' -Name 'MyCompany.Updated' -Directory $updatedDirectory
    $updatedProject = Get-Content (Join-Path $updatedDirectory 'src' 'MyCompany.Updated' 'MyCompany.Updated.csproj') -Raw
    Assert-True ($updatedProject -match ">$([regex]::Escape($updateVersion))<") "更新后生成的项目未引用 $updateVersion"
    Assert-True ($installedText -notmatch "(?m)^\s*Version:\s*$([regex]::Escape($frameworkVersion))\s*`$") '更新后旧版本模板包仍在'
    $script:Results.Add([pscustomobject]@{ Scenario = 'update'; Tests = 0; Result = "通过（$updateVersion）" })

    Write-Stage '卸载模板并核对用户模板库'
    Invoke-Checked -FilePath 'dotnet' -ArgumentList @('new', 'uninstall', 'XiHan.Framework.Templates', '--debug:custom-hive', $hiveDir) -LogName 'uninstall' | Out-Null
    $afterUninstall = Invoke-Checked -FilePath 'dotnet' -ArgumentList @('new', 'list', 'xihan', '--debug:custom-hive', $hiveDir) -AllowedExitCodes @(0, 103)
    Assert-True (-not (($afterUninstall.Output -join "`n") -match 'xihan-web|xihan-module')) '卸载后隔离模板库仍有模板'
    Assert-True ((Get-UserTemplateList) -eq $userTemplatesBefore) '用户模板库被改动'

    $succeeded = $true
}
finally {
    Write-Host ''
    Write-Host '==== 结果 ====' -ForegroundColor Cyan
    $script:Results | Format-Table -AutoSize | Out-String | Write-Host
    Write-Host "工作目录：$WorkRoot"
    if ($succeeded -and -not $KeepWorkRoot) {
        Remove-Item -Recurse -Force $WorkRoot -ErrorAction SilentlyContinue
        if (Test-Path $WorkRoot) {
            Write-Warning "工作目录未能完全删除：$WorkRoot"
        }
    }
}

Write-Host "全部场景通过（$($script:Results.Count) 项）。" -ForegroundColor Green
exit 0
