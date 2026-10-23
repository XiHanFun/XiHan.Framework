# 06 BasicApp 迁移清查

> 日期：2026-10-23。对象：`XiHan.BasicApp` `dev` 分支（`34f127757`，v5.9.1）的 `backend/src`。目的：P5 开始前弄清 BasicApp 到底依赖 4.x 的哪些能力，按"5.0 已有 / 不再包装、换原生 / 必须补"分类，据此调整 P5 的顺序与范围。
>
> 方法：扫 9 个项目的 csproj 引用、`using XiHan.Framework.*`、类的基类型、特性与接口的命中数；数字是 grep 结果，用来看量级，不是精确统计。

## 1. 总览

| 项目 | 文件 / 行 | 角色 | 用到的 4.x 命名空间 |
| --- | --- | --- | --- |
| `XiHan.BasicApp.Core` | 5 / 309 | 公共基础层，`XiHanBasicAppCoreModule` 一次 `[DependsOn]` 了 38 个框架模块 | 几乎全部（其中 13 个只在 `DependsOn` 出现，代码里没用） |
| `XiHan.BasicApp.Web.Core` | 2 / 219 | Web 基础层 | Web.Core / Api / Docs / RealTime / Gateway / Mcp（无 Grpc 使用） |
| `XiHan.BasicApp.WebHost` | 5 / 314 | 宿主：`AddApplicationAsync<XiHanBasicAppWebHostModule>()` | Core、Data、Web、Bot、Utils |
| `XiHan.BasicApp.Saas` | 1375 / 149 338 | 主体：租户、RBAC、用户、日志、通知、文件、升级…… | Application、Domain、Data、Uow、Authorization、MultiTenancy、Caching、EventBus、Tasks、Auditing、Localization、ObjectStorage、Templating、Bot、Messaging、Upgrade、Workflow、Web |
| `XiHan.BasicApp.CodeGeneration` | 106 / 13 376 | 代码生成 | 同 Saas 的核心集合 + Localization |
| `XiHan.BasicApp.Workflow` | 59 / 7 325 | 工作流 | 核心集合 + **Workflow.Abstractions**、Timing |
| `XiHan.BasicApp.AI` | 65 / 6 051 | AI 助手、提示词、RAG | 核心集合 + **AI.Abstractions** |
| `XiHan.BasicApp.Chat` | 42 / 5 650 | 聊天 | 核心集合 + EventBus、**Web（SignalR Hub）** |
| `XiHan.BasicApp.Printing` | 36 / 3 286 | 打印 | 核心集合 + Caching |

"核心集合" = `Application`、`Domain`、`Data`、`Uow`、`Authorization`、`Security`、`MultiTenancy`、`Core`；每个模块都是一个 `XiHanModule`，共 9 个，依赖链 `WebHost → 六个模块 → Saas → Core / Web.Core`。

宿主 Development 配置启用的框架节：`Observability`、`DistributedIds`、`Authentication`（含微信 / 企业微信 / 飞书 / 钉钉扫码登录）、`Data`（SqlSugar，PostgreSQL，读写分离）、`Upgrade`、`Caching`、`Web`（签名、加密、CORS 等）、`Localization`、`VirtualFileSystem`、`ObjectStorage`；没有 `EventBus` 节（走本地事件，Kafka / RabbitMQ / Redis 只在引用与 Production 配置里出现）。

## 2. 按能力清查

阻塞等级：**硬**＝5.0 没有对应能力且 BasicApp 大量使用；**中**＝有对应能力但形态差异大或缺 Provider；**低**＝代码修复或少量手改即可；**无**＝不用动或 BasicApp 本来就没用。

### 2.1 已有 5.0 对应物

| 4.x 用法 | 命中 | 5.0 去处 | 手段 | 阻塞 |
| --- | --- | --- | --- | --- |
| `XiHanModule` + `[DependsOn]`（9 个模块，Core 依赖 38 个框架模块） | 9 / 9 | 特性 + 清单 `requires` | `Compat.V4`（P5-01）过渡，`xihan migrate v4` 生成特性 | 低 |
| `[DynamicApi]` + `IApplicationService` / `ApplicationServiceBase` + 各模块的 `XxxApplicationService` 基类 | 165 / 154 / 38 | 操作契约 `[Operation]`（一方法一操作），HTTP / MCP 由契约推出 | 代码修复：每个公开方法拆成操作类，请求 / 响应 DTO 复用（P5-02） | 中：Saas 有 55 个 AppService + 99 个 QueryService + 72 个 Service，方法数以数百计 |
| 实体基类 `BasicAppFullAuditedEntity` 44、`BasicAppCreationEntity` 33、`BasicAppAggregateRoot` 11、`BasicAppEntity` 3，接口 `IStrictMultiTenantEntity` 57、`ITraceableEntity` 8、`ISplitTableEntity` 10 | 166 | `Domain` 的接口协议组合（`IEntity<TKey>`、审计 / 软删除 / 租户接口） | 代码修复；BasicApp 的基类本身可以保留为应用层基类，只换它们实现的框架接口 | 低 |
| `[UnitOfWork]` 287、`IUnitOfWorkManager` 32 | 319 | Transaction 阶段中间件（每个操作默认一个工作单元） | 删特性；手动开启的 32 处改为显式工作单元 API | 低 |
| `[PermissionAuthorize]` 501、`[Authorize]` 131、`IPermissionChecker` 20 | 666 | 契约的 `Permission` / `Policy` + Authorize 阶段 | 代码修复：特性参数进 `[Operation]`；`IPermissionChecker` 改为 `IAuthorizationService` | 低 |
| `ICurrentTenant` 166、`ICurrentUser` 89 | 255 | `InvocationContext` 的 Baggage（`xihan.tenant`）与 `SecurityContext` | 代码修复 + 应用层薄包装（保留 `ICurrentUser` 名字做适配） | 低 |
| 本地事件 `ILocalEventHandler<T>` 19、`ILocalEventBus` 14、`DomainEventBase` 6 | 87 | `Domain` 领域事件 + `Messaging` 处理器 | 处理器签名改写；发件箱语义变化要核对 | 低 |
| 缓存 `IDistributedCache` 94（.NET 原生接口）+ 自建失效器 `ISaasCacheInvalidator` 62 | 101 | `ISharedCache` / `ICache` | 两条路：给 5.0 加一个把 `IDistributedCache` 架在 `ICacheStore` 上的适配器（小），或改调用点 | 低 |
| 后台任务 `BackgroundServiceBase` 10、`IBackgroundTaskItem` 13、`DynamicJobWorker` 8、`IJobWorker` 6 | 71 | `Scheduling.*`（手动 / 间隔 / cron 作业，领导租约） | 改写为 `AddJob<TJob>`；动态作业（运行期按租户配置生成）要核对 `IJobScheduler` 是否够 | 中 |
| 审计 `Auditing` 使用 16 处 using（无框架审计特性命中，主要是实体审计字段） | 16 | `Auditing.*` + 实体审计接口 | 少量 | 低 |
| 本地化 `LocalizableString` 13；宿主 `Localization/*.json`（ApiResponse / Enums / Errors × 7 区域，含 `hi-IN`） | 13 | `Localization.*`（消息键 + `Localization/<区域>.json`） | 资源文件转换器（键名与模板占位符格式不同）；`hi-IN` 不在内置 10 种里，应用自加 | 中 |
| 对象存储：BasicApp 自己的 `IFileStorageProvider` / `IStorageProviderResolver` 抽象 + 框架 `ObjectStorage` | 8 + 30 | `ObjectStorage.Abstractions` / `FileSystem` | 应用抽象映射到 `IBlobStore`；云存储 Provider（S3 / OSS）5.0 还没有 | 中 |
| `IClock` 14 | 14 | `TimeProvider`（已有 33 处直接用） | 代码修复 | 无 |
| `IScopedDependency` 13、`ITransientDependency` 3、`ISingletonDependency` 3 | 19 | 显式注册 / 生成器 | 代码修复 | 无 |
| `IValidatableObject` 20、`[Required]` 19（DataAnnotations） | 39 | Validate 阶段（原生校验） | 不动 | 无 |
| 分布式 ID（配置节 `DistributedIds`，代码无直接命中） | 0 | `Persistence` 的 `IIdGenerator<T>` | 核对实体 ID 由谁生成（可能在 SqlSugar 实体基类里） | 低 |
| 认证：JWT + 微信 / 企业微信 / 飞书 / 钉钉扫码登录（配置 `Authentication:*`，`Infrastructure/OAuth` 3 文件） | — | `Security.Jwt`、`Security.OpenIdConnect` | 国内 OAuth 不是 OIDC：应用层保留自己的 OAuth 实现，接到 `security.authentication` | 中 |
| 观测、Web 基础（CORS、签名 `HMACSHA256`、内容签名、`AES-CBC` 加密中间件） | 配置节 | `Observability.OpenTelemetry`、`Hosting.AspNetCore` + `Protocols.Http` | 签名 / 加密是 4.x `Web.Core` 的中间件，5.0 没有对应：应用层自写 Ingress / Egress 阶段中间件 | 中 |

### 2.2 不再包装，换原生或应用层

| 4.x 用法 | 命中 | 结论 |
| --- | --- | --- |
| SignalR：`XiHanHub` 2（Chat、Saas）、`IHubContext` 36 处 / 21 文件 | 36 | 5.0 不包装实时通信（5.x P1 才有网关 / 实时包）：BasicApp 直接用 ASP.NET Core SignalR，`Hosting.AspNetCore` 不拦；只需把 Hub 里取租户 / 用户的方式换成 5.0 上下文 |
| `Web.Gateway`：`GatewayResolver` / `GatewayRequest` 8 处 / 5 文件 | 8 | 用途是请求转发；直接用 YARP 或应用层中间件 |
| 模板 `TemplateEngine` 55（通知模板、代码生成） | 65 | 直接用 Scriban / Fluid |
| `Upgrade`：`IsUpgrading`、`UpgradeNode`、`VersionUpgradeStart/FinishCommand`、`UpgradeMigrationHistory`、`SaasUpgradeLockProvider` | 130 / 19 文件 | 版本升级编排是应用关切：迁到 BasicApp 自己的升级模块（或孵化区包），用 `Scheduling` 的领导租约做升级锁 |
| Bot：Telegram / Email / Sms / DingTalk / WeCom / Lark 的 Abstractions、Options、密钥保护 | 77 / 27 文件 | 不进框架：抽成孵化区包 `XiHan.Bots.*` 或 BasicApp 内部实现 |
| `VirtualFileSystem` 2、`Http` 2、`Serialization` 0、`Threading` 0、`ObjectMapping` 0（BasicApp 自写 58 个 Mapper）、`Script` 0、`SearchEngines` 0、`Settings` 0、`Logging`（Serilog 直接用） | ≤ 2 | 只在 `DependsOn` 里出现，删引用即可 |
| `Metadata` 141 处 | — | 是 BasicApp 自己的"元数据目录"功能（`MetadataCatalog`、`MetadataQueryService`），不是框架包 |

### 2.3 必须补（5.0 现在没有）

| 4.x 用法 | 命中 | 缺口 | 建议 |
| --- | --- | --- | --- |
| **SqlSugar**：`ISqlSugarRepository`、`Queryable` / `Insertable` / `Updateable` / `Deleteable`、`SugarTable` / `SugarColumn`、分表、读写分离，PostgreSQL | **1 975 / 326 文件** | 5.0 只有 EF Core；`IRepository<T>` 0 命中，数据层全是 SqlSugar 原生 API | `Persistence.SqlSugar`（P5-03）从"可选"升为**前置必做**：改成 EF Core 等于重写 Saas 的 66 个仓储加 326 个文件，不现实 |
| **Workflow**：`Workflow.Abstractions` 的 Runtime / Definitions / Stores / Exceptions，`IWorkflowInstanceStore`、`IWorkflowInstanceRepository`、`WorkflowStoreMapper` | **1 237 / 81 文件** | 5.0 没有 `Workflow.*`（计划 5.x P0） | 硬阻塞。两个选项：A）把 `Workflow.*` 提前到 P5；B）BasicApp.Workflow 模块随 5.1 迁，P5 的"全量跑在 5.0"改为"核心模块跑在 5.0"。 |
| **AI**：`AI.Abstractions`（`IXiHanAiService`、`IAiChatClientResolver`、`IAiSkill`、`Rag`） | 77 / 17 文件 + 模块 6 051 行 | 5.0 没有 `AI.*`（计划 5.x P0） | 硬阻塞，选项同上；5.0 的 AI 子系统基于 `Microsoft.Extensions.AI`，BasicApp.AI 可先直接用 M.E.AI 过渡 |
| 租户分库：`ITenantConnectionCacheInvalidator`、`Infrastructure/MultiTenancy`、`Tenancy` | 8 + 3 文件 | `multitenancy.sharding` 只有能力 ID，没有实现 | 补 `MultiTenancy.Abstractions` 的分片契约 + SqlSugar / EF Core 的按租户连接；规模 M |
| 分布式事件 Kafka / Redis（Production 配置） | 引用 | 5.0 只有 `Messaging.RabbitMQ` | 看生产真正用的是哪个：用 Kafka 就补 `Messaging.Kafka`，用 Redis 建议改 RabbitMQ 而不是补 Provider |
| 云对象存储 | 配置节 | 5.0 只有 `FileSystem` | 补一个 `ObjectStorage.S3`（兼容 OSS / MinIO）；规模 M |

## 3. 对 P5 的影响

1. **`Persistence.SqlSugar` 是前置条件**，不是可选项：没有它 BasicApp 的 Saas 模块一行都跑不起来。顺序改为 P5-03 → P5-01（Compat.V4）→ P5-02（migrate）→ P5-04。
2. **Workflow 与 AI 是硬阻塞**，需要拍板：A）提前实现 `Workflow.*`、`AI.*`（各自至少一个 L 级任务，GA 推迟）；B）P5-04 分两批——第一批 Saas、CodeGeneration、Printing、Chat 四个模块跑在 5.0（Chat 的 SignalR 直接用 ASP.NET Core），第二批 Workflow、AI 随 5.1 的子系统一起迁，期间经 `Compat.V4` 过渡或保持在 4.x 维护线。建议 B，并把 7.2 的退出条件改成"BasicApp 核心模块全量跑在 5.0；Workflow、AI 模块有明确的 5.1 迁移计划"。
3. **新增 P5 条目**（都是 M 以下）：租户分库（`multitenancy.sharding`）；`IDistributedCache` 到 `ICacheStore` 的适配器；4.x 本地化资源到 `Localization/<区域>.json` 的转换器（并入 `xihan migrate v4`）；`ObjectStorage.S3`；按生产配置决定是否补 `Messaging.Kafka`。
4. **明确不进框架、需要 BasicApp 自己承接的**：SignalR Hub、网关转发、模板引擎、版本升级编排、Bot 渠道、请求签名 / 加密中间件、国内 OAuth 扫码登录。迁移指南（P5-07）要逐项给替代写法；Bot 与升级编排可以考虑孵化区包。
5. **代码修复器的范围**（P5-02）按命中数排优先级：`[DynamicApi]` → 操作契约（165 处，方法级拆分是最大工作量）、`[PermissionAuthorize]` → 契约权限（501）、`[UnitOfWork]` 删除（287）、实体基类接口替换（166）、`ICurrentTenant` / `ICurrentUser` 替换（255）、DI 约定接口（19）、`IClock`（14）、本地事件处理器签名（19）。
6. **工作量感受**：Saas 占 BasicApp 的 78%（149k 行）；在保留 SqlSugar 的前提下，数据层不动，主要工作在应用层（AppService → 操作）与横切（权限、租户、事件、缓存），代码修复能覆盖其中大半，剩下的是逐模块手工核对。

## 4. 引用了但没用的 4.x 包

`Castle`、`Http`、`Serialization`、`Threading`、`Settings`、`Script`、`SearchEngines`、`ObjectMapping`、`Logging`、`DistributedIds`（配置节有、代码无命中）、`Validation`（只有 DataAnnotations）、`VirtualFileSystem`（2 处）、`Observability`（配置节）、`Web.Grpc`（引用、无使用）、`Web.Mcp`（引用、无使用）。清理 `XiHanBasicAppCoreModule` 的 `DependsOn` 就能去掉这 15 个引用，迁移前先做，减少噪音。

## 5. 批量优先：5.0 抽象的现状与缺口

AGENTS.md 的"批量优先"要求仓储、缓存等对集合的操作提供原生批量方法，不在调用方循环单条。5.0 各抽象的现状（2026-10-23）：

| 抽象 | 现状 | 缺口与要求 |
| --- | --- | --- |
| `ObjectStorage.Abstractions` `IBlobStore` | 已批量优先：`PutAsync(uploads)`、`DeleteAsync(keys)` 逐项结果 | 无 |
| `Scheduling.Abstractions` `IJobScheduler` | 已批量优先：`TriggerAsync(names)` 逐项结果 | 无 |
| `Localization.Database` `MessageTranslationStore` | 已批量增改删 | 无 |
| `Persistence.Abstractions` `IRepository<TEntity, TKey>` | 只有单条 `AddAsync` / `UpdateAsync` / `RemoveAsync`；EF Core 实现没有 `AddRange` / `ExecuteUpdate` / `ExecuteDelete` | **要补** `AddRangeAsync` / `UpdateRangeAsync` / `RemoveRangeAsync`（以及按查询批量删除 / 更新），逐项结果或汇总错误、空集合与重复项语义、取消时机写进 RFC-0005；EF Core 用 `AddRange` + `ExecuteUpdateAsync` / `ExecuteDeleteAsync`，SqlSugar 用 `Insertable(list)` / `Updateable(list)` / `Deleteable` 批量；InMemory 原生批量；套件 `Persistence.Conformance` 加批量用例；单条改为批量的特例 |
| `Caching.Abstractions` `ICacheStore` / `ICache` / `ISharedCache` | 只有单键 `Get` / `Set` / `TryAdd` / `Remove` / `TryRemove` | **要补** `GetManyAsync(keys)` / `SetManyAsync(entries)` / `RemoveManyAsync(keys)`，Redis 用 `MGET` / `MSET` / 管道，InMemory 原生，`SerializingCache` 对应泛型批量；RFC-0006 与 `CacheStoreConformance` 同步；远程 Provider 操作集加 `cache-entries.get-many` / `set-many` / `remove-many`（RFC-0019 的未决项顺势定下），Go SDK `caching.Store` 同步 |
| `Messaging.Abstractions` `IMessagePublisher` | 只有单条 `PublishAsync(topic, envelope)`；插件协议的 `Host.Publish` 倒是批量的 | **要补** `PublishAsync(topic, envelopes)` 逐项结果，RabbitMQ 用发布确认的批量，发件箱批量入库；RFC-0009 与 `Messaging.Conformance` 同步 |
| `MultiTenancy`、`Security`、`Auditing`、`Configuration` | 按标识的集合操作较少；`ISecretStore` 已是批量只读 | 审查时逐个核对，缺的补齐 |

这项列为 P5 的前置工作（API 审查 P5-05 的一部分，但要在 `Persistence.SqlSugar` 与 BasicApp 迁移之前做完，否则迁移出来的代码会把 4.x 的循环单条写法带进来）：先改 RFC-0005 / 0006 / 0009，再改抽象与全部实现、一致性套件、Go SDK 的 `caching` 包与远程 Provider 操作集，最后检查框架内部与示例没有在循环里调单条方法。
