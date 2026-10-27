# XiHan Framework 5（next 工作区）

下一代框架的 .NET 参考实现工作区。设计文档在仓库根的 [`nextdocs/`](../nextdocs/下一代框架总体方案.md)，规范草案在本目录 `spec/`。

## 布局

```text
next/
├─ XiHan.Framework.Next.slnx        # 解决方案目录按环层分组
├─ Directory.Build.props            # 版本线 5.x、Nullable、警告即错误、文档文件
├─ Directory.Packages.props         # 中央包版本，Ring 0/1 只允许 Microsoft.Extensions.* 抽象包
├─ spec/                            # XiHan Spec：RFC、术语表、清单 Schema、命名约定、协议映射、插件协议
├─ scripts/                         # Sync-PublicApi.ps1 等维护脚本
├─ src/                             # 正式包，物理目录扁平，包名由术语表 + 文法推出
├─ test/                            # 单元、架构、集成测试
├─ samples/                         # SimpleApp（Standard 组合、Native AOT 冒烟）、SamplePlugin（进程内插件）、EchoPlugin（进程外插件子进程）、EchoWasmPlugin（Rust 写的 Wasm 插件）、EchoGoPlugin（用 framework-sdk-go 写的进程外插件）、CacheGoPlugin（Go 写的 caching.distributed 远程 Provider）
└─ incubator/                       # 实验包，只能标 Experimental
```

## 正式包

| 包 | 环 | 内容 |
| --- | --- | --- |
| `XiHan.Framework.Primitives` | 0 | 原语与契约，只增不改 |
| `XiHan.Framework.Kernel` | 1 | 宿主、能力图谱、管道、诊断薄桥、安全原语、插件抽象、操作契约与 XGI 调用器、Resilience 阶段的截止时间与按操作 ID 的熔断 / 限流 / 并发上限、Validate 阶段的契约校验（JSON Schema 子集 + `IValidatableObject`） |
| `XiHan.Framework.Domain` | 2 | 实体协议、值对象、领域事件缓冲、规约；纯契约 |
| `XiHan.Framework.Localization.Abstractions` | 2 | 区域名、内置 10 种区域（默认 `en-US`）、消息目录（每种语言一个文件）、显式区域的本地化接口；消息键、模板与 `key.L()` 在原语层 |
| `XiHan.Framework.Persistence.Abstractions` | 2 | 仓储、工作单元（登记写过的聚合）、查询（含时间范围提示）、标识生成、Transaction 阶段中间件（提交前经 `IDomainEventDispatcher` 派发领域事件）、实体登记 |
| `XiHan.Framework.Caching.Abstractions` | 2 | 字节存储、类型化缓存、序列化器、分布式锁、Idempotency 阶段中间件、`IDistributedCache` 适配 |
| `XiHan.Framework.MultiTenancy.Abstractions` | 2 | 租户信息与存储、租户解析器、租户分片契约与默认解析器、Tenant 阶段中间件 |
| `XiHan.Framework.Security.Abstractions` | 2 | 凭据与认证处理器、授权要求与策略目录、RBAC 授予，Authenticate / Authorize 阶段中间件 |
| `XiHan.Framework.Messaging.Abstractions` | 2 | CloudEvents 信封、代理与发布者、处理器订阅、经管道的派发、事务性发件箱、领域事件到消息的登记与派发 |
| `XiHan.Framework.ObjectStorage.Abstractions` | 2 | 对象存储契约：容器与键文法、批量写入与删除、按键读取、按前缀分页列举 |
| `XiHan.Framework.Scheduling.Abstractions` | 2 | 调度契约与引擎：作业与触发器、计划（间隔 / Quartz 文法 cron / 一次 / 手动，起止窗口与日历）、misfire 策略、经管道的派发、作业存储契约 `IJobStore`、集群分发引擎 `JobSchedulerEngine`（抢占、独占、失联恢复、历史）、租约式领导选举 |
| `XiHan.Framework.Configuration.Abstractions` | 2 | 远程配置客户端与密钥库契约，以及接进 `IConfigurationBuilder` 的配置源（轮询重载、失败保留最后一份） |
| `XiHan.Framework.Auditing.Abstractions` | 2 | Observe 阶段审计中间件、调用上下文的动作 / 资源 / 细节扩展、审计查询契约 |
| `XiHan.Framework.AI.Abstractions` | 2 | AI 契约：基于 `Microsoft.Extensions.AI` 的 Provider 目录、带提示词 / 护栏 / 工具的 `IAIChat`、`Expose.Ai` 操作桥接成模型工具的目录（`AIToolsFeature` 默认提供 `ai.tools@1`）、知识库流水线与 `IKnowledgeStore` 契约 |
| `XiHan.Framework.Workflow.Abstractions` | 2 | 工作流契约与引擎：JSON 定义模型、校验与目录、活动（经完整管道执行的 19 个内置活动，含事件网关 / 补偿 / Http / 外部任务）、实例 / 书签 / 故障、用户任务服务、表达式、`workflow.*` 事件、存储与实例锁契约 |
| `XiHan.Framework.Localization.InMemory` | 3 | 内存本地化默认实现：汇集各包目录，精确区域 → 同语言 → 默认区域 → `en-US` 回退，设进程默认消息作用域 |
| `XiHan.Framework.Persistence.InMemory` | 3 | 内存持久化默认实现（`role: default`）：有界、读己之写、软删除与租户隔离 |
| `XiHan.Framework.Persistence.EntityFrameworkCore` | 3 | EF Core Provider：应用自有 DbContext、事务型工作单元、协议约定（非 AOT） |
| `XiHan.Framework.Caching.InMemory` | 3 | 内存缓存默认实现：有界 LRU、绝对与滑动过期，兼任单进程共享缓存 |
| `XiHan.Framework.Caching.Redis` | 3 | Redis Provider：共享缓存、分布式锁、幂等（非 AOT） |
| `XiHan.Framework.Caching.Hybrid` | 3 | 两级缓存 Provider：L1 进程内对象 LRU + L2 被选中的共享缓存，读穿透回填、写删先远后近、L1 存活上限 |
| `XiHan.Framework.Caching.Remote` | 3 | 远程 Provider 适配器（RFC-0019，Experimental）：`caching.distributed` 由实现操作集 `cache-entries.*` 的进程外插件提供（非 AOT） |
| `XiHan.Framework.MultiTenancy.InMemory` | 3 | 内存租户存储默认实现：从配置装载、有界 |
| `XiHan.Framework.MultiTenancy.Database` | 3 | 数据库租户目录 Provider：租户以实体经仓储读写，单例目录直接开工作单元读取并按租户有界缓存（带存活时间、写入即失效），分片从租户属性推出 |
| `XiHan.Framework.Security.InMemory` | 3 | 内存安全默认实现：ApiKey 主体与 RBAC 授予，从配置装载 |
| `XiHan.Framework.Security.Database` | 3 | 数据库授权 Provider：只提供 `security.authorization@1`，授予以实体经仓储读写（全局授予以空字符串租户列落库），授予存储带有界 TTL 缓存，`PermissionGrantRecordStore` 批量授予 / 撤销逐项结果 |
| `XiHan.Framework.Security.Jwt` | 3 | JWT Bearer 认证 Provider（非 AOT） |
| `XiHan.Framework.Security.OpenIdConnect` | 3 | OpenID Connect Bearer 认证 Provider：发现文档 + JWKS，密钥轮换时限速刷新（非 AOT） |
| `XiHan.Framework.Messaging.InMemory` | 3 | 内存消息代理默认实现：有界通道、顺序投递、重试与死信 |
| `XiHan.Framework.Messaging.RabbitMQ` | 3 | RabbitMQ Provider：持久队列、手动确认、重试与死信队列（非 AOT） |
| `XiHan.Framework.Messaging.Kafka` | 3 | Kafka Provider：队列与流，幂等生产者、消费组位点、`xihankey` 分区键有序、按时刻重放、原位重试与死信主题（非 AOT） |
| `XiHan.Framework.Configuration.Nacos` | 3 | Nacos Provider：HTTP 接口直连取整份文档，`Content-MD5` 为版本并长轮询，JSON / properties 展平，登录续令牌，多地址 |
| `XiHan.Framework.Localization.Database` | 3 | 数据库本地化 Provider：翻译经仓储读写，启动后加载并周期刷新，优先于嵌入目录 |
| `XiHan.Framework.ObjectStorage.FileSystem` | 3 | 文件系统对象存储默认实现：原子替换写入、SHA-256 ETag、总量与单对象配额 |
| `XiHan.Framework.ObjectStorage.S3` | 3 | S3 Provider：Amazon S3 与兼容服务（MinIO / OSS / COS / R2），条件写入实现不覆盖，批量删除一次请求（非 AOT） |
| `XiHan.Framework.Scheduling.InMemory` | 3 | 内存调度默认实现：进程内作业存储（有界历史）+ 抽象包引擎、进程内租约领导选举 |
| `XiHan.Framework.Scheduling.Database` | 3 | 数据库 Provider：作业 / 触发器 / 运行 / 节点 / 租约五张表经仓储读写，并发戳 CAS 抢占的集群分发、节点心跳与失联恢复、数据库领导租约 |
| `XiHan.Framework.Scheduling.Kubernetes` | 3 | Kubernetes Provider：以 Lease 做跨 Pod 的领导选举，直接调用 API Server，无客户端库 |
| `XiHan.Framework.Auditing.InMemory` | 3 | 内存审计默认实现：有界环形缓冲、可查询、每条事件写日志 |
| `XiHan.Framework.Auditing.Database` | 3 | 数据库 Provider：审计事件经有界队列后台批量落库（满了丢弃并计数、失败只记日志不重试），`AuditRecord` 经仓储读写，按进程写入序号分页查询、续页令牌是序号 |
| `XiHan.Framework.AI.OpenAI` | 3 | OpenAI 兼容端点的 `ai.chat@1` / `ai.embedding@1` Provider：按配置创建客户端，工具调用在客户端循环里自动执行 |
| `XiHan.Framework.AI.VectorData` | 3 | `ai.knowledge@1` Provider：把 `IKnowledgeStore` 映射到应用注册的 `Microsoft.Extensions.VectorData` 向量存储 |
| `XiHan.Framework.Workflow.InMemory` | 3 | 内存工作流默认实现：有界内存的定义 / 实例 / 书签存储、进程内实例锁、单进程到期书签轮询 |
| `XiHan.Framework.Workflow.Database` | 3 | 数据库 Provider：定义 / 实例 / 节点实例 / 书签以 JSON 文档加查询列经仓储读写，每个存储操作一个事务，租约行实例锁（续约、CAS 接管），轮询只在持有租约的节点上进行 |
| `XiHan.Framework.Observability.OpenTelemetry` | 3 | OpenTelemetry 导出：内核与运行时的追踪、指标、日志，OTLP 与控制台导出器 |
| `XiHan.Framework.Hosting.AspNetCore` | 4 | HTTP 请求桥接、`Accept-Language` 协商、探针端点、本地化的 Problem Details |
| `XiHan.Framework.Hosting.CommandLine` | 4 | 命令行宿主：`help` / `graph` / `pipeline` / `operations` / `invoke`，按错误 KIND 的退出码，Problem 写 stderr |
| `XiHan.Framework.Protocols.Http` | 5 | HTTP 协议适配器：操作 ID 推导路由（可覆盖）、路由值/请求体/查询串绑定为 XGI 载荷、`MapXiHanOperations()`、OpenAPI 带权限扩展 |
| `XiHan.Framework.Protocols.Grpc` | 5 | gRPC 协议适配器：操作成为一元方法，protobuf 线格式由 JSON Schema 推出，`/operations.proto` 发布 |
| `XiHan.Framework.Protocols.Mcp` | 5 | MCP 协议适配器：操作成为 tool，JSON-RPC 2.0 + Streamable HTTP 与 stdio 传输 |
| `XiHan.Framework.Protocols.CloudEvents` | 5 | CloudEvents 1.0 HTTP 绑定：入站 `/events` 派发或发布，出站 Webhook 发布者 |
| `XiHan.Framework.Plugins.Protocol` | 3 | 插件协议 `xihan.plugin.v1` 的 .NET 类型：生成的消息与桩、错误码与消息键、握手核对与映射，各隔离级别的宿主共用（非 AOT） |
| `XiHan.Framework.Plugins.InProcess` | 5 | 可收集 AssemblyLoadContext 的插件宿主（非 AOT） |
| `XiHan.Framework.Plugins.OutOfProcess` | 5 | 进程外插件宿主：按 `xihan.plugin.v1` 启动、握手、监视与重启子进程，代理操作、事件投递、受权限约束的回调（非 AOT） |
| `XiHan.Framework.Plugins.Wasm` | 5 | Wasm 插件宿主（Experimental）：wasmtime 沙箱里的 WASI reactor 模块，同一套消息按 JSON 经导出函数 + 线性内存，可贡献中间件，内存 / CPU 上限与崩溃重建（非 AOT） |
| `XiHan.Framework.Profiles.Minimal` | 6 | 组合包：原语层 + 内核 + ASP.NET Core 宿主 + 内存本地化 |
| `XiHan.Framework.Profiles.Standard` | 6 | 组合包：Minimal + Domain + 五个子系统的抽象与内存默认实现，`UseStandardProfile()` |
| `XiHan.Framework.Profiles.Full` | 6 | 组合包：Standard + 对象存储、调度、审计的默认实现 + OpenTelemetry + 四种协议适配器，`UseFullProfile()` |
| `XiHan.Framework.Kernel.Analyzers` | 工具链 | Roslyn 分析器 XH0001–XH0005：ApiLevel、命名、Internal 引用、零反射 |
| `XiHan.Framework.Kernel.Generators` | 工具链 | 源生成器：`[Operation]` → 登记代码与请求/响应 JSON Schema（XHG001–XHG003） |
| `XiHan.Framework.Persistence.Generators` | 工具链 | 源生成器：实体类 → 仓储登记、EF Core 实体登记、软删除 / 租户过滤规约（XHP001–XHP003） |
| `XiHan.Framework.Cli` | 工具链 | `xihan` 工具：`pack` 从 `xihan.app.json` 生成 Dockerfile / Kustomize / Compose / Aspire 产物，`graph` / `pipeline` / `operations` / `invoke` 转发给应用 |
| `XiHan.Framework.Testing` | 工具链 | 测试宿主、特性桩、清单与上下文构造 |
| `XiHan.Framework.Persistence.Conformance` | 工具链 | `persistence.relational` Provider 必须通过的 xUnit 套件 |
| `XiHan.Framework.Caching.Conformance` | 工具链 | `ICacheStore` 实现必须通过的 xUnit 套件 |
| `XiHan.Framework.MultiTenancy.Conformance` | 工具链 | `ITenantStore` 实现必须通过的 xUnit 套件 |
| `XiHan.Framework.Security.Conformance` | 工具链 | `IPermissionGrantStore` 实现必须通过的 xUnit 套件 |
| `XiHan.Framework.Messaging.Conformance` | 工具链 | `messaging.queue` Provider 必须通过的 xUnit 套件 |
| `XiHan.Framework.Localization.Conformance` | 工具链 | `IMessageLocalizer` 实现必须通过的 xUnit 套件 |
| `XiHan.Framework.ObjectStorage.Conformance` | 工具链 | `IBlobStore` 实现必须通过的 xUnit 套件 |
| `XiHan.Framework.Scheduling.Conformance` | 工具链 | `IJobScheduler` 与 `ILeaderElection` 实现必须通过的 xUnit 套件 |
| `XiHan.Framework.Configuration.Conformance` | 工具链 | `IRemoteConfigurationClient` 与 `ISecretStore` 实现必须通过的 xUnit 套件 |
| `XiHan.Framework.Auditing.Conformance` | 工具链 | `IAuditSink` + `IAuditTrail` 实现必须通过的 xUnit 套件 |
| `XiHan.Framework.AI.Conformance` | 工具链 | 知识库存储与模型 Provider 目录必须通过的 xUnit 套件，含确定性的哈希嵌入 |
| `XiHan.Framework.Workflow.Conformance` | 工具链 | 工作流存储 / 实例锁与引擎装配必须通过的 xUnit 套件 |
| `XiHan.Framework.Plugins.Conformance` | 工具链 | 任何语言实现的 `xihan.plugin.v1` 进程外插件必须通过的 xUnit 黑盒套件 |

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
dotnet run --project next/src/XiHan.Framework.Cli -- pack --manifest next/samples/SimpleApp/xihan.app.json --output /tmp/simple-app-deploy   # Dockerfile、Kustomize、Compose、Aspire
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

`samples/KubernetesApp` 用 Full 组合演示云原生落地（P3-12）：探针（`/health/startup|ready|live`）、优雅停机（`XiHan:Kernel:StopTimeoutSeconds` 与 `terminationGracePeriodSeconds` 对齐）、ConfigMap 热更新（`XIHAN_CONFIG_PATH` → `IOptionsMonitor`，`/config`）、Lease 领导选举（`Scheduling.Kubernetes`，`/leader` 与只在领导 Pod 上运行的心跳作业）。`deploy/` 是 `xihan pack` 从 `xihan.app.json` 生成并提交的产物（测试保证不漂移）：

```bash
dotnet run --project next/samples/KubernetesApp                                  # 本地：进程内选举，http://localhost:5100/leader
cd next && docker build -f samples/KubernetesApp/deploy/Dockerfile -t ghcr.io/xihanfun/kubernetes-app:5.0.0-preview.1 .   # 构建上下文是 next/（build.context）
kind load docker-image ghcr.io/xihanfun/kubernetes-app:5.0.0-preview.1 && kubectl apply -k next/samples/KubernetesApp/deploy/kustomize
kubectl get lease kubernetes-app-scheduler                                         # 持有者是其中一个 Pod；删掉它后另一个 Pod 接管
kubectl edit configmap kubernetes-app-config                                       # 改 Sample:Banner，稍后 /config 返回新值，无需重启
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
| 依赖方向、包 ID 文法、命名空间、零反射、第三方类型泄漏、版权头、`<summary>` 三行写法 | `test/XiHan.Framework.Architecture.Tests` |
| CI | `.github/workflows/next-ci.yml` |

## 阶段

P1 内核已完成：原语层、内核、ASP.NET Core 宿主、进程内插件宿主、测试基座、分析器、示例与基准；`Kernel.Generators` 与 `Cli` 顺延到 P3 随操作契约一起实现。P2 子系统已完成：`Domain` 与 `Persistence`（抽象、内存默认实现、EF Core Provider、一致性套件，RFC-0005）、`Caching`（抽象、内存默认实现、Redis Provider、一致性套件、幂等中间件，RFC-0006）、`MultiTenancy`（抽象、内存默认实现、一致性套件、Tenant 阶段中间件，RFC-0007）、`Security`（抽象、内存默认实现、JWT Provider、一致性套件、Authenticate / Authorize 阶段中间件，RFC-0008）、`Messaging`（抽象、内存默认实现、RabbitMQ Provider、一致性套件、CloudEvents 信封、发件箱，RFC-0009）、`Localization`（原语层消息键、模板、`key.L()` 与 `MessageScope`；抽象与内存默认实现；默认 `en-US` 与内置 10 种区域、每包每种语言一个文件、宿主 `Accept-Language` 协商与 Problem Details 本地化，RFC-0010）、`Profiles.Minimal` / `Profiles.Standard` 与 Standard 组合示例。退出条件已满足：全部默认实现与 Provider 通过一致性套件（Redis、RabbitMQ、SQLite 在真实服务上验证），Standard 示例零外部基础设施运行，换 Provider 只改配置与包引用。P3 进行中：P3-01 操作契约与 XGI 已完成（RFC-0011：`[Operation]`、`IOperation<TRequest, TResponse>`、`OperationRegistry`、`IOperationInvoker`，Authorize 阶段按契约判定，宿主端点元数据携带操作描述）；P3-03 `Protocols.Http` 已完成（操作 ID 推导路由、配置覆盖与前缀、路由值/请求体/查询串绑定、`spec/protocol/http.md`），示例的 `/orders` 已改为 `CreateOrder` / `ListOrders` 两个操作契约；P3-02 `Kernel.Generators` 已完成（`[Operation]` → `GeneratedOperations` 登记代码与请求/响应 JSON Schema，`XHG001`–`XHG003` 诊断），`Protocols.Http` 据此发布 OpenAPI 3.1（`MapXiHanOpenApi()`），示例改为 `context.AddGeneratedOperations()`。P3-05 `Protocols.Mcp` 已完成（`Expose.Mcp` 的操作成为 MCP tool：JSON-RPC 2.0 + Streamable HTTP，`tools/list` 复用生成的 Schema 与消息目录里的 `<操作 ID>.description`，`tools/call` 以独立调用经完整管道执行、失败按 `isError`，`spec/protocol/mcp.md`）；P3-04 `Protocols.Grpc` 已完成（`Expose.Grpc` 的操作成为一元方法 `{Resource}/{Verb}`，protobuf 线格式由操作的 JSON Schema 推出、服务端在 JSON 与 protobuf 间转换且不依赖 Google.Protobuf，`.proto` 由同一份描述生成并经 `/operations.proto` 发布，错误映射为 gRPC 状态 + `xihan-error-code` 尾部元数据，`grpc-timeout` 进截止时间，`spec/protocol/grpc.md`）。一份契约已同时在 HTTP、gRPC、MCP 上服务，三者共用一份 Schema。P3-06 `Protocols.CloudEvents` 已完成（消息信封的 CloudEvents 1.0 HTTP 绑定：入站 `POST /events` 接收 binary / structured / batch 事件，按类型映射主题后在请求内经管道派发给处理器或发布到代理；出站 `CloudEventsWebhookPublisher` 把信封推送到配置的 Webhook，`spec/protocol/cloudevents.md`）。P3-07 `Hosting.CommandLine` 已完成（通用宿主里的命令行宿主：`help` / `graph` / `pipeline` / `operations` / `invoke`，`<resource> <verb>` 简写，`--json` / `--data` / 字段选项成载荷，`--tenant` / `--correlation` / `--locale` / `--timeout` / `--authorization` 进上下文，失败按错误 KIND 映射退出码并把 Problem 写到 stderr，日志只走 stderr；`Protocols.Mcp` 据此注册 `mcp` 命令提供 stdio 传输；`samples/SimpleCli`；P1-19 的 `xihan graph` / `xihan pipeline dump` 由此提供，`spec/protocol/commandline.md`）。P3-08 进行中：`ObjectStorage`（RFC-0012：`ObjectStorage.Abstractions` 的 `IBlobStore`——批量写入与删除、按键读取、按前缀分页列举；默认实现 `ObjectStorage.FileSystem`，原子替换写入、SHA-256 ETag、总量与单对象配额；套件 `ObjectStorage.Conformance`）与 `Scheduling`（RFC-0013：`Scheduling.Abstractions` 的作业与计划——手动 / 间隔 / 5 段 cron、`AddJob<TJob>`、经完整管道的 `JobDispatcher`、`IJobScheduler` 批量触发与状态、租约式 `ILeaderElection`；默认实现 `Scheduling.InMemory` 单进程轮询调度器与进程内租约；套件 `Scheduling.Conformance`）与 `Configuration`（RFC-0014：`Configuration.Abstractions` 的 `IRemoteConfigurationClient`（带版本的整份文档，可长轮询）与 `ISecretStore`（批量只读，值遮蔽），以及把它们接进 `IConfigurationBuilder` 的 `AddXiHanRemoteConfiguration` / `AddXiHanSecrets`（首次同步加载、轮询重载、失败保留最后一份）；默认实现是 .NET 原生文件与环境变量，无包；套件 `Configuration.Conformance`）与 `Auditing`（RFC-0015：`Auditing.Abstractions` 的 Observe 阶段审计中间件——按显式动作或 `Selection` 决定记录、结果取管道 outcome、接收器故障只记日志——以及 `SetAuditAction` / `SetAuditResource` / `AddAuditDetail` 与查询契约 `IAuditTrail`；默认实现 `Auditing.InMemory` 有界环形缓冲并写日志；套件 `Auditing.Conformance`）与 `Localization.Database`（`localization.resources@1` 的数据库 Provider：`MessageTranslation` 实体经仓储读写，`MessageTranslationStore` 批量增改删，`MessageTranslationLoader` 在宿主进入服务状态后经管道分页加载并按周期刷新，数据库快照优先于嵌入目录；新增 `Localization.Conformance` 套件，内存与数据库两个实现共同通过）已完成。P3-08 的首批外部 Provider（S3、Nacos、Vault、`Scheduling.Kubernetes` 等）随 P3-11 / P3-12 的部署工作一起做。P3-09 `Observability.OpenTelemetry` 已完成（`observability.export@1`：内核的 `xihan.invoke` Span 与 `xihan.invocations` / `xihan.invocation.duration` 仪表连同 .NET、ASP.NET Core 内置的源与仪表按配置接到追踪、指标、日志提供者；OTLP 在配置端点或 `OTEL_EXPORTER_OTLP_ENDPOINT` 时自动启用，可选控制台导出器与根 Span 采样比例；无需检测包）。P3-13 `Profiles.Full` 已完成（Standard + 对象存储、调度、审计的默认实现 + OpenTelemetry 导出 + 四种协议适配器；测试在零外部基础设施下验证每个能力都选中默认实现、管道阶段齐全、健康 / OpenAPI / .proto / MCP 端点应答）。P3-10 `Persistence.Generators` 已完成（从实现 `IEntity<TKey>` 的实体类生成 `AddGeneratedRepositories()`、引用 EF Core 时的 `ApplyGeneratedEntities()`，以及软删除 / 多租户实体的 `<实体>Filters` 规约；`XHP001`–`XHP003` 诊断；示例改为生成的登记）。P3-11 `xihan pack` 已完成（RFC-0016：`Cli` 工具包；`xihan.app.json` 描述名字、项目、镜像、端口、绑定、配置、探针、停机、资源、伸缩与领导租约，`xihan pack` 生成 chiseled 非 root 的 Dockerfile、含探针 / 宽限 / 安全上下文 / Service Binding 挂载的 Kustomize 清单、每个绑定一个依赖服务的 Compose 文件与 Aspire AppHost 登记；`graph` / `pipeline` / `operations` / `invoke` 转发给应用的命令行宿主；示例自带 `xihan.app.json`）。P3-12 k8s 示例已完成（`Hosting.*` 的 `AddXiHan` 装载 `XIHAN_CONFIG_PATH` 目录并监听变化；`Scheduling.Kubernetes` 以 Lease 做跨 Pod 领导选举，直接调用 API Server；`samples/KubernetesApp` 带 `xihan.app.json`、提交的 `deploy/` 产物与验证步骤，探针、宽限、热更新、领导选举四项对齐）。`Security.OpenIdConnect`（P2-09 顺延项）已完成（`security.authentication@1` 的 OpenID Connect Provider：按需取签发者的发现文档与 JWKS、按间隔自动刷新、未知签名密钥时限速按需刷新、刷新失败沿用旧密钥、签发者不可达返回 `SECURITY_UNAVAILABLE_003`）。P3 全部条目已完成，退出条件（一份契约三种协议、k8s 示例）已满足。P4 进行中：P4-01 插件协议已完成（RFC-0017：`spec/plugin/plugin.proto` 的 `xihan.plugin.v1`——`Plugin` 服务的握手 / 配置 / 启停 / 健康与推送 / 一元与流式调用 / 事件投递，`Host` 服务的操作回调与消息发布；`handshake.md` 的环境变量、stdout 握手行、版本协商与会话令牌，`lifecycle.md` 的状态机、激活、健康、停止与重启退避，`invocation.md` 的代理操作、上下文与错误映射、事件投递与回调权限；向量 `conformance/vectors/plugin/`）。P4-02 `Plugins.OutOfProcess` 已完成（配置阶段按目录发现插件——启动进程、读握手行、`Handshake` 并核对清单——把声明的操作登记为代理操作经完整管道调用、消息目录接进本地化器、订阅接进消息派发；启动阶段启动命名管道 / Unix 域套接字上的回调服务器与 `onStartup` 插件，`onOperation:` / `onEvent:` 懒激活；健康推送与轮询、崩溃退避重启与重启上限、`Stop` 宽限与终止；回调 `Host.Invoke` / `Host.Publish` 以插件身份执行并按 `capability:` 权限判定；内核新增 `IHealthContributor`，每个插件是应用的健康条目，Failed / Degraded 让应用 Degraded 但不退出；示例 `samples/EchoPlugin` 只依赖 protobuf 与 gRPC，测试以真实子进程验证调用、本地化、回调、事件、崩溃重启、停止与运行期加载）。P4-04 `Plugins.Conformance` 已完成（协议黑盒套件：握手行与版本协商、无共同版本时拒绝、清单一致性、Configure / Start / Health / WatchHealth / Stop 与宽限内退出、未知操作 Unimplemented、声明的操作都返回 status、订阅投递确认、stdin 关闭即退出；EchoPlugin 通过全部用例）。P4-03 `Plugins.Wasm` 已完成（RFC-0018 / ADR-0009，Experimental：同一套消息按 proto3 JSON 经导出函数 + 线性内存，WASI preview 1 reactor 核心模块由 wasmtime 在进程内沙箱运行；`Plugins.Protocol` 抽出与载体无关的协议类型供两种宿主共用；内核管道支持按名登记同类型的多个中间件实例，插件清单的 `contributes.pipeline` 据此插进对应阶段，before 可短路或改写 Baggage、after 只观察、失败即关闭；内存 / 栈上限与 epoch 截止、陷阱与超时后退避重建、回调经导入函数并按权限判定，Windows 宿主可执行项目须 `CETCompat=false`（wasmtime 陷阱与影子栈冲突，创建宿主时检测并以 `PLUGINS_UNAVAILABLE_003` 拒绝）；Rust 示例 `samples/EchoWasmPlugin` 的产物随仓库提交，`Plugins.Conformance` 新增 `WasmPluginConformance`）。P4-08 已完成（ADR-0010）：规范拆为独立公开仓库 [`XiHanFun/framework-spec`](https://github.com/XiHanFun/framework-spec)（保留 `next/spec` 的全部提交历史），含一致性向量、规范文法 `rules` 包与语言中立的黑盒运行器 `xihan-conformance`（Go 写，对 .NET 的 EchoPlugin 全部用例通过）；本仓库以 submodule 在 `next/spec` 引用它（clone 或新建 worktree 后 `git submodule update --init next/spec`），规范改动先在 framework-spec 提交再更新指针。P4-05 Go SDK 已完成：[`XiHanFun/framework-sdk-go`](https://github.com/XiHanFun/framework-sdk-go)（模块 `github.com/xihanfun/framework-sdk-go`）的 `primitives`（错误与错误码、`Result[T]`、`Maybe[T]`、类型化标识、生命周期、管道阶段、稳定性、Baggage 键、消息键与模板）、`manifest`（清单模型与校验）、`invocation`（调用上下文，嵌入 `context.Context`）、`pipeline`（十一阶段构建器与排序规则）、`operation`（契约、登记表、经管道执行的调用器）、`plugin`（进程外插件运行时：环境变量、握手行、`xihan.plugin.v1` 服务端、生命周期与健康推送、事件投递、宿主回调客户端、宿主可解析的 JSON 日志、消息目录）与 `plugin/v1`（由规范 proto 生成），原语与协议规则都通过 framework-spec 的向量；`examples/echo` 的 `echo-go` 通过黑盒套件全部用例。P4-06 已完成：`samples/EchoGoPlugin` 以该 SDK 构建的 Go 插件由 `Plugins.OutOfProcess` 加载，`EchoGoPluginConformanceTests` 与 `EchoGoPluginHostTests` 在测试运行时用本机 `go` 构建它（没有 `go` 时跳过，CI 装 Go），验证发现、经管道调用、握手目录翻译插件错误、回调以插件身份执行且需授予权限、事件投递、崩溃后 Degraded 并退避重启、停止即退出。P4-07 已完成（RFC-0019 / ADR-0011）：远程 Provider = 宿主侧 `<领域>.Remote` 适配器 + 插件实现规范规定的操作集。`Caching.Remote` 提供 `caching.distributed@1`，`RemoteCacheStore` 按操作集 `cache-entries.get / set / try-add / remove / try-remove` 直接执行宿主登记表里的代理操作（不走管道，插件未加载、未登记、不可达、超时都映射为 `CACHING_UNAVAILABLE_001`），插件侧错误的消息键 `caching.remote.*` 随包嵌入 10 种区域；framework-sdk-go 的 `caching` 包（`Store`、有界 `MemoryStore`、`RegisterOperations`）与 `examples/cache` 是插件侧实现，`samples/CacheGoPlugin` 由 `Caching.Remote.Tests` 在测试运行时构建并跑完整套 `Caching.Conformance`（32 路并发 `TryAdd`、256 KiB 值、真实时间的过期与滑动过期、分布式锁全部通过）。P4 全部条目已完成，退出条件（Go 插件在 .NET 宿主服务操作且崩溃不退出、Go SDK 通过全部向量、Spec 公开）已满足。P5 进行中：顺序与范围见 [`06-BasicApp迁移清查`](../nextdocs/architecture/06-BasicApp迁移清查.md)（Workflow / AI 随 5.1 迁，SqlSugar 前置，批量优先先行）；P5-00 批量优先已完成（ADR-0012，`spec/conventions/batch-first.md`）：`IRepository` 加 `FindManyAsync` 与 `AddRangeAsync` / `UpdateRangeAsync` / `RemoveRangeAsync`（逐项 `Result`，内存一次进覆盖层、EF Core 一次 `IN` 查询加一次 `SaveChanges`），`ICacheStore` / `ICache` 加 `GetManyAsync` / `SetManyAsync` / `RemoveManyAsync`（内存一次加锁、Redis `MGET` 与批次、远程操作集加 `cache-entries.get-many / set-many / remove-many`，framework-sdk-go 的 `caching.Store` 同步），`IMessagePublisher` / `IMessageBroker` 加 `PublishManyAsync`（内存整批入队、RabbitMQ 同通道连续发布，发件箱一次写入并按主题批量投递，插件宿主 `Host.Publish` 与 CloudEvents 入站按主题分组、出站多条用 batch 内容模式），三个一致性套件加批量用例，`Architecture.Tests` 的批量优先门禁禁止正式包在循环里调单条方法。P5-03 `Persistence.SqlSugar` 已完成（ADR-0013）：依赖 `SqlSugarCore`，`SqlSugarPersistenceFeature(configure)` 创建作用域内的客户端并自动应用 `SqlSugarConventions`（`Id` 主键不自增、`ConcurrencyStamp` 长度 32、`DateTimeOffset` 经转换器按 UTC 往返、可空列、无 setter 与集合属性忽略），`SqlSugarPersistenceFeature()` 复用应用注册的 `ISqlSugarClient`；工作单元首次写入 `BeginTran`，写入经 `InsertableByObject` / `UpdateableByObject` / `DeleteableByObject`（实体无 `new()` 约束），批量一次调用，更新与删除先在事务内 `TranLock` 取回当前行比对并发戳；SQLite 通过 `Persistence.Conformance` 全部用例；P5-01 `Compat.V4` 取消（ADR-0014：垫片只能省模块骨架，省不下迁移大头），模块骨架改特性归入 P5-04。P5-02 `xihan migrate v4` 已完成（RFC-0020，`spec/migration/v4.md`）：语法级 Roslyn 重写，不加载 MSBuild 工作区，改不了的位置留 `// xihan-migrate(<规则>): …` 标记，再跑一遍零改动；内核新增 `IInvocationAccessor`（作用域访问器，调用链深处不传上下文的服务取当前调用的上下文）供 V4-07 的 `CurrentUser` / `CurrentTenant` 用。已完成 V4-01 模块 → 特性（清单从命名空间推出、Pre / 主 / Post 钩子按序合并进 `Configure` / `StartAsync` / `StopAsync`、`[DependsOn]` 按模块表映射成 `Requires`、`GetApplicationBuilder()` 留标记、文件与引用改名）、V4-02 实体基类 → 协议接口（`BasicId` → `Id`、`CreatedTime` → `CreatedAt` 等改名，`RowVersion` / `TenantId` 改字符串并留改表标记，`Sugar*` 基类按表展开成接口与带原列名的属性，`: base(id)` 改体内赋值）、V4-03 注入标记接口 → 每项目一个 `<项目末段>V4Services.cs` 的显式注册、V4-04 `IClock` → `TimeProvider`、V4-05 删 `[UnitOfWork]` 并标记 `IUnitOfWorkManager`、V4-06 动态 API → 操作契约（委托模式：`[DynamicApi]` 服务类——含经抽象基类继承的——的每个公开实例方法生成 `Operations/<资源>/<方法>Operation.cs`，`[Operation("<资源>.<动词>")]` 主构造函数注入原服务、`HandleAsync` 只做委托，动词按 Create / Update / Delete / Get / List 前缀表推出，唯一的 Dto 参数直接作请求否则生成请求记录，`Task` 用项目级 `EmptyResponse`，`[PermissionAuthorize]` 原样进 `Permission`、`[Authorize]` → `Policy = "authenticated"`，每项目生成 `OperationsJsonContext`，原服务类删特性与 `ApplicationServiceBase` 并按作用域进 `V4Services`，项目文件加 `Kernel.Generators` 引用）、V4-07 `ICurrentUser` / `ICurrentTenant`（255 处调用点不改：每条项目引用链最底层的使用项目生成 `Security/` 下的同名应用接口与读 `IInvocationAccessor.Current` 的 `CurrentUser` / `CurrentTenant`，标识改字符串，使用文件补 `using`，`Change` 抛 `NotSupportedException` 并在调用点留标记）、V4-08 本地事件 → 消息（`ILocalEventHandler<T>` → `IMessageHandler`：每个事件一个主题常量，生成的 `HandleAsync` 按 `delivery.Topic` 解码并转调保留的 `HandleEventAsync`，按主题进 `V4Services`；`ILocalEventBus` → `IMessagePublisher`，`PublishAsync(evt)` 改成带主题与 `MessageEnvelope` 的发布，主题从 `new T(...)` 或变量声明类型推出，序列化处留标记提醒改用 `JsonSerializerContext`）、V4-09 `Localization/` 下 4.x 的 `{ resource, culture, texts }` 资源文件 → 每区域一个 `Localization/<区域>.json`（平铺键 `<领域>.<资源>.<键>`，原文件删除，项目文件补嵌入项并留装配目录的标记）、V4-10 包引用（含指向 4.x 框架项目的 `ProjectReference`）与 4.x `using` 清理；BasicApp 的 1695 个文件数秒跑完，第二遍零改动。5.x 后续 P0 的 `Workflow` 已完成（RFC-0021 / ADR-0016）：`Workflow.Abstractions` 的 JSON 定义模型（节点 / 转移 / 变量，表达式语言，默认出边只作兜底）、`IWorkflowDefinitionCatalog`（草稿 / 发布 / 新版本 / 停用 / 归档，发布前校验）、`IWorkflowEngine`（启动、按书签恢复、信号、挂起 / 恢复 / 取消 / 终止 / 重试全部批量）、15 个内置活动（Start / End / Terminate / SetVariable / Decision / Parallel / Join / Delay / PublishEvent / WaitSignal / SubWorkflow / ForEach / Log / Fault / UserTask，经完整管道以独立调用执行）、用户任务（Any / All / Sequential，转办与加签，指派规则 Provider）、重试策略与 `ContinueOnError`、补偿、`workflow.instance.* / node.* / task.*` 事件经 `IMessagePublisher` 发布；`Workflow.InMemory` 是有界内存存储 + 进程内实例锁 + 到期书签轮询的默认实现；`Workflow.Conformance` 有存储与引擎两套套件；`Profiles.Full` 已含工作流。5.x 后续 P0 的 `AI` 已完成（RFC-0022 / ADR-0017）：以 `Microsoft.Extensions.AI` 为线格式——`AI.Abstractions` 的 `IAIProviderCatalog`（按名字解析 `IChatClient` / `IEmbeddingGenerator`，缓存到 `Invalidate`）、`IAIChat`（提示词模板 → 工具绑定 → 护栏 → 调用，`AI_DENIED_001` / `AI_UNAVAILABLE_001` / `AI_TIMEOUT_001`）、内置关键词与提示注入护栏、`IAIToolCatalog`（`AddAITool` 的工具 + 内核新增的 `Expose.Ai` 操作桥接成 `AIFunction`，在新作用域经完整管道执行，失败以 JSON 错误交给模型；`AIToolsFeature` 默认提供 `ai.tools@1`，已进 `Profiles.Full`）、知识库流水线（切块 → 嵌入 → `IKnowledgeStore` → 检索 → 提示增强）；`AI.OpenAI` 提供 `ai.chat@1` / `ai.embedding@1`（OpenAI 兼容端点，`UseFunctionInvocation`），`AI.VectorData` 提供 `ai.knowledge@1`（任意 `VectorStore` 连接器）；`AI.Conformance` 有存储与 Provider 目录两套套件及哈希嵌入。P5-09 能力补齐已完成（ADR-0018）：租户分库（`MultiTenancy.Abstractions` 的 `ITenantShardResolver` / `TenantShard`，默认解析器从租户属性 `ConnectionString` / `DatabaseProvider` / `Shard.*` 推出并按租户缓存，Tenant 阶段解析后写进调用上下文，`MultiTenancy.InMemory` 同时提供 `multitenancy.sharding@1`，SqlSugar / EF Core 在作用域内按分片选连接）、`CacheStoreDistributedCache`（`IDistributedCache` 架在 `ICacheStore` 上，分布式缓存 Provider 被选中时登记）、`ObjectStorage.S3`（AWS SDK 覆盖 Amazon S3 与 MinIO / OSS / COS 等兼容服务，MinIO 通过一致性套件）；`Messaging.Kafka` 不补（BasicApp 生产用 RabbitMQ）——此条后由 ADR-0019 撤销。P5-10 三个 Provider 已完成（ADR-0019）：`Messaging.Kafka`（队列 + 新定义的流能力：`xihankey` 分区键有序、消费组位点、`IMessageReplayer` 按时刻重放，`MessageStreamConformance`）、`Caching.Hybrid`（L1 对象 LRU + L2 共享缓存的两级缓存）、`Configuration.Nacos`（HTTP 接口直连，对 Nacos 2.5.1 通过一致性套件）。P5-11 调度与工作流的数据库 Provider 已完成（ADR-0020）：不引入 Quartz，`Scheduling.Abstractions` 重做为「引擎在抽象包、Provider 只实现 `IJobStore`」并补齐作业 / 触发器分离、运行时登记、Quartz 文法 cron、日历、misfire、暂停、历史、失联恢复，新包 `Scheduling.Database`；`Workflow.Database` 与工作流能力补齐（触发式启动、事件网关、外部任务、Http / Compensate、跳转 / 迁移 / 清理、认领 / 退回 / 催办、阈值与提醒）。P5-12 已完成（2026-10-26）：BasicApp 的 Workflow 模块接到 5.0——删除 4.x 契约副本，模块成为 `workflow.execution@1` 的显式 Provider（SqlSugar 存储 + 缓存租约锁 + 逐库轮询），应用层改走定义目录 / 引擎 / 任务服务，Saas 的审批业务契约随之改为引擎标识。P5-13 十项能力补齐已完成（2026-10-27，ADR-0022–0031）：SqlSugar 多库（实体按 `IEntityConnectionSelector` 落点，工作单元经 `AsTenant()` 跨连接提交或回滚）与分表（`[SplitTable]` 自动走分表入口，`EntityQuery<T>.Within` 时间范围提示）；Validate 阶段契约校验（内核 JSON Schema 子集校验器 + `IValidatableObject`，DataAnnotations 折进 Schema，宿主进管道前绑定载荷，Problem Details 带字段级 `errors` 与标量 `details`）；领域事件派发（工作单元登记写过的聚合，Transaction 阶段提交前经 `IDomainEventDispatcher` 发成 CloudEvents，发件箱时同事务）；`MultiTenancy.Database` / `Security.Database` / `Auditing.Database` 三个数据库 Provider；Resilience 阶段（`ResilienceMiddleware` 按操作 ID 施加熔断 / 固定窗口限流 / 并发上限，`XiHan:Kernel:Resilience` 可按操作覆盖，拒绝以 `KERNEL_UNAVAILABLE_002`–`004` 返回）；Provider 健康项（16 个 Provider 实现清单声明的 `contributes.health`，连通性探针经 `PeriodicHealthContributor` 周期探测，架构测试守护声明与实现一致）；OpenAPI 的 `x-xihan-permission` / `x-xihan-policy` 与 `components.x-xihan-permissions`。P5-14 雪花标识生成器已完成（2026-10-27，ADR-0033）：`Persistence.Abstractions` 的 `SnowflakeIdFeature` / `SnowflakeIdGenerator`（`IIdGenerator<long>`，41 / 10 / 12 位，epoch 缺省 `2026-01-01T00:00:00Z`），机器位 `XiHan:Persistence:Snowflake:WorkerId` 显式配置或经 `IDistributedLock` 租约分配（释放再获取续约，丢失即拒绝生成并以健康项 `persistence.snowflake.worker` 上报，不做随机），回拨 ≤ 100 ms 等待、超过 `PERSISTENCE_UNAVAILABLE_004`，没有机器位 `PERSISTENCE_UNAVAILABLE_005`，`SnowflakeId.Parse` 解析；`Persistence.Conformance` 新增 `IdGeneratorConformance<TKey>`。P5-15 Schema 命名修正已完成（2026-10-28，ADR-0034）：封闭泛型的根 `title` 与 `$defs` 定义名带实参名（`PageResultOfUserResponse`，多实参以 `And` 连接、嵌套递归），按字符串序列化的枚举进 `$defs/<枚举类型名>` 并以 `$ref` 引用，gRPC 对枚举定义仍映射 `string`、不建消息，`HttpRouteConvention` 按定义判定 `$ref` 是否嵌套，`OpenApiDocumentSource.FromServices` 公开与 `MapXiHanOpenApi` 相同的文档输入组装。下一项 P5-04 BasicApp 第一批。各阶段退出条件见 [`nextdocs/architecture/05-开发计划.md`](../nextdocs/architecture/05-开发计划.md)。
