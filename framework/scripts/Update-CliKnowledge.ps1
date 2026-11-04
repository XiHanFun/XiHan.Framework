<#
.SYNOPSIS
    重新生成 xihan 工具离线知识库的包与特性清单索引 framework/src/XiHan.Framework.Cli/Knowledge/catalog.json

.DESCRIPTION
    构建 XiHan.Framework.Cli.Tests（它引用全部正式包），以 XIHAN_UPDATE_CLI_KNOWLEDGE=1 运行一次测试：
    KnowledgeCatalogTests 从 framework/src/*/*.csproj、framework/Directory.Packages.props、framework/README.md 的包表与各特性的 Manifest 生成索引并写回文件
    之后再构建一次让工具嵌入新的索引，并不带变量重跑测试核对
    配置节索引 Knowledge/configuration.json 是人工整理的，不由本脚本生成；KnowledgeConfigurationTests 失败时按失败信息逐条改它（ADR-0076）

.EXAMPLE
    pwsh -File framework/scripts/Update-CliKnowledge.ps1
#>

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$framework = Split-Path -Parent $PSScriptRoot
$project = Join-Path $framework 'test/XiHan.Framework.Cli.Tests/XiHan.Framework.Cli.Tests.csproj'

& dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# 生成：只有清单测试会因变量改写文件，其余测试照常运行，失败也不影响这一步
$env:XIHAN_UPDATE_CLI_KNOWLEDGE = '1'
try {
    & dotnet test --project $project -c Release --no-build | Out-Null
}
finally {
    Remove-Item Env:XIHAN_UPDATE_CLI_KNOWLEDGE
}

# 工具把索引作为嵌入资源打进程序集：重新构建后再核对一遍
& dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& dotnet test --project $project -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Tests failed after regenerating the catalog; fix Knowledge/configuration.json by the reported differences.' }
