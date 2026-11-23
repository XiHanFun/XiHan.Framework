# 测试、文档与发布

## 标准验证

在仓库根执行：

```bash
dotnet restore framework/XiHan.Framework.slnx
dotnet build framework/XiHan.Framework.slnx -c Release --no-restore
dotnet test --solution framework/XiHan.Framework.slnx --configuration Release --no-build -p:TestingPlatformCommandLineArguments="--coverage --coverage-output-format cobertura"
```

- `global.json` 使用 Microsoft.Testing.Platform；测试命令必须保留 MTP 形式。
- 先跑受影响测试项目，再跑完整解决方案。
- CI 工作流是覆盖率产物位置、阈值和程序集数量的事实源；不得降低门槛掩盖缺陷。
- 系统明确报告资源不足时，可临时加 `/m:4 /nr:false` 降低 MSBuild 并发，并在结果中说明。

## CI 的测试分片

CI 的作业并行，总时长取最慢的一个：`build`（带全部分析器的构建、打包、SBOM）、`test-core` / `test-integration` / `test-database` / `test-oracle`（测试分片）、`aot`（AOT 冒烟与依赖漏洞清单）、`benchmarks`（性能门禁）。

- 分片表是 `framework/test/shards.json`：只列要外部服务或工具链的测试项目，每片写明作业设置的门控环境变量（`variables`）与安装的工具链（`tools`）；没列的测试项目归 `core`，不带服务。
- 各测试作业经 `.github/actions/test-shard` 只构建自己那一片（`-p:RunAnalyzers=false`，分析器门禁在 `build` 作业）再测试。
- 新测试项目不用登记，自动进 `core`。用了 `XIHAN_TEST_*` 门控变量或要 Go 的测试项目必须列进提供它的分片，否则用例在 CI 上静默跳过；架构测试 `TestShardTests` 把这种情况变成失败。新增服务时同时改分片表的 `variables` 与 `ci.yml` 里那个作业的 `services`、`env`。
- 本机跑一片：

```bash
pwsh -File framework/scripts/New-TestShard.ps1 -Shard database   # 输出 framework/artifacts/shards/database.slnf
dotnet build framework/artifacts/shards/database.slnf -c Release
dotnet test --solution framework/artifacts/shards/database.slnf --configuration Release --no-build
```

## 测试质量

- 单元测试覆盖纯逻辑、边界和失败语义。
- 集成测试覆盖能力解析、管道次序、DI、配置、生命周期、序列化和 Provider 接线；默认实现与 Provider 跑所属子系统的一致性套件。
- 并发测试验证可观察不变量，不依赖脆弱的固定延时。
- 修复回归先保留失败用例，再实现修复。
- 不删除断言、忽略失败或扩大超时来制造绿色结果。

## 文档

文档变更在 `docs/` 使用当前包管理器配置：

```bash
pnpm --dir docs install --frozen-lockfile
pnpm --dir docs build
```

公开 API、配置、默认值或生命周期变化必须同步 XML 文档、包 README、示例和文档站。示例只保留当前推荐路径。

## 包与发布

- `framework/Directory.Build.props` 与 `framework/src/Directory.Build.props` 管理包元数据和输出，包在 `framework/artifacts/packages/`。
- 发布前核对 `.github/workflows/ci.yml`、`.github/workflows/release.yml` 和实际生成包清单。
- NuGet unlist、标签、推送和发布会改变外部状态，只在用户明确要求时执行。
- 不手工修改包或构建产物；修正源项目和 props 后重新生成。
- 一个版本内的契约、实现、测试、文档和包元数据必须一致。
