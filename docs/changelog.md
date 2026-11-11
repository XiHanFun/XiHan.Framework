# 更新日志 · XiHan Framework

本页记录 XiHan Framework 各版本中使用者可感知的变更，每条标注 **新增 / 修复 / 优化 / 调整 / 移除** 类别；仓库自身的 CI、测试工程与构建脚本不列入。

## 未发布

### 版本与兼容

- **调整** 稳定性级别的承诺：`Stable` 大版本内不破坏；`Preview` 与 `Experimental` 可能在小版本变更，补丁版本不变，每次变更在本页写明迁移方式；要求升级不改代码就只用 `Stable` 的 API（见 [稳定性分级](/getting-started/concepts#稳定性分级)）

### 原语与内核

- **移除** `CapabilityAttribute`：没有使用者，运行期也不读，随清单生成器一起提供；`CapabilityResolver` 与 `NullAuditSink` 不再公开，能力求解经 `AddXiHan` 装配，结果从 `ResolvedGraph` 读取
- **调整** `OperationContract` 构造只收 ID，其余字段改为初始化器：手写的 `new OperationContract(id, stability, expose, policy, permission, readOnly)` 改成 `new OperationContract(id) { Stability = …, Expose = …, Policy = …, Permission = …, IsReadOnly = … }`；用生成器的项目重新编译即可，生成的 `GeneratedOperations` 变为 internal
- **调整** `Expose.All` 冻结为 5.0 的五个协议：以后新增的协议不并入 `All`，要在新协议上发布须显式开启；现有行为不变
- **调整** 清单记录的可选字段改为初始化器：`new Binding(name, type) { Required = false }`、`new CapabilityRequirement(id, version) { Optional = true }`、`new ConfigContribution(section) { Schema = … }`、`new PipelineContribution(middleware, stage) { Before = …, After = … }`（`PipelineContribution` 不再是位置记录）
- **调整** `Error.Unexpected` 的参数顺序改为 `(code, messageKey, details, inner)`，与构造函数一致；按位置传 `inner` 的写成 `inner: …`
- **修复** `CapabilityDeclaration.TryParse` 与 `CapabilityRequirement.TryParse` 的输出标注为可空（`[NotNullWhen(true)]`），失败时为 `null`
- **修复** `InvocationContext.Baggage` 构造时复制成只读副本，调用方之后改原字典不再影响上下文；`Items` 在派生上下文之间共享，并行写入时不再损坏
- **调整** 匿名主体的 `Principal.Id` 由空字符串改为 `null`，`new Principal(id, scheme)` 只创建已认证主体（两者都不得为空），匿名用 `Principal.Anonymous`；判断 `IsAuthenticated` 之后可直接用 `Id`。`ResourceName.Tenant` 的全局资源由空字符串改为 `null`，构造时校验每一段
- **调整** 内核的数据类型不再是位置记录：`AuditEvent`、`HealthSnapshot`、`OperationResponse`、`FeatureStatus` 按属性名写初始化器（`new AuditEvent { Timestamp = …, Action = …, Outcome = …, TraceId = … }`）；`AuthorizationRequest`、`ContributorStatus`、`PluginSource` 只保留两参构造，第三个实参改成初始化器（`new ContributorStatus(name, state) { Error = … }`）；这些类型都不再能解构

### 子系统

- **新增** `Messaging.Redis`：用 Redis Streams 提供 `messaging.queue@1`，只有 Redis 的部署也能跨进程收发消息；需显式 `UseFeature<RedisMessagingFeature>()`，配置节 `XiHan:Messaging:Redis`，可选绑定 `queue`（类型 `cache:distributed`），需要 Redis 6.2+，不提供 `messaging.stream@1`（见 [Messaging.Redis](/packages/messaging-redis)）
- **修复** 工作流启动请求不再公开 `InstanceId`、`ParentInstanceId`、`ParentNodeInstanceId` 与 `Depth`：调用方原先能借它们伪造子流程的父链接、绕过嵌套深度上限；这几个字段只由引擎为子流程设置，给它们赋值的代码删掉即可，要幂等启动用 `CorrelationId` 与 `ReusePolicy`
- **修复** 活动、结果处理器与指派规则拿到的 `Instance`、`NodeInstance` 改为只读视图 `WorkflowInstanceView` / `WorkflowNodeInstanceView`：原先交出的是引擎随后写回的对象，扩展代码改了状态或租户会被持久化；只读的代码不用改，要读变量与私有状态改用上下文的 `Variables` 与 `State`
- **调整** 工作流的引擎、定义目录、任务与外部任务服务的实现类型，`ActivityRegistry.Add` 与 `GetActivityRegistry`，实例的 `JoinStates` 与 `WorkflowJoinState`，以及 `Workflow.Database` 的实例锁与轮询类型不再公开：经 `IWorkflowEngine` 等接口与 `AddActivity<TActivity>` 使用；`WorkflowEngine.LastErrorVariable` 等常量移到 `WorkflowReservedNames`，成员名不变，只改类名
- **调整** 调度与配置的实现细节不再公开：`JobPlanner`、`InMemorySchedulingHealthContributor`、`NacosDocumentParser`，以及 `KubernetesSchedulingOptions` 的 `ResolveNamespace` / `ResolveHolder` / `ResolveApiServer` / `LeaseNameFor`；它们只在各自的包里用
- **调整** `JobRegistry`、`JobTypeRegistry`、`JobCalendars` 的 `Add` 不再公开：装配之后只读，登记作业、作业类型与日历用 `AddJob`、`AddJobType`、`AddJobCalendar`
- **调整** 持久化、本地化、缓存与消息的实现类型不再公开：三个 Provider 的仓储实现、`InMemoryUnitOfWork`、`InMemoryTable` 与 `InMemoryStore.Table`、`LeasedWorkerIdSource`、`RepositoryRegistrations.Items`、`MessageCatalog.ParseLocaleFile`、`CacheStoreDistributedCache.Translate`、领域事件的登记表、映射与派发器、三个消息代理的连接与探测成员、`OpenIdConnectMetadataCache.FailureRetryDelay`；照常经 `IRepository`、`IUnitOfWork`、`AddDomainEvent<TEvent>` 等入口使用
- **调整** `IPermissionGrantStore` 改为批量读取 `GetManyAsync(subjects, tenantId)`，权限判定把用户与全部角色一次读出；实现授予存储的 Provider 把 `GetGrantsAsync` 换成 `GetManyAsync`，单主体的 `GetGrantsAsync` 仍可调用（扩展方法）
- **调整** `ITenantStore.FindAsync` 返回 `Result<Maybe<TenantInfo>>`，存储不可达是 `MULTITENANCY_UNAVAILABLE_003`（Tenant 阶段以 503 结束，不再当作 500 抛出）；`ListAsync(TenantQuery)` 改为分页，逐页读到 `ContinuationToken` 为空；自己实现租户存储的 Provider 改这两个方法
- **调整** `ITenantResolver.ResolveAsync` 返回 `Result<Maybe<string>>`，自定义解析器可以返回失败拒绝调用（以该错误结束，后面的解析器与默认租户不再顶替）；实现解析器的应用把返回值包进 `Result.Success(…)`
- **调整** `IMessageLocalizer` 只留 `Options`、`Find`、`Resolve(key, locale, arguments)`，其余 `Localize` / `Resolve(error, …)` 改为 `MessageLocalizerExtensions` 的扩展方法：调用方不用改，自己实现本地化器的 Provider 删掉这 5 个成员
- **调整** `IAuditSink.WriteAsync` 的契约写明不抛出：接收不下或落地失败时由实现自己丢弃、计数并告警，返回只表示已接收；自己实现接收器的 Provider 需通过一致性套件新增的用例
- **移除** `XiHan:MultiTenancy:Database:MaxListedTenants`：租户列表分页之后不再截断，删掉这个配置键
- **调整** `MessageDelivery` 改为初始化器（`new MessageDelivery { Topic = …, Envelope = …, Attempt = …, Mode = … }`），`MessageSubscription` 只保留 `(topic, mode)` 构造；`IdempotencyRecord` 不再公开，记录格式与回放不变
- **调整** `UnitOfWorkOptions` 改为初始化器：`new UnitOfWorkOptions { TenantId = …, IsReadOnly = true, Connection = … }`；`IUnitOfWork.TenantId` / `IsReadOnly` 改为 `unitOfWork.Options.TenantId` / `Options.IsReadOnly`，自己实现工作单元的 Provider 实现 `Options`

### 宿主、协议与插件

- **调整** AI、协议与宿主的实现类型不再公开：AI 的对话、知识库摄入与检索、提示增强、Provider / 工具 / 提示目录的默认实现，`McpServer` 的构造与 `HandleAsync`、`McpReply`、`McpStdioCommand`，命令行内置的 `graph` / `pipeline` / `operations` / `invoke` 命令，`HttpRouteConvention.Derive` 的布尔重载、`OpenApiDocumentBuilder.Build` 的五参重载，`ProtoMessage` 的构造与 `S3ObjectStorageFeature.CreateClient`；经接口与特性使用的写法不变
- **调整** `MapXiHanOperations()` 返回 `IEndpointConventionBuilder` 而不是端点数，可以在返回值上给全部操作端点统一加 CORS、限流等约定
- **调整** `ProblemResponse`、`HealthResponse`、`FeatureHealth` 改为初始化器，按属性名赋值；响应的 JSON 不变
- **修复** `Plugins.OutOfProcess`、`Plugins.Wasm`、`Plugins.Conformance` 对 `Plugins.Protocol`（以及 Conformance 对两个宿主）改为精确版本依赖：原先单独升级其中一个包，还原能通过，运行时才缺成员；这几个包要用同一版本

## v5.0.0-preview.2（2026-11-06）

::: warning 预览须知
- 这是预览版，安装要带 `--prerelease`；公开面多数标为 `Preview` 或 `Experimental`，正式版前仍可能调整
:::

### 宿主、协议与插件

- **修复** `invoke` 的字段选项按操作的请求 Schema 写成布尔、数字与数组，`--times 2` 进载荷是 `"times":2`；操作没有 Schema 时值仍是字符串（见[命令行](/protocols/command-line)）

### 包

- **优化** 全部包的描述与 README 重写，NuGet 包恢复图标并附更新日志链接

## v5.0.0-preview.1（2026-11-05）

::: warning 预览须知
- 这是预览版，安装要带 `--prerelease`；公开面多数标为 `Preview` 或 `Experimental`，正式版前仍可能调整
:::

### 原语与内核

- **新增** 原语层 `Primitives`：`Result` / `Result<T>`、`Error` 与错误码文法、`Maybe<T>`、`TypedId`、`InvocationContext`、清单模型、`PipelineStage`、`LifecycleState`、`Stability` 与 `[ApiLevel]`，只增不改
- **新增** 内核 `Kernel`：`AddXiHan` + `UseFeature<T>()` 装配特性，按能力 ID 求解能力图谱，`XiHan:Providers:<能力 ID>` 或 `UseProvider` 选择 Provider，启动日志打印图谱与管道
- **新增** 十一个阶段的执行管道与内置中间件：截止时间、按操作 ID 的熔断 / 限流 / 并发上限（`XiHan:Kernel:Resilience`）、按 JSON Schema 与 `IValidatableObject` 的契约校验
- **新增** 操作契约与调用器：`[Operation]`、`IOperation<TRequest, TResponse>`、`OperationRegistry`、`IOperationInvoker`；`Kernel.Generators` 生成登记代码与请求 / 响应 JSON Schema（`XHG001`–`XHG006`），程序集级 `OperationDefaults` 与只读声明
- **新增** 九态生命周期、`/health/startup|ready|live` 探针、`IHealthContributor` 与周期探测的 `PeriodicHealthContributor`，健康条目带说明、检查时刻、耗时与标量数据
- **新增** 资源绑定（`XiHan:Bindings`、`SERVICE_BINDING_ROOT`）与配置目录 `XIHAN_CONFIG_PATH` 热更新
- **新增** `IInvocationAccessor` 与调用来源 `InvocationOrigin`（协议、请求目标、远端地址、User-Agent、端点路由与名字）
- **新增** 分析器 `Kernel.Analyzers`（XH0001–XH0005）与测试基座 `Testing`
- **新增** `OutboundAddressPolicy.ConnectAsync(host, port, ct)`：不是 HTTP 但地址来自用户的连接（SMTP）用同样的规则，返回连好的 `Socket`
- **修复** `MapXiHanBlobs` 的响应带 `X-Content-Type-Options: nosniff`，写出内容时除 PDF 外带沙箱的 `Content-Security-Policy`，用户上传的 HTML、SVG 不能在应用的源上运行脚本
- **修复** 载荷解码错误的 `reason` 只写 JSON 路径，不再把请求类型的全名回给调用方
- **修复** SqlSugar 的数据库健康项与运行时数据源探测走一次数据库往返并按配置超时：连接池里有空闲连接时不再在库宕了之后报健康，服务器不回话时不再卡住启动；`persistence.sqlsugar.database` 的错误码由 `PERSISTENCE_UNAVAILABLE_003` 变成 `KERNEL_UNAVAILABLE_005`；`TestAsync` 单次上限是运行时数据源的 `HealthProbeIntervalSeconds`
- **修复** 配了从库时授予与租户目录读主库，撤销授予、停用租户的生效时间不再多出复制延迟；新增 `UnitOfWorkOptions.ReadsPrimary`
- **调整** HTTP、gRPC、MCP 入站丢弃 Baggage 里的 `xihan.tenant` 与 `xihan.principal`，这些调用方的租户只来自认证（CloudEvents 入站不变，端点权限由宿主自己加）；要按主机名或请求头选租户时登记自己的 `ITenantResolver`
- **新增** 出站地址策略 `OutboundAddressPolicy`：工作流 `Http` 活动、CloudEvents Webhook 与 S3 命名实例缺省只能连公网地址，连接时按实际 IP 判定；连本机或内网要在 `XiHan:Security:Outbound` 放行，本地开发写 `AllowLoopback: true`（见[出站地址策略](/security/outbound)）

### 子系统

- **新增** 本地化：消息键与 `key.L()`、每种语言一个 `Localization/<区域>.json`、内置 10 种区域（默认 `en-US`）、宿主按 `Accept-Language` 翻译 Problem Details；`Localization.InMemory` 与 `Localization.Database`
- **新增** 领域模型 `Domain`：实体协议接口、值对象、领域事件缓冲与规约
- **新增** 持久化：仓储、查询、工作单元与 Transaction 阶段、批量方法、雪花标识生成器；`Persistence.InMemory`、`Persistence.EntityFrameworkCore`、`Persistence.SqlSugar`（多库、分表、差异日志）与 `Persistence.Generators`
- **新增** SqlSugar 读写分离：只读工作单元读从库，其余读写走主库；从库权重归一化与周期探测，不可读的自动摘除、全部不可读时读回到主库，健康项 `persistence.sqlsugar.replicas`（见[持久化](/persistence/sqlsugar)）
- **新增** 缓存：字节存储、类型化缓存、分布式锁、Idempotency 阶段、`IDistributedCache` 适配、键管理；`Caching.InMemory`、`Caching.Redis`、`Caching.Hybrid`、`Caching.Remote`（Experimental）
- **新增** 多租户：租户解析与 Tenant 阶段、租户分片与管道外的 `TenantShardScope`；`MultiTenancy.InMemory`、`MultiTenancy.Database`
- **新增** 安全：认证处理器与 Authenticate 阶段、权限与策略、RBAC 授予、权限收窄过滤器、授予来源、条件引擎、IP 归属地；`Security.InMemory`、`Security.Database`、`Security.Jwt`、`Security.OpenIdConnect`、`Security.Ip2Region`
- **新增** 消息：CloudEvents 信封、发布与订阅（竞争消费与每个实例都收到的广播订阅）、经管道的派发、重试与死信、事务性发件箱、领域事件到消息的登记；`Messaging.InMemory`、`Messaging.RabbitMQ`、`Messaging.Kafka`（流与按时刻重放）
- **新增** 对象存储：批量写入与删除、按前缀列举、范围读取、分片上传、预签名、命名存储实例；`ObjectStorage.FileSystem`、`ObjectStorage.S3`
- **新增** 调度：作业与触发器、间隔 / cron / 一次 / 手动计划、日历、misfire、集群分发与失联恢复、领导选举、重试与执行时限；`Scheduling.InMemory`、`Scheduling.Database`、`Scheduling.Kubernetes`
- **新增** 远程配置与密钥：`Configuration.Abstractions` 的配置源（轮询重载、失败保留最后一份）与 `Configuration.Nacos`
- **新增** 审计：Observe 阶段审计中间件与查询契约；`Auditing.InMemory`、`Auditing.Database`
- **新增** 可观测性：`Observability.OpenTelemetry` 导出追踪、指标与日志，`Observability.Host` 读取进程与主机运行信息
- **新增** 工作流：JSON 定义模型、定义目录、引擎、内置活动、用户任务、补偿与事件；`Workflow.InMemory`、`Workflow.Database`
- **新增** AI：基于 `Microsoft.Extensions.AI` 的 Provider 目录、`IAIChat`、护栏、操作桥接成模型工具、知识库流水线；`AI.OpenAI`、`AI.VectorData`
- **新增** 每个领域的一致性套件 `<领域>.Conformance`，默认实现与 Provider 通过同一套用例

### 宿主、协议与插件

- **新增** 宿主 `Hosting.AspNetCore`（请求桥接、探针、本地化的 Problem Details、绕过管道端点的 `XiHan` 认证方案）与 `Hosting.CommandLine`（`help` / `graph` / `pipeline` / `operations` / `invoke`，按错误 KIND 的退出码）
- **新增** 协议适配器 `Protocols.Http`（路由推导、OpenAPI 3.1 与权限扩展）、`Protocols.Grpc`（由 Schema 推出 protobuf、`/operations.proto`）、`Protocols.Mcp`（Streamable HTTP 与 stdio）、`Protocols.CloudEvents`（入站 `/events` 与出站 Webhook）
- **新增** 插件协议 `xihan.plugin.v1` 与三种宿主：`Plugins.InProcess`、`Plugins.OutOfProcess`、`Plugins.Wasm`（Experimental），黑盒套件 `Plugins.Conformance`
- **新增** 组合包 `Profiles.Minimal`、`Profiles.Standard`、`Profiles.Full`
- **新增** 命令行工具 `xihan`：`pack` 从 `xihan.app.json` 生成 Dockerfile、Kustomize、Compose 与 Aspire 产物，`graph` / `pipeline` / `operations` / `invoke` 转发给应用
