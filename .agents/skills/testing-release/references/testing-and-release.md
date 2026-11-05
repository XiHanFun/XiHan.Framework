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
