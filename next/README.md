# XiHan Framework 5（next 工作区）

下一代框架的 .NET 参考实现工作区。设计文档在仓库根的 [`nextdocs/`](../nextdocs/下一代框架总体方案.md)，规范草案在本目录 `spec/`。

## 布局

```text
next/
├─ XiHan.Framework.Next.slnx        # 解决方案目录按环层分组
├─ Directory.Build.props            # 版本线 5.x、Nullable、警告即错误、文档文件
├─ Directory.Packages.props         # 中央包版本，Ring 0/1 只允许 Microsoft.Extensions.* 抽象包
├─ spec/                            # XiHan Spec：RFC、术语表、清单 Schema、命名约定
├─ scripts/                         # Sync-PublicApi.ps1 等维护脚本
├─ src/                             # 正式包，物理目录扁平，包名由术语表 + 文法推出
├─ test/                            # 单元、架构、集成测试
├─ samples/                         # SimpleApp（Standard 组合、Native AOT 冒烟）、SamplePlugin（进程内插件）
└─ incubator/                       # 实验包，只能标 Experimental
```

## 正式包

| 包 | 环 | 内容 |
| --- | --- | --- |
| `XiHan.Framework.Primitives` | 0 | 原语与契约，只增不改 |
| `XiHan.Framework.Kernel` | 1 | 宿主、能力图谱、管道、诊断薄桥、安全原语、插件抽象、操作契约与 XGI 调用器 |
| `XiHan.Framework.Domain` | 2 | 实体协议、值对象、领域事件缓冲、规约；纯契约 |
| `XiHan.Framework.Localization.Abstractions` | 2 | 区域名、内置 10 种区域（默认 `en-US`）、消息目录（每种语言一个文件）、显式区域的本地化接口；消息键、模板与 `key.L()` 在原语层 |
| `XiHan.Framework.Persistence.Abstractions` | 2 | 仓储、工作单元、查询、标识生成、Transaction 阶段中间件、实体登记 |
| `XiHan.Framework.Caching.Abstractions` | 2 | 字节存储、类型化缓存、序列化器、分布式锁、Idempotency 阶段中间件 |
| `XiHan.Framework.MultiTenancy.Abstractions` | 2 | 租户信息与存储、租户解析器、Tenant 阶段中间件 |
| `XiHan.Framework.Security.Abstractions` | 2 | 凭据与认证处理器、授权要求与策略目录、RBAC 授予，Authenticate / Authorize 阶段中间件 |
| `XiHan.Framework.Messaging.Abstractions` | 2 | CloudEvents 信封、代理与发布者、处理器订阅、经管道的派发、事务性发件箱 |
| `XiHan.Framework.Localization.InMemory` | 3 | 内存本地化默认实现：汇集各包目录，精确区域 → 同语言 → 默认区域 → `en-US` 回退，设进程默认消息作用域 |
| `XiHan.Framework.Persistence.InMemory` | 3 | 内存持久化默认实现（`role: default`）：有界、读己之写、软删除与租户隔离 |
| `XiHan.Framework.Persistence.EntityFrameworkCore` | 3 | EF Core Provider：应用自有 DbContext、事务型工作单元、协议约定（非 AOT） |
| `XiHan.Framework.Caching.InMemory` | 3 | 内存缓存默认实现：有界 LRU、绝对与滑动过期，兼任单进程共享缓存 |
| `XiHan.Framework.Caching.Redis` | 3 | Redis Provider：共享缓存、分布式锁、幂等（非 AOT） |
| `XiHan.Framework.MultiTenancy.InMemory` | 3 | 内存租户存储默认实现：从配置装载、有界 |
| `XiHan.Framework.Security.InMemory` | 3 | 内存安全默认实现：ApiKey 主体与 RBAC 授予，从配置装载 |
| `XiHan.Framework.Security.Jwt` | 3 | JWT Bearer 认证 Provider（非 AOT） |
| `XiHan.Framework.Messaging.InMemory` | 3 | 内存消息代理默认实现：有界通道、顺序投递、重试与死信 |
| `XiHan.Framework.Messaging.RabbitMQ` | 3 | RabbitMQ Provider：持久队列、手动确认、重试与死信队列（非 AOT） |
| `XiHan.Framework.Hosting.AspNetCore` | 4 | HTTP 请求桥接、`Accept-Language` 协商、探针端点、本地化的 Problem Details |
| `XiHan.Framework.Protocols.Http` | 5 | HTTP 协议适配器：操作 ID 推导路由（可覆盖）、路由值/请求体/查询串绑定为 XGI 载荷、`MapXiHanOperations()` |
| `XiHan.Framework.Plugins.InProcess` | 5 | 可收集 AssemblyLoadContext 的插件宿主（非 AOT） |
| `XiHan.Framework.Profiles.Minimal` | 6 | 组合包：原语层 + 内核 + ASP.NET Core 宿主 + 内存本地化 |
| `XiHan.Framework.Profiles.Standard` | 6 | 组合包：Minimal + Domain + 五个子系统的抽象与内存默认实现，`UseStandardProfile()` |
| `XiHan.Framework.Kernel.Analyzers` | 工具链 | Roslyn 分析器 XH0001–XH0005：ApiLevel、命名、Internal 引用、零反射 |
| `XiHan.Framework.Testing` | 工具链 | 测试宿主、特性桩、清单与上下文构造 |
| `XiHan.Framework.Persistence.Conformance` | 工具链 | `persistence.relational` Provider 必须通过的 xUnit 套件 |
| `XiHan.Framework.Caching.Conformance` | 工具链 | `ICacheStore` 实现必须通过的 xUnit 套件 |
| `XiHan.Framework.MultiTenancy.Conformance` | 工具链 | `ITenantStore` 实现必须通过的 xUnit 套件 |
| `XiHan.Framework.Security.Conformance` | 工具链 | `IPermissionGrantStore` 实现必须通过的 xUnit 套件 |
| `XiHan.Framework.Messaging.Conformance` | 工具链 | `messaging.queue` Provider 必须通过的 xUnit 套件 |

基准在 `benchmarks/XiHan.Framework.Benchmarks`，本地运行：`dotnet run -c Release --project next/benchmarks/XiHan.Framework.Benchmarks -- --filter '*'`。

## 示例

`samples/SimpleApp` 用 Standard 组合零外部基础设施运行两个操作契约（`orders.create` → `POST /orders`、MCP tool `orders_create` 与 gRPC `Orders/Create`，`orders.list` → `GET /orders`、`orders_list` 与 `Orders/List`，带租户、鉴权、事务与消息；HTTP 在 5000，gRPC（h2c）在 5001；配置里的开发用 API Key 见 `appsettings.json`）：

```bash
dotnet run --project next/samples/SimpleApp
curl -s localhost:5000/whoami -H "Authorization: ApiKey dev-alice-key"
curl -s -X POST localhost:5000/orders -H "Authorization: ApiKey dev-alice-key" -H "Content-Type: application/json" -d '{"name":"widget","amount":3}'
curl -s localhost:5000/orders -H "Authorization: ApiKey dev-bob-key"          # viewer 只能读
curl -s -X POST localhost:5000/orders -H "Authorization: ApiKey dev-bob-key" -H "Content-Type: application/json" -d '{"name":"x","amount":1}'   # 403 SECURITY_DENIED_004
curl -s localhost:5000/orders/stats -H "Authorization: ApiKey dev-alice-key"  # 发件箱投递后的消息处理计数
curl -s localhost:5000/missing -H "Accept-Language: zh-CN"                     # Problem Details 的 detail 按 Accept-Language 翻译，默认英文
curl -s localhost:5000/openapi.json                                             # 从操作契约与生成的 Schema 发布的 OpenAPI 3.1
curl -s -X POST localhost:5000/mcp -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'   # 同一份契约作为 MCP tool，描述取自消息目录
curl -s -X POST localhost:5000/mcp -H "Content-Type: application/json" -H "Authorization: ApiKey dev-alice-key" -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"orders_create","arguments":{"name":"widget","amount":3}}}'
curl -s localhost:5000/operations.proto                                         # 从同一份 Schema 生成的 .proto；gRPC 调用示例：grpcurl -plaintext -proto operations.proto -H "authorization: ApiKey dev-alice-key" localhost:5001 Orders/List
curl -s -o /dev/null -w "%{http_code}\n" -X POST localhost:5000/events -H "ce-specversion: 1.0" -H "ce-id: evt-1" -H "ce-source: /external" -H "ce-type: orders.placed" -H "ce-xihantenant: acme" -H "Content-Type: application/json" -d '{"id":"evt-1"}'   # 入站 CloudEvent（binary 模式）→ 主题 orders → OrderPlacedHandler，202；再看 /orders/stats
```

`samples/SimpleCli` 用 Minimal 组合把同一份契约放到命令行与 MCP stdio 上：

```bash
dotnet run --project next/samples/SimpleCli -- help                              # 用法与命令列表
dotnet run --project next/samples/SimpleCli -- operations                        # greetings.create  Experimental  Mcp, CommandLine  -
dotnet run --project next/samples/SimpleCli -- greetings create --name Ada --times 2   # {"message":"Hello, Ada! Hello, Ada!","traceId":"…"}
dotnet run --project next/samples/SimpleCli -- invoke greetings.create --json '{"name":"Ada","times":99}'; echo $?   # stderr 为 Problem JSON，退出码 2
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project next/samples/SimpleCli -- mcp   # MCP stdio：tool greetings_create 带本地化描述
```

文案不写在代码里：`SampleMessages` 只定义键，`Localization/<区域>.json` 每种语言一个文件，端点里 `SampleMessages.ResourceMissing.L()` 得到当前请求语言的文案，`Error.NotFound(code, SampleMessages.ResourceMissing)` 由宿主在 Egress 翻译（RFC-0010）。

换 Provider 只加包与配置：再 `UseFeature<EntityFrameworkCorePersistenceFeature<AppDbContext>>()`、`UseFeature<RedisCachingFeature>()`、`UseFeature<JwtSecurityFeature>()` 或 `UseFeature<RabbitMQMessagingFeature>()`，对应的内存默认实现自动让位。

## 命令

在仓库根执行：

```bash
dotnet restore next/XiHan.Framework.Next.slnx
dotnet build next/XiHan.Framework.Next.slnx -c Release --no-restore
dotnet test --solution next/XiHan.Framework.Next.slnx --configuration Release --no-build
dotnet pack next/XiHan.Framework.Next.slnx -c Release --no-build
```

`global.json` 已启用 Microsoft.Testing.Platform，`dotnet test` 必须用 `--solution`。

## 门禁

| 门禁 | 位置 |
| --- | --- |
| 警告即错误、Nullable、NU1903/NU1904 | `Directory.Build.props` |
| PublicAPI 文件、包验证、AOT 分析器 | `src/Directory.Build.props` |
| 依赖方向、包 ID 文法、命名空间、零反射、第三方类型泄漏、版权头 | `test/XiHan.Framework.Architecture.Tests` |
| CI | `.github/workflows/next-ci.yml` |

## 阶段

P1 内核已完成：原语层、内核、ASP.NET Core 宿主、进程内插件宿主、测试基座、分析器、示例与基准；`Kernel.Generators` 与 `Cli` 顺延到 P3 随操作契约一起实现。P2 子系统已完成：`Domain` 与 `Persistence`（抽象、内存默认实现、EF Core Provider、一致性套件，RFC-0005）、`Caching`（抽象、内存默认实现、Redis Provider、一致性套件、幂等中间件，RFC-0006）、`MultiTenancy`（抽象、内存默认实现、一致性套件、Tenant 阶段中间件，RFC-0007）、`Security`（抽象、内存默认实现、JWT Provider、一致性套件、Authenticate / Authorize 阶段中间件，RFC-0008）、`Messaging`（抽象、内存默认实现、RabbitMQ Provider、一致性套件、CloudEvents 信封、发件箱，RFC-0009）、`Localization`（原语层消息键、模板、`key.L()` 与 `MessageScope`；抽象与内存默认实现；默认 `en-US` 与内置 10 种区域、每包每种语言一个文件、宿主 `Accept-Language` 协商与 Problem Details 本地化，RFC-0010）、`Profiles.Minimal` / `Profiles.Standard` 与 Standard 组合示例。退出条件已满足：全部默认实现与 Provider 通过一致性套件（Redis、RabbitMQ、SQLite 在真实服务上验证），Standard 示例零外部基础设施运行，换 Provider 只改配置与包引用。P3 进行中：P3-01 操作契约与 XGI 已完成（RFC-0011：`[Operation]`、`IOperation<TRequest, TResponse>`、`OperationRegistry`、`IOperationInvoker`，Authorize 阶段按契约判定，宿主端点元数据携带操作描述）；P3-03 `Protocols.Http` 已完成（操作 ID 推导路由、配置覆盖与前缀、路由值/请求体/查询串绑定、`spec/protocol/http.md`），示例的 `/orders` 已改为 `CreateOrder` / `ListOrders` 两个操作契约；P3-02 `Kernel.Generators` 已完成（`[Operation]` → `GeneratedOperations` 登记代码与请求/响应 JSON Schema，`XHG001`–`XHG003` 诊断），`Protocols.Http` 据此发布 OpenAPI 3.1（`MapXiHanOpenApi()`），示例改为 `context.AddGeneratedOperations()`。P3-05 `Protocols.Mcp` 已完成（`Expose.Mcp` 的操作成为 MCP tool：JSON-RPC 2.0 + Streamable HTTP，`tools/list` 复用生成的 Schema 与消息目录里的 `<操作 ID>.description`，`tools/call` 以独立调用经完整管道执行、失败按 `isError`，`spec/protocol/mcp.md`）；P3-04 `Protocols.Grpc` 已完成（`Expose.Grpc` 的操作成为一元方法 `{Resource}/{Verb}`，protobuf 线格式由操作的 JSON Schema 推出、服务端在 JSON 与 protobuf 间转换且不依赖 Google.Protobuf，`.proto` 由同一份描述生成并经 `/operations.proto` 发布，错误映射为 gRPC 状态 + `xihan-error-code` 尾部元数据，`grpc-timeout` 进截止时间，`spec/protocol/grpc.md`）。一份契约已同时在 HTTP、gRPC、MCP 上服务，三者共用一份 Schema。P3-06 `Protocols.CloudEvents` 已完成（消息信封的 CloudEvents 1.0 HTTP 绑定：入站 `POST /events` 接收 binary / structured / batch 事件，按类型映射主题后在请求内经管道派发给处理器或发布到代理；出站 `CloudEventsWebhookPublisher` 把信封推送到配置的 Webhook，`spec/protocol/cloudevents.md`）。P3-07 `Hosting.CommandLine` 已完成（通用宿主里的命令行宿主：`help` / `graph` / `pipeline` / `operations` / `invoke`，`<resource> <verb>` 简写，`--json` / `--data` / 字段选项成载荷，`--tenant` / `--correlation` / `--locale` / `--timeout` / `--authorization` 进上下文，失败按错误 KIND 映射退出码并把 Problem 写到 stderr，日志只走 stderr；`Protocols.Mcp` 据此注册 `mcp` 命令提供 stdio 传输；`samples/SimpleCli`；P1-19 的 `xihan graph` / `xihan pipeline dump` 由此提供，`spec/protocol/commandline.md`）。P3-08 进行中：`ObjectStorage`（RFC-0012：`ObjectStorage.Abstractions` 的 `IBlobStore`——批量写入与删除、按键读取、按前缀分页列举；默认实现 `ObjectStorage.FileSystem`，原子替换写入、SHA-256 ETag、总量与单对象配额；套件 `ObjectStorage.Conformance`）与 `Scheduling`（RFC-0013：`Scheduling.Abstractions` 的作业与计划——手动 / 间隔 / 5 段 cron、`AddJob<TJob>`、经完整管道的 `JobDispatcher`、`IJobScheduler` 批量触发与状态、租约式 `ILeaderElection`；默认实现 `Scheduling.InMemory` 单进程轮询调度器与进程内租约；套件 `Scheduling.Conformance`）与 `Configuration`（RFC-0014：`Configuration.Abstractions` 的 `IRemoteConfigurationClient`（带版本的整份文档，可长轮询）与 `ISecretStore`（批量只读，值遮蔽），以及把它们接进 `IConfigurationBuilder` 的 `AddXiHanRemoteConfiguration` / `AddXiHanSecrets`（首次同步加载、轮询重载、失败保留最后一份）；默认实现是 .NET 原生文件与环境变量，无包；套件 `Configuration.Conformance`）与 `Auditing`（RFC-0015：`Auditing.Abstractions` 的 Observe 阶段审计中间件——按显式动作或 `Selection` 决定记录、结果取管道 outcome、接收器故障只记日志——以及 `SetAuditAction` / `SetAuditResource` / `AddAuditDetail` 与查询契约 `IAuditTrail`；默认实现 `Auditing.InMemory` 有界环形缓冲并写日志；套件 `Auditing.Conformance`）与 `Localization.Database`（`localization.resources@1` 的数据库 Provider：`MessageTranslation` 实体经仓储读写，`MessageTranslationStore` 批量增改删，`MessageTranslationLoader` 在宿主进入服务状态后经管道分页加载并按周期刷新，数据库快照优先于嵌入目录；新增 `Localization.Conformance` 套件，内存与数据库两个实现共同通过）已完成。P3-08 的首批外部 Provider（S3、Nacos、Vault、`Scheduling.Kubernetes` 等）随 P3-11 / P3-12 的部署工作一起做。P3-09 `Observability.OpenTelemetry` 已完成（`observability.export@1`：内核的 `xihan.invoke` Span 与 `xihan.invocations` / `xihan.invocation.duration` 仪表连同 .NET、ASP.NET Core 内置的源与仪表按配置接到追踪、指标、日志提供者；OTLP 在配置端点或 `OTEL_EXPORTER_OTLP_ENDPOINT` 时自动启用，可选控制台导出器与根 Span 采样比例；无需检测包）。接下来是 P3-13 `Profiles.Full`、`Cli`、OpenID Connect。各阶段退出条件见 [`nextdocs/architecture/05-开发计划.md`](../nextdocs/architecture/05-开发计划.md)。
