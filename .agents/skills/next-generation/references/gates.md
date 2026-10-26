# 门禁与阶段

## 命令（仓库根）

```bash
dotnet restore next/XiHan.Framework.Next.slnx
dotnet build next/XiHan.Framework.Next.slnx -c Release --no-restore
dotnet test --solution next/XiHan.Framework.Next.slnx --configuration Release --no-build
dotnet pack next/XiHan.Framework.Next.slnx -c Release --no-build
dotnet publish next/samples/SimpleApp/SimpleApp.csproj -c Release -p:PublishAot=true   # 需要本机 C++ 工具链
dotnet run -c Release --project next/benchmarks/XiHan.Framework.Benchmarks -- --filter '*'
```

`global.json` 启用了 Microsoft.Testing.Platform：`dotnet test` 必须用 `--solution`，不要改回 VSTest。CI 同形流程在 `.github/workflows/next-ci.yml`。

## 门禁清单

| 门禁 | 位置 | 失败时 |
| --- | --- | --- |
| 警告即错误、`latest-recommended` 分析器、Nullable | `next/Directory.Build.props` | 修代码，不加 `NoWarn`（测试、示例、基准项目仅允许 CA1515、CA1707、xUnit1051、CS1591） |
| 构建期代码样式 `EnforceCodeStyleInBuild`：IDE1006 命名（私有字段 `_camelCase`、私有常量 PascalCase）、IDE0005 未使用 using | `next/Directory.Build.props`、`next/.editorconfig` | 改名、删 using；批量清理 `dotnet format style next/XiHan.Framework.Next.slnx --diagnostics IDE0005 --severity warn`；增量构建可能不重编译，验证时加 `--no-incremental` |
| XML 文档 `GenerateDocumentationFile`（CS1591 在 src 为错误） | `src/Directory.Build.props` | 给全部成员含私有补中文 `<summary>` |
| NU1903/NU1904 漏洞 | 同上 | 升级依赖 |
| PublicAPI 文件（RS00xx） | `src/Directory.Build.props` | 运行 `pwsh -File next/scripts/Sync-PublicApi.ps1 -Project <csproj>` 同步 `PublicAPI.Unshipped.txt`；已发布行不改 |
| 包验证 `EnablePackageValidation` | 同上 | 小版本不得破坏；大版本列出破坏清单 |
| AOT 分析器 `IsAotCompatible` | 同上 | 去掉反射与动态代码 |
| 自研分析器 XH0001–XH0005 | `src/XiHan.Framework.Kernel.Analyzers`，经 `src/Directory.Build.props` 引用 | 补 `[ApiLevel]`、改名、去反射；不加 `NoWarn` |
| 架构测试 | `test/XiHan.Framework.Architecture.Tests` | 改名、改依赖、改命名空间；不改测试放宽 |
| 新正式包 | 同上 `EverySourceProject_IsReferencedByArchitectureTests` | 把新包加进架构测试项目引用 |

架构测试覆盖：包 ID 文法、目录名等于包 ID、命名空间等于包 ID 或其下一层、公开类型不带 `XiHan` 前缀、一个源文件一个顶层类型且文件名等于类型名（泛型加 `OfT`，`FileNamingTests`）、批量优先（循环体内不调单条写入与发布，`BatchFirstTests`）、依赖只向下或同环契约包、`Primitives` 无引用、Ring 1/2 只引用 `Microsoft.Extensions.*` 抽象包（允许名单另有 `Microsoft.Extensions.Options`、`Microsoft.Extensions.Primitives`、`System.Threading.RateLimiting`）、公开签名不泄漏第三方类型、Ring 0/1 零反射、版权头、README 与 PublicAPI 文件存在、术语表自洽、本地化约定（`LocalizationTests`：带 `Localization/` 的包每种内置区域一个文件且键集一致、占位符与 `en-US` 一致、键首段是术语表领域词、文件以 `LogicalName="Localization/%(Filename)%(Extension)"` 嵌入；`<领域>Messages` 的 `const string` 与 `en-US.json` 的键一一对应且全局唯一；正式包里 `Error.*` 的第二个参数必须是常量，`Error.Literal` 只允许在 `FrameworkException.cs` 与 `IdempotencyRecord.cs`）。

## 新建正式包的步骤

1. 用术语表与文法确定包 ID；目录 `next/src/<包 ID>/`。
2. 放入 `<包 ID>.csproj`（`PackageId`、`RootNamespace`、`Description`、`PackageTags`）、`README.md`（定位、责任边界、依赖方向、不负责的内容、状态）、`PublicAPI.Shipped.txt` 与 `PublicAPI.Unshipped.txt`（首行 `#nullable enable`）。
3. 加进 `XiHan.Framework.Next.slnx` 对应环层目录，加进架构测试项目的引用。
4. 若引入新能力 ID 或错误码，登记到 `next/spec/conventions/`。
5. 有面向人的文案时：`<领域>Messages` 加键常量，`Localization/<区域>.json` 10 个文件各加一行，csproj 加 `<EmbeddedResource Include="Localization/*.json" LogicalName="Localization/%(Filename)%(Extension)" />`，Ring ≥2 的包用 `MessageCatalog.FromEmbeddedResources(名, typeof(<领域>Messages))` 暴露 `Catalog`，Provider 的 `Configure` 第一行 `AddMessageCatalog`。
6. 同步 PublicAPI：`pwsh -NoProfile -File next/scripts/Sync-PublicApi.ps1 -Project <csproj>`，按依赖顺序执行（被依赖项编译失败时只会写出一行）。

## 阶段退出条件

| 阶段 | 退出条件 |
| --- | --- |
| P0 定边界 | 8 项 ADR 通过；空解决方案过全部门禁；RFC-0001 列出原语层全部类型 |
| P1 内核 | 零 Provider 的 SimpleApp 可运行；AOT 发布通过；内核公开类型 ≤ 120、第三方依赖 0、启动 < 50 ms、空应用 < 30 MB |
| P2 子系统 | Standard Profile 应用零外部基础设施运行；切换 Provider 只改配置与包引用；默认实现与 Provider 全过一致性套件 |
| P3 协议与宿主 | 一份契约在 HTTP、gRPC、MCP 上服务；k8s 示例带探针与优雅停机 |
| P4 插件与跨语言 | Go 插件在 .NET 宿主中服务一个操作；Go SDK 通过全部一致性向量；Spec 仓库公开 |
| P5 GA | BasicApp 全量跑在 5.0；4.x 进入维护期 |

详细任务清单在 `nextdocs/architecture/05-开发计划.md`。
