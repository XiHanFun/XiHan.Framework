# ADR-0135 GA 前把实现细节改为 internal

## 状态

已采纳（修订 ADR-0075 中「`CapabilityAttribute` 保留」一项；规范 `rfcs/0002-manifest.md` 同步）。

## 日期

2026-11-11

## 背景

P5-05 的 API 审查列出了约 60 个只在本包或本包测试里使用、却是 `public` 的类型与成员（`plans/5.0-API审查.md` 2.7 节）。它们一旦进了 5.0.0 的基线，就会被包验证冻结：

- 公开的构造函数会把依赖列表冻住，引擎、目录、仓储以后每加一个依赖都算破坏。
- 公开的实现类会被应用直接构造或强转，绕开接口与特性。

`InternalsVisibleTo` 只授权给本包自己的 `<程序集>.Tests`，所以被别的正式包使用的类型只能保持公开（跨包共享 internal 的限制见 ADR-0134）。

## 决策

### 判定标准

公开类型或成员满足下面任一条就保持公开，否则改为 `internal`：

1. 被别的正式包、示例或 BasicApp 使用。
2. 文档写明的扩展点、替换点，或给应用用的辅助方法。
3. 作为别的公开成员的签名的一部分，比如公开构造函数的参数类型。

改为 `internal` 的类型去掉 `[ApiLevel]`。internal 类上的公开构造函数仍能被依赖注入激活；公开类要是只把构造函数改成 internal，需要改用工厂注册。

### 本批改动

| 范围 | 改为 internal 或删除 |
| --- | --- |
| 内核 | 删除 `CapabilityAttribute`（没有使用者，运行期不读，随清单生成器一起提供）；`CapabilityResolver`、`NullAuditSink` |
| 工作流 | `WorkflowEngine`、`WorkflowDefinitionCatalog`、`WorkflowTaskService`、`WorkflowExternalTaskService`；`ActivityRegistry` 的构造与 `Add`、`GetActivityRegistry`；`WorkflowInstance.JoinStates` 与 `WorkflowJoinState`（`[JsonInclude]` 保持实例文档的格式不变）；`Workflow.Database` 的 `DatabaseWorkflowInstanceLock`、`PollerLeadership`、`DueBookmarkPollingLoop`。引擎上原有的保留名常量移到新的公开类 `WorkflowReservedNames`，成员名不变 |
| 调度与配置 | `JobPlanner`、`InMemorySchedulingHealthContributor`、`NacosDocumentParser`；`KubernetesSchedulingOptions` 的 `ResolveNamespace` / `ResolveHolder` / `ResolveApiServer` / `LeaseNameFor`；`JobRegistry`、`JobTypeRegistry`、`JobCalendars` 的 `Add`（装配之后只读，只经 `AddJob`、`AddJobType`、`AddJobCalendar` 填充） |
| 持久化与本地化 | 三个 Provider 的仓储实现；`InMemoryUnitOfWork`、`InMemoryTable`、`InMemoryStore.Table`；`LeasedWorkerIdSource`；`RepositoryRegistrations.Items`；`MessageCatalog.ParseLocaleFile` |
| 缓存、消息与安全 | `CacheStoreDistributedCache.Translate`；`DomainEventRegistry`、`DomainEventRegistration`、`DomainEventDispatcher`、`GetDomainEventRegistry`；三个消息代理的 `ConnectAsync`、`IsConnected`，以及 `ProbeAsync` 或 `CheckConnection`；`OpenIdConnectMetadataCache.FailureRetryDelay` |
| AI | `AIChat`、`KnowledgeIngestor`、`KnowledgeRetriever`、`RagPromptAugmenter`、`AIProviderCatalog`、`AIToolCatalog`、`AIPromptCatalog` |
| 协议、宿主与对象存储 | `McpServer` 的构造、`HandleAsync` 与 `UnsupportedVersionReply`；`McpReply`；`McpStdioCommand`；命令行内置的 `GraphCommand`、`PipelineCommand`、`OperationsCommand`、`InvokeCommand`；`HttpRouteConvention.Derive` 的布尔重载；`OpenApiDocumentBuilder.Build` 的五参重载；`ProtoMessage` 的构造；`S3ObjectStorageFeature.CreateClient` |

### 核对后保持公开

| 对象 | 原因 |
| --- | --- |
| `JobSchedulerEngine` 的构造、`JobDispatcher` | 测试用 `ActivatorUtilities` 另建一个尚未启动的引擎，依赖注入只认公开构造函数；`JobDispatcher` 是这个构造函数的参数。留作 5.x 的 `Preview` 改形 |
| `KubernetesLeaseClient` | 文档写明应用或测试可以预先注册自己的实例替代它 |
| `HttpInvocation.ParseBaggage` / `FormatBaggage` | 文档推荐应用调用下游 HTTP 服务时用 `FormatBaggage` 生成 `baggage` 头 |
| `OperationAIFunction` | 构造函数本来就是 internal；应用经 `OperationId` 认出由操作桥接的工具 |
| `Protocols.Grpc` 的 `Proto` 编解码 | BasicApp 的 gRPC 对等冒烟测试用 `ProtoEncoder`、`ProtoDecoder` 与 `GrpcOperationTable` 发请求。本批只把外部构造永远是空消息的 `ProtoMessage` 构造改为 internal，稳定性级别在「级别调整与冻结」批次评估 |
| `McpServer.ProtocolVersion` | 改成支持多个版本、按版本协商是行为变化，不在本批 |
| `MessageSubscriptions.Add` | `InMemoryMessageBroker` 的公开构造函数接收它，测试与应用可以用它手动构造代理 |

## 影响

- 只经接口、特性与 `Add*` 扩展使用框架的代码不受影响。
- 直接用到上述类型或成员的代码编不过。迁移方式：
  - 实现类改用对应的接口，从容器取用。
  - 引擎保留名常量从 `WorkflowEngine.X` 改为 `WorkflowReservedNames.X`。
  - 登记活动、作业、作业类型与日历，用对应的 `Add*` 扩展。
- 受影响的包都还在 GA 之前，不需要登记豁免（ADR-0131）。
- BasicApp 升级到包含本批的框架版本时，需要把 `WorkflowEngine.LastErrorVariable` 改为 `WorkflowReservedNames.LastErrorVariable`。
