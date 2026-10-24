# XiHan.Framework.Cli

命令行工具 `xihan`（.NET 工具包，RFC-0016）。

```bash
dotnet tool install --global XiHan.Framework.Cli
xihan pack                                   # 读 ./xihan.app.json，写到 ./deploy/
xihan pack --manifest src/Orders/xihan.app.json --output deploy/orders --only dockerfile,kustomize
xihan graph --project src/Orders             # 转发给应用：dotnet run --project src/Orders -- graph
xihan orders create --name widget --amount 3 # 转发 invoke 简写
xihan migrate v4 --path backend/src --dry-run --report migrate.json   # 先看报告
xihan migrate v4 --path backend/src --fixers V4-01,V4-03              # 只跑部分规则
xihan version
```

| 命令 | 说明 |
| --- | --- |
| `pack` | 从 `xihan.app.json`（规范：`spec/packaging/app-manifest.md`）生成 `Dockerfile` + `Dockerfile.dockerignore`、`kustomize/`（Deployment、Service、ConfigMap、HPA、PDB、Lease 的 RBAC、Secret 示例、kustomization）、`compose.yaml`、`aspire/XiHanAppHost.g.cs`；逐行输出写出的文件 |
| `migrate v4` | 按 `spec/migration/v4.md` 把 4.x 代码改写成 5.0（语法级 Roslyn 重写，不加载 MSBuild 工作区）：`--path` 目录、`.csproj` 或 `.slnx`（默认当前目录），`--fixers` 选规则子集，`--dry-run` 只报告不写文件，`--report` 把 JSON 报告写到文件。规则按编号顺序执行：V4-01 `XiHanModule` → `IFeature` / `IHostedFeature`（清单、钩子合并、`[DependsOn]` → `Requires`、文件改名；被删 4.x 模块在 `ConfigureServices` 里做过的注册按模块在 `Configure` 末尾留带清单的标记，应用已有同名承接扩展方法时直接生成调用）、V4-02 实体基类 → 协议接口（`BasicId` → `Id` 等改名含记录位置参数等声明、`RowVersion` / `TenantId` 改字符串、`Sugar*` 基类展开）、V4-03 注入标记 → `<项目末段>V4Services.cs` 显式注册（调用插进同项目特性的 `Configure` 开头，已有调用不重复；自己没有标记、沿基类链继承到标记或 4.x 框架基类如 `ApplicationServiceBase` 的具体类，按继承到的存期 `TryAdd` 类本身并把类名以「接口名去 I」结尾的接口转发到它）、V4-04 `IClock` → `TimeProvider`、V4-05 删 `[UnitOfWork]` 并标记 `IUnitOfWorkManager`、V4-06 `[DynamicApi]` 服务类（含经基类继承的）的每个公开实例方法生成一个委托给原服务的操作契约（`Operations/<资源>/<方法>Operation.cs`、请求记录、每项目 `EmptyResponse` 与 `<项目末段>OperationsJsonContext`（生成的类型写全限定名、简单名重复的加 `TypeInfoPropertyName`；接口类型的返回值包 `Result.Success`）、项目文件加 `Kernel.Generators` 引用；`[PermissionAuthorize]` 原样进 `Permission`，`[Authorize]` → `Policy = "authenticated"`）、V4-07 `ICurrentUser` / `ICurrentTenant` 的调用点不改，在每条项目引用链最底层的使用项目生成同名应用接口与读 `IInvocationAccessor.Current` 的 `CurrentUser` / `CurrentTenant`（`Security/` 目录），使用文件补 `using`，`UserId` 仍是 `long?` 并加 `Subject`，`CurrentTenant.Change(...)` 用 AsyncLocal 覆盖栈实现、V4-08 聚合根里的 `AddLocalEvent` / `AddDistributedEvent` → `Raise`（任何文件），`ILocalEventHandler<T>` → `IMessageHandler`（每个事件一个主题常量，生成按主题解码并转调保留的 `HandleEventAsync` 的 `HandleAsync`，按主题进 `V4Services`）与 `ILocalEventBus.PublishAsync(evt)` → `IMessagePublisher.PublishAsync(主题, 信封, ct)`、V4-09 `Localization/` 下 4.x 的 `{ resource, culture, texts }` 资源文件 → 每区域一个 `Localization/<区域>.json`（平铺键 `<领域>.<资源>.<键>`，原文件删除，项目文件补嵌入项）、V4-10 包引用（含指向 4.x 框架项目的 `ProjectReference`）与 4.x `using` 清理。改不了的位置留 `// xihan-migrate(<规则>): …` 标记；再跑一遍零改动。跳过 `bin/`、`obj/`、`.git/`、`*.g.cs`、`*.Designer.cs` |
| `graph` / `pipeline` / `operations` / `invoke` | 转发给应用自己的命令行宿主（`Hosting.CommandLine`）：`dotnet run --project <project> --configuration <c> -- <命令> …`；项目取 `--project`，否则取当前目录清单的 `project` |
| `version` | 框架版本 |
| `help` | 命令列表 |

退出码沿用命令行宿主：成功 0，清单不存在 3（`CLI_NOTFOUND_001`），清单不合法 2（`CLI_VALIDATION_001`，`detail` 列出全部问题），用法错误 64，转发命令原样返回子进程退出码；`migrate` 的路径不存在 3（`CLI_NOTFOUND_002`），来源不是 `v4` 或 `--fixers` 有不认识的编号 64（`CLI_VALIDATION_002` / `CLI_VALIDATION_003`）。文案按 `--locale` 或当前 UI 文化；`migrate` 留下的标记与生成代码里的文档注释也按这个区域。

## 依赖方向

工具链；依赖 `Hosting.CommandLine`（解析、帮助、失败输出、退出码）、`Localization.InMemory`（文案）与 `Microsoft.CodeAnalysis.CSharp`（`migrate` 的语法树）。

## 状态

P3：`pack` 与转发命令可用。P5-02：`migrate v4` 的 V4-01 ~ V4-10 全部可用（在 BasicApp 的 1695 个文件上数秒跑完，再跑一遍零改动）。离线知识库与 MCP 服务端后续。
