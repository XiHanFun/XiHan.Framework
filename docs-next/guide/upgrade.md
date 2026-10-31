# 从 4.x 迁移

本章面向维护 4.x 应用、考虑转到 5.0 的团队：说明两条版本线的关系、4.x 概念在 5.0 里的对应写法，以及 4.x 中不再由框架提供的能力的替代做法。

## 两条版本线

5.0 在 `next` 分支从零重写，与 4.x（`framework/`）互不引用，包 ID 前缀相同但版本线从 `5.0.0` 起。4.x 单独维护、继续发布补丁，不提供 4.x 到 5.0 的迁移工具：

- 不提供兼容垫片（ADR-0014）：垫片只能省掉模块骨架，省不下迁移的主要工作量。
- 不提供迁移规则集（ADR-0021）：命令行工具 `xihan migrate <来源版本>` 只保留通用机制，当前没有登记任何规则集，`xihan migrate v4` 返回 `CLI_VALIDATION_002`。

迁移按应用分层重写：装配层（模块 → 特性）与对外接口（动态 API → 操作契约）必须重写；领域模型与数据访问在保留同一 ORM 的前提下改动较小，例如继续用 SqlSugar 时改用 `Persistence.SqlSugar`，SqlSugar 的实体与查询写法可以沿用。

## 概念对照

| 4.x | 5.0 | 详见 |
| --- | --- | --- |
| `XiHanModule` 基类 | 实现 `IFeature` 的类，带 `Manifest` | [能力包与清单](./modularity) |
| `[DependsOn(typeof(XxxModule))]` | 清单 `Requires`：按能力 ID 与接口版本声明依赖 | [能力包与清单](./modularity) |
| `ConfigureServicesAsync` 等 7 个生命周期钩子 | `Configure(FeatureContext)` 一个方法；启动与停止实现 `IHostedFeature` | [生命周期与健康](./lifecycle) |
| `AddApplicationAsync<TModule>()` + `InitializeApplicationAsync()` | `builder.AddXiHan(x => x.UseFeature<T>())` + `app.UseXiHan()` | [宿主与 Web 应用](./web) |
| `ITransientDependency` 等注入标记接口、约定注册 | 在 `Configure` 里显式注册；仓储与操作的登记由源生成器生成 | [依赖注入](./dependency-injection) |
| Castle 动态代理、拦截器、AOP 特性 | 固定阶段的管道中间件 | [执行管道](./pipeline) |
| `[DynamicApi]` 应用服务、`ApplicationServiceBase` | 一个操作一个类：`[Operation]` + `IOperation<TRequest, TResponse>`，一份契约经 HTTP / gRPC / MCP / 命令行暴露 | [操作契约](./operations) |
| 统一响应信封与返回码 | 成功直接返回响应对象；失败返回 `Error`，宿主翻译成 RFC 9457 Problem Details | [结果与错误](./errors) |
| 运行期 DataAnnotations 校验 | 注解在编译期折进 JSON Schema，Validate 阶段执行，加 `IValidatableObject` 自校验 | [数据校验](./validation) |
| 实体基类（多层继承） | 实体协议接口组合：`IEntity<TKey>`、`IAggregateRoot<TKey>`、`IMultiTenant`、`ISoftDeletable`、`IHasCreationTime` 等 | [领域模型](./domain) |
| `Data` 包（绑定 SqlSugar） | `Persistence.Abstractions` 的仓储与查询，Provider 选 `InMemory`、`EntityFrameworkCore` 或 `SqlSugar` | [持久化](./persistence) |
| `[UnitOfWork]`、`IUnitOfWorkManager` | Transaction 阶段：每次操作调用一个工作单元；管道外显式开启 `IUnitOfWork` | [工作单元与事务](./uow) |
| `ICurrentUser`、`ICurrentTenant` | 调用上下文：`context.GetSecurity()` 的主体与租户；调用链深处用 `IInvocationAccessor` | [依赖注入](./dependency-injection#取得当前调用的上下文) |
| `[PermissionAuthorize]`、`[Authorize]` | 操作契约的 `Permission` / `Policy`，Authorize 阶段判定；权限码先用 `AddPermission` 登记 | [授权](./authorization) |
| `Authentication` 包（JWT 等） | `Security.Jwt`、`Security.OpenIdConnect`，或应用自己实现认证处理器 | [认证](./authentication) |
| `MultiTenancy` 包 | `MultiTenancy.*`：Tenant 阶段、租户存储、租户分片 | [多租户](./multi-tenancy) |
| `Caching`（HybridCache、`IDistributedCache<T>`、`[Cacheable]`） | `Caching.*`：`ICacheStore`、`ICache`、`ISharedCache`、分布式锁、两级缓存；声明式缓存特性不再提供 | [缓存与分布式锁](./caching) |
| `EventBus`、`ILocalEventBus`、`ILocalEventHandler<T>` | `Messaging.*`：`IMessagePublisher`、`IMessageHandler`、发件箱；领域事件经 `AddDomainEvent` 映射成消息 | [消息与事件](./messaging) |
| `Tasks`（后台任务、作业） | `Scheduling.*`：作业与触发器、cron、集群分发、领导选举 | [调度与后台作业](./scheduling) |
| `Workflow` | `Workflow.*`：JSON 定义模型、引擎、用户任务 | [工作流](./workflow) |
| `ObjectStorage` | `ObjectStorage.*`：`IBlobStore`、文件系统与 S3 | [对象存储](./storage) |
| `Localization`（`{ resource, culture, texts }` 资源文件） | 每种语言一个 `Localization/<区域>.json`，平铺消息键；代码只引用键常量 | [国际化](./localization) |
| `Auditing` | `Auditing.*`：Observe 阶段审计中间件与查询 | [审计](./auditing) |
| `Logging`（Serilog）、`Observability` | `Microsoft.Extensions.Logging` 与 `System.Diagnostics`；导出用 `Observability.OpenTelemetry` | [可观测性](./observability) |
| `DistributedIds` | `IIdGenerator<T>`，雪花标识 `SnowflakeIdGenerator` | [标识生成](./distributed-ids) |
| `AI` 与 `Web.Mcp` | `AI.*`（基于 `Microsoft.Extensions.AI`）与 `Protocols.Mcp` | [AI](./ai) |
| `Web.Grpc` | `Protocols.Grpc`：由操作的 Schema 推出 protobuf | [gRPC、MCP 与 CloudEvents](./protocols) |
| `Web.Docs`（Scalar、Swagger UI） | `Protocols.Http` 的 `MapXiHanOpenApi()` 发布 OpenAPI 3.1；文档界面由应用自选 | [HTTP 与 OpenAPI](./http) |
| `IClock` | `TimeProvider` | [依赖注入](./dependency-injection#时间与测试替身) |
| `Utils`、`Threading`、`Timing` | .NET 基础类库 | — |

## 不再由框架提供的能力

下列能力在 5.0 不再包装，原因是它们属于应用层需求，或 .NET 与成熟的专门组件已经覆盖。开发指南不再为它们设章节。

| 4.x 能力 | 5.0 的替代做法 |
| --- | --- |
| 机器人 `Bot` 与六个渠道（邮件、短信、Telegram、钉钉、飞书、企业微信） | 应用直接使用各渠道的 SDK 或 HTTP 接口，封装成应用自己的服务；需要异步发送时经消息子系统投递 |
| 消息通知 `Messaging`（4.x 的通知渠道） | 同上。5.0 的 `Messaging` 是队列、流与发布订阅子系统，不是通知渠道 |
| 实时通信 `Web.RealTime`（SignalR 封装） | 直接使用 ASP.NET Core SignalR，Hub 的认证经 `XiHan` 认证方案交给内核，见[实时通信](./realtime) |
| 网关 `Web.Gateway` 与流量治理 `Traffic` | 反向代理与转发用 YARP 等专门组件；按操作的熔断、限流、并发上限由内核的 Resilience 阶段提供，见[弹性与限流](./resilience) |
| 模板引擎 `Templating` | 直接使用 Scriban、Fluid 等模板库 |
| 脚本引擎 `Script` | 不提供。需要隔离执行第三方逻辑时用插件，见[插件](./plugins) |
| 搜索引擎 `SearchEngines` | 5.0 未提供，直接使用 Elasticsearch、OpenSearch 等的官方客户端 |
| HTTP 客户端 `Http` | 使用 `IHttpClientFactory` 与 `Microsoft.Extensions.Http.Resilience` |
| 数据加解密 `Security`（4.x 的加密工具） | 使用 `System.Security.Cryptography` 与 ASP.NET Core Data Protection。5.0 的 `Security` 是认证与授权子系统 |
| 对象映射 `ObjectMapping` | 显式映射，或编译期生成代码的映射库，见[序列化与 Native AOT](./aot#对象映射) |
| 序列化封装 `Serialization` | 直接使用 System.Text.Json 的源生成上下文 |
| 虚拟文件系统 `VirtualFileSystem` | 使用 .NET 的 `IFileProvider` 与嵌入资源 |
| 设置 `Settings` | 使用 .NET 配置与 `IOptionsMonitor`，远程配置见[配置与选项](./configuration) |
| 升级引擎 `Upgrade` | 版本升级编排由应用实现，需要单点执行时用调度子系统的领导选举 |
| 开发工具 `DevTools` | 命令行宿主的 `graph`、`pipeline`、`operations`、`invoke` 与 `xihan` 工具，见[命令行宿主](./command-line) |

## 迁移顺序建议

1. 清理 4.x 应用里只出现在 `[DependsOn]`、代码并未使用的模块引用，减少迁移面。
2. 选定持久化 Provider。保留 SqlSugar 时先接 `Persistence.SqlSugar`，数据层改动最小。
3. 每个 4.x 模块改写成一个特性：清单按能力 ID 写依赖，`Configure` 里显式注册原来靠约定注册的服务。
4. 实体改为实现 5.0 的实体协议接口；4.x 的应用层基类可以保留，只替换它们实现的框架接口与字段名（如创建时间改为 `CreatedAt`、租户标识改为字符串）。
5. 动态 API 的每个公开方法改写成一个操作类，DTO 原样作为请求与响应类型，登记到 `JsonSerializerContext`；`[PermissionAuthorize]` 的权限码进 `[Operation(Permission = ...)]`。
6. `ICurrentUser` / `ICurrentTenant` 的调用点保留，在应用层写一层基于 `IInvocationAccessor` 的同名包装。
7. 本地事件处理器改为 `IMessageHandler`，领域事件用 `AddDomainEvent` 登记主题。
8. 本地化资源转换成每种语言一个 JSON 文件，代码里的字符串文案换成消息键常量。
9. 从 Standard 组合跑通全部操作后，再逐个换上生产用的 Provider。

基于 4.x 构建的 XiHan.BasicApp 6.0 正在按这个顺序接入 5.0，可作为参照。

## 相关

- [框架概述](../overview)
- [更新日志](../changelog)
- 设计记录：ADR-0014《取消 V4 垫片》、ADR-0021《撤掉 4.x 迁移规则集》
