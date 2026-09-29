---
name: xihan-framework-testing-release
description: 测试、覆盖率、文档构建、版本升级、NuGet 打包或发布 XiHan.Framework 时使用。适用于 Microsoft.Testing.Platform 命令、CI 门禁、包清单和发布流程；不负责实现普通模块功能。
---

# XiHan.Framework 测试与发布

先读 `references/testing-and-release.md`，再以当前 `.github/workflows/ci.yml`、`.github/workflows/release.yml` 和 `framework/props` 为事实源。

## 标准验证

```bash
dotnet restore framework/XiHan.Framework.slnx
dotnet build framework/XiHan.Framework.slnx -c Release --no-restore -p:GeneratePackageOnBuild=false
dotnet test --solution framework/XiHan.Framework.slnx --configuration Release --no-build -p:TestingPlatformCommandLineArguments="--coverage --coverage-output-format cobertura"
```

- 测试由 Microsoft.Testing.Platform 驱动，不改回 VSTest 形式。
- 先跑受影响测试，再跑完整解决方案；不删除断言或降低 CI 阈值制造通过。
- 公开 API、默认值、配置和生命周期变化同步 XML 文档、README、示例与文档站。
- 日常构建关闭 `GeneratePackageOnBuild`，不手改 `nupkgs` 或其他生成物。
- VersionUpgrade、NuGet unlist、标签、推送和发布会改变外部状态，只有用户明确要求时执行。

发布前核对 Release 构建、MTP 测试、版本、包数量、包内容和工作流状态；失败时停止发布并保留诊断证据。
