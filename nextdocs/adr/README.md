# 架构决策记录（ADR）

每条决策一个文件，编号递增，状态只增不改（已采纳 → 已替代）。模板：状态、日期、背景、备选、决策、理由、影响。

| 编号 | 决策 | 状态 |
| --- | --- | --- |
| [0001](0001-版本线.md) | 下一代所有包共享 5.x 版本线 | 已采纳 |
| [0002](0002-原语层与内核分包.md) | `Primitives` 与 `Kernel` 分包，原语层单独冻结 | 已采纳 |
| [0003](0003-内核单包.md) | 内核为单包，职责用子命名空间表达 | 已采纳 |
| [0004](0004-第二语言.md) | 第二语言 Go 先行 | 已采纳 |
| [0005](0005-数据Provider顺序.md) | 持久化 Provider 以 EntityFrameworkCore 优先 | 已采纳 |
| [0006](0006-进程外插件协议.md) | 进程外 gRPC 插件协议进 5.0，Wasm 标 Experimental | 已采纳 |
| [0007](0007-Spec仓库位置.md) | Spec 先留在 `next/spec/`，P4 公开时拆出 | 已采纳 |
| [0008](0008-旧版本维护期.md) | 4.x 在 5.0 GA 后维护 24 个月 | 已采纳 |
| [0009](0009-Wasm插件ABI.md) | Wasm 插件 ABI v1 取核心模块 + 线性内存 + JSON，组件模型留给 v2 | 已采纳 |
| [0010](0010-规范与SDK仓库命名.md) | 规范与各语言 SDK 分仓库，按 `framework-*` 命名，`next/spec` 为 submodule | 已采纳 |
| [0011](0011-远程Provider.md) | 远程 Provider = 宿主侧 `<领域>.Remote` 适配器 + 插件实现规范的操作集，适配器不走管道直接执行代理操作 | 已采纳 |
| [0012](0012-批量优先.md) | 批量优先：抽象层加集合入参的批量方法，逐项结果，Provider 原生批量，不提供按查询条件的批量更新 / 删除 | 已采纳 |
| [0013](0013-SqlSugar-Provider.md) | SqlSugar Provider：依赖 `SqlSugarCore`，客户端由特性创建或应用注册，实体只实现协议接口由约定映射，并发用框架并发戳 | 已采纳（分片连接选择被 0022 修订） |
| [0014](0014-取消V4垫片.md) | 取消 `Compat.V4` 运行期垫片：模块骨架由 `xihan migrate v4` 生成为原生特性，投入放在代码修复与迁移指南 | 已采纳（迁移工具部分被 0021 替代） |
| [0015](0015-集合级自定义动词路由.md) | 自定义动词按请求是否声明 `id` 分实例级 `/{resource}/{id}:{verb}` 与集合级 `/{resource}:{verb}` HTTP 路由；操作描述公开请求 / 响应的 `JsonTypeInfo` | 已采纳 |
| [0016](0016-工作流子系统.md) | 工作流子系统：引擎与内置活动进 `Workflow.Abstractions`，Provider 只实现存储、实例锁与轮询；JSON 值模型；活动经管道执行；事件走 `workflow.*` 消息 | 已采纳 |
| [0017](0017-AI子系统.md) | AI 子系统：以 `Microsoft.Extensions.AI` 为线格式；`Expose.Ai` 的操作契约经完整管道成为模型工具；知识库定义 `IKnowledgeStore`，`AI.VectorData` 适配 `VectorStore`；只有 `ai.tools` 有默认实现 | 已采纳 |
| [0018](0018-P5-09能力补齐.md) | P5-09 能力补齐：租户分片契约与默认解析器进 `MultiTenancy.Abstractions`，Tenant 阶段解析、持久化 Provider 按分片选连接；`IDistributedCache` 架在 `ICacheStore` 上；`ObjectStorage.S3` 用 AWS SDK 覆盖全部兼容服务；不补 `Messaging.Kafka`（后由 ADR-0019 撤销） | 已采纳 |
| [0019](0019-Kafka流、两级缓存与Nacos.md) | 流能力 = 队列 + `xihankey` 分区键有序 + 消费组位点 + `IMessageReplayer` 按时刻重放，`Messaging.Kafka` 同时提供队列与流；`Caching.Hybrid` 自己实现两级缓存（L1 对象 LRU + L2 共享缓存）而不架在 `HybridCache` 上；`Configuration.Nacos` 直连 HTTP 接口不依赖 SDK | 已采纳 |
| [0020](0020-调度与工作流的数据库Provider.md) | 不引入 Quartz：调度引擎进抽象包、Provider 只实现 `IJobStore`，作业与触发器分离、Quartz 文法 cron、日历、misfire、运行历史、集群分发靠并发戳 CAS 抢占；`Workflow.Database` 的存储与锁单例各自提交；工作流补齐触发式启动、事件网关、外部任务、Http / Compensate、跳转 / 迁移 / 清理、认领 / 退回 / 催办、阈值与提醒 | 已采纳 |
| [0021](0021-撤掉4.x迁移规则集.md) | 撤掉 4.x 迁移规则集：4.x 单独维护，`xihan migrate <来源版本>` 只保留通用机制、规则集为空，P5 不再要求 4.x 迁移指南 | 已采纳 |
| [0022](0022-SqlSugar多库.md) | SqlSugar 多库：作用域内的客户端带默认与 `shard` 两个连接，实体按 `IEntityConnectionSelector` 落点（`[Tenant]` → `IMultiTenant` 落分片 → 默认），工作单元经 `AsTenant()` 跨连接提交或回滚 | 已采纳 |
| [0023](0023-SqlSugar分表.md) | SqlSugar 分表：仓储按实体的 `[SplitTable]` 自动走分表入口，`EntityQuery<T>.Within` 是 Provider 无关的时间范围提示（只限定扫描的表，不过滤行） | 已采纳 |
| [0024](0024-Validate阶段契约校验.md) | Validate 阶段契约校验：内核自带 JSON Schema 子集校验器按请求 Schema 拒绝载荷、解码后执行 `IValidatableObject`；DataAnnotations 由生成器折进 Schema；HTTP 宿主进管道前绑定载荷 | 已采纳 |
| [0025](0025-领域事件派发.md) | 领域事件派发：工作单元登记写过的聚合，Transaction 阶段提交前经 `IDomainEventDispatcher`（Messaging 实现，显式登记事件类型）发成 CloudEvents；发件箱时同事务；没有派发器却有事件即失败 | 已采纳 |
| [0026](0026-MultiTenancy-Database.md) | `MultiTenancy.Database`：租户目录进数据库，`TenantRecord` 的属性放 JSON 列；单例目录直接开只读工作单元读取（不经管道，避免 Tenant 阶段自指与服务状态限制）并按租户有界缓存（存活时间、写入即失效），作用域的 `TenantRecordStore` 批量写入 | 已采纳 |
| [0027](0027-Security-Database.md) | `Security.Database`：授予进数据库，`IPermissionGrantStore` 在新作用域直接开只读工作单元读仓储（不经管道），全局授予以空字符串租户列落库保证唯一索引语义一致，授予缓存放存储里（契约不变），`PermissionGrantRecordStore` 批量授予 / 撤销逐项结果，新错误码 `SECURITY_NOTFOUND_002` | 已采纳 |
| [0028](0028-Auditing-Database.md) | `Auditing.Database`：审计事件经有界队列后台批量落库（不阻塞调用、满了丢弃并计数、失败不重试），`Sequence` 列作写入顺序与续页令牌，`Error` 按键还原、字面错误仿 `JobRunRecord` 先例用 `Error.Literal`，细节 JSON 手写不经反射 | 已采纳 |
| [0029](0029-Resilience阶段.md) | Resilience 阶段：内核内置 `ResilienceMiddleware` 按操作 ID 施加熔断、固定窗口限流与并发上限（BCL `System.Threading.RateLimiting`，熔断自写三态），配置节 `XiHan:Kernel:Resilience` 可按操作覆盖，拒绝以 `KERNEL_UNAVAILABLE_002` – `004` 返回；只算基础设施类失败；不做分桶与自适应 | 已采纳 |
| [0030](0030-Provider健康项.md) | Provider 健康项：清单声明的 `contributes.health` 由各 Provider 实现——连通性探针经内核 `PeriodicHealthContributor` 周期探测并缓存结果，容量 / 状态项同步计算，`Localization.Database` 以最近一次加载结果上报；架构测试守护清单与实现一致；不改内核聚合语义 | 已采纳 |
| [0031](0031-OpenAPI权限扩展.md) | OpenAPI 文档以 `x-xihan-permission` / `x-xihan-policy` 发布操作的授权要求、以 `components.x-xihan-permissions` 发布权限定义（被引用的 ∪ 注册表全部），不占用标准 `security` 字段 | 已采纳 |
| [0032](0032-OpenAPI生成器补齐.md) | OpenAPI 生成器补齐：`MapXiHanOpenApi(OpenApiOptions)` 指定标题 / 版本 / `servers`；装配 Bearer 认证时发布 `securitySchemes.bearer` 与全局 `security`（无要求的标 `security: []`）；操作 `summary` 取 `<操作 ID>.description` 并按区域缓存；普通端点按元数据收录，JSON 只引用已登记操作的根 Schema（显式声明或按类型匹配，根提升到组件），引用不到即失败 | 已采纳 |
| [0033](0033-雪花标识生成器.md) | 雪花标识生成器：`Persistence.Abstractions` 的 `SnowflakeIdFeature` / `SnowflakeIdGenerator`（`IIdGenerator<long>`，41 / 10 / 12 位，epoch 缺省 `2026-01-01T00:00:00Z`）；机器位 `XiHan:Persistence:Snowflake:WorkerId` 显式配置或经 `IDistributedLock` 租约分配（释放再获取续约，丢失即拒绝生成并以健康项上报），不做随机；回拨 ≤ 100 ms 等待、超过 `PERSISTENCE_UNAVAILABLE_004`；没有机器位 `PERSISTENCE_UNAVAILABLE_005`；`IdGeneratorConformance<TKey>` 套件 | 已采纳 |
| [0034](0034-Schema命名与枚举定义.md) | Schema 命名：封闭泛型的根 `title` 与 `$defs` 定义名带实参名（`<外层名>Of<实参名>`，多个实参以 `And` 连接、嵌套递归，如 `PageResultOfUserResponse`）；按字符串序列化的枚举进 `$defs/<枚举类型名>` 并以 `$ref` 引用（可空为 `anyOf`），整数枚举仍内联；gRPC 对枚举定义仍映射 `string`、不建消息；`OpenApiDocumentSource.FromServices` 公开与 `MapXiHanOpenApi` 相同的文档输入组装 | 已采纳 |
| [0035](0035-SqlSugar可空值类型判空.md) | `Persistence.SqlSugar` 在交给 SqlSugar 前把规约里可空值类型与 null 的 `==` / `!=` 改写成 `HasValue` 判定（`NullableComparisonRewriter`）：SqlSugar 把 `DateTimeOffset?` 等的提升比较译成 `= NULL`、静默空集；字面量 null、闭包里的 null 变量、左右互换都改写，与非 null 值的比较不动；一致性套件新增可空判空用例，三个 Provider 一致 | 已采纳 |
