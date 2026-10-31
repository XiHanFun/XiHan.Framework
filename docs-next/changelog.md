# 更新日志 · XiHan Framework 5

本页记录 XiHan Framework 5 各版本中使用者可感知的变更，每条标注 **新增 / 修复 / 优化 / 调整 / 移除** 类别；仓库自身的 CI、测试工程与构建脚本不列入。4.x 的变更记录在 4.x 文档站。

## v5.0.0-preview.1（未发布）

::: warning 升级须知
- 5.0 在 `next` 分支从零重写，与 4.x 互不引用，不提供 4.x 到 5.0 的迁移工具与规则集，迁移按[从 4.x 迁移](./guide/upgrade)重写装配层与对外接口
- 预览包尚未发布到 nuget.org，从源码引用或用本地包源取得，见[快速上手](./quickstart#获取预览包)
:::

### 原语与内核

- **新增** 原语层 `Primitives`：`Result` / `Result<T>`、`Error` 与错误码文法、`Maybe<T>`、`TypedId`、`InvocationContext`、清单模型、`PipelineStage`、`LifecycleState`、`Stability` 与 `[ApiLevel]`，只增不改
- **新增** 内核 `Kernel`：`AddXiHan` + `UseFeature<T>()` 装配特性，按能力 ID 求解能力图谱，`XiHan:Providers:<能力 ID>` 或 `UseProvider` 选择 Provider，启动日志打印图谱与管道
- **新增** 十一个阶段的执行管道与内置中间件：截止时间、按操作 ID 的熔断 / 限流 / 并发上限（`XiHan:Kernel:Resilience`）、按 JSON Schema 与 `IValidatableObject` 的契约校验
- **新增** 操作契约与调用器：`[Operation]`、`IOperation<TRequest, TResponse>`、`OperationRegistry`、`IOperationInvoker`；`Kernel.Generators` 生成登记代码与请求 / 响应 JSON Schema（`XHG001`–`XHG005`），程序集级 `OperationDefaults` 与只读声明
- **新增** 九态生命周期、`/health/startup|ready|live` 探针、`IHealthContributor` 与周期探测的 `PeriodicHealthContributor`，健康条目带说明、检查时刻、耗时与标量数据
- **新增** 资源绑定（`XiHan:Bindings`、`SERVICE_BINDING_ROOT`）与配置目录 `XIHAN_CONFIG_PATH` 热更新
- **新增** `IInvocationAccessor` 与调用来源 `InvocationOrigin`（协议、请求目标、远端地址、User-Agent、端点路由与名字）
- **新增** 分析器 `Kernel.Analyzers`（XH0001–XH0005）与测试基座 `Testing`

### 子系统

- **新增** 本地化：消息键与 `key.L()`、每种语言一个 `Localization/<区域>.json`、内置 10 种区域（默认 `en-US`）、宿主按 `Accept-Language` 翻译 Problem Details；`Localization.InMemory` 与 `Localization.Database`
- **新增** 领域模型 `Domain`：实体协议接口、值对象、领域事件缓冲与规约
- **新增** 持久化：仓储、查询、工作单元与 Transaction 阶段、批量方法、雪花标识生成器；`Persistence.InMemory`、`Persistence.EntityFrameworkCore`、`Persistence.SqlSugar`（多库、分表、差异日志）与 `Persistence.Generators`
- **新增** 缓存：字节存储、类型化缓存、分布式锁、Idempotency 阶段、`IDistributedCache` 适配、键管理；`Caching.InMemory`、`Caching.Redis`、`Caching.Hybrid`、`Caching.Remote`（Experimental）
- **新增** 多租户：租户解析与 Tenant 阶段、租户分片与管道外的 `TenantShardScope`；`MultiTenancy.InMemory`、`MultiTenancy.Database`
- **新增** 安全：认证处理器与 Authenticate 阶段、权限与策略、RBAC 授予、权限收窄过滤器、授予来源、条件引擎、IP 归属地；`Security.InMemory`、`Security.Database`、`Security.Jwt`、`Security.OpenIdConnect`、`Security.Ip2Region`
- **新增** 消息：CloudEvents 信封、发布与订阅、经管道的派发、重试与死信、事务性发件箱、领域事件到消息的登记；`Messaging.InMemory`、`Messaging.RabbitMQ`、`Messaging.Kafka`（流与按时刻重放）
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

### 移除

- **移除** 4.x 的模块基类与生命周期钩子、`[DependsOn]`、Castle 动态代理、动态 API、`Utils`，以及机器人、消息通知渠道、模板引擎、脚本引擎、搜索引擎、HTTP 客户端封装、网关、SignalR 封装、对象映射、升级引擎、虚拟文件系统、开发工具等包；替代做法见[从 4.x 迁移](./guide/upgrade)
