# ADR-0028 Auditing.Database：审计事件经有界队列后台批量落库，查询按进程序列分页

## 状态

已采纳（RFC-0015 的数据库 Provider）。

## 日期

2026-10-27

## 背景

RFC-0015 定义了审计子系统：内核的 `IAuditSink`（契约要求有界、不阻塞调用路径）与 `AuditEvent`，抽象包的 Observe 阶段中间件与查询契约 `IAuditTrail`，能力 `auditing.trail@1`。到目前为止只有内存实现 `Auditing.InMemory`（环形缓冲，重启即失）。BasicApp 6.0 的操作日志在数据库里，且要按租户、主体、时间与 trace-id 查询；用户于 2026-10-27 决定在框架里实现数据库 Provider，而不是由 BasicApp 自己绕过框架的审计中间件另写一套。

约束：Ring 3 实现包要保持 AOT 兼容（细节 JSON 不能经反射序列化）；仓储只能在活动的工作单元里用，而审计写入由 Observe 阶段或后台线程发起，查询可能来自 Authorize 阶段之前，不能再经 `App.InvokeAsync` 走一遍管道（要求租户时嵌套调用会失败，启动与停止期间应用不在服务状态）；`Error` 落库后要能还原，带键的错误读回后仍按调用方区域翻译。

## 备选

1. **同步写**：`WriteAsync` 直接开事务落库，调用结束后才返回。否：`IAuditSink` 契约要求不阻塞调用路径；数据库抖动会放大到每个业务调用的尾延迟。
2. **有界队列 + 后台批量刷写**：`WriteAsync` 只是 `TryWrite` 进 `Channel`，满了丢弃并计数；后台循环攒够一批或到间隔就一次 `AddRangeAsync`。取。
3. **随业务事务写**：在调用方的工作单元里插入记录，随业务一起提交。否：审计在 Observe 阶段、调用结束后才知道结果，那时 Transaction 阶段已经提交或回滚；而且失败的调用（回滚的）也要记。
4. **排序用 UUID v7 的 `Id`**：按主键分页。否：UUID v7 只到毫秒单调，同毫秒内不保证顺序；Scheduling / Workflow 的数据库 Provider 已为此引入 `Sequence` 列。
5. **`Sequence` 列**：出队时分配，取上一个加一与时钟 UTC 刻度的较大者。取：同节点严格递增并与提交顺序一致，跨节点按时钟序；续页令牌直接是它。
6. **`Error` 还原**：带键的错误 `new Error(code, key, details)`；没有键的字面错误 `Error.Literal(code, 写入时渲染的消息, details)`。取，仿 `JobRunRecord` / `WorkflowFault` 的先例把映射文件加进架构测试 `LocalizationTests._literalAllowed`。
7. **细节 JSON 用 `JsonSerializer` + 源生成上下文**：`object?` 值的运行时类型没法在编译期穷举，仍会落到反射路径。否：用 `Utf8JsonWriter` / `JsonDocument` 手写，值按类型分支（`null`、字符串、布尔、整数与浮点、时刻与 `Guid`、`JsonElement` / `JsonNode`、嵌套字典与列表，其余 `ToString()`），读回时每个值是 `JsonElement`。

## 决策

- 新包 `XiHan.Framework.Auditing.Database`（Ring 3，`role: provider`，清单 `xihan.auditing.database`）：`Provides` = `auditing.trail@1`，`Requires` = `kernel.hosting` 与 `persistence.relational@1`，`Contributes.Config` = `XiHan:Auditing`，不声明 `Health`（审计不可用不改变应用健康等级，由计数与日志暴露）。
- `DatabaseAuditingFeature : IHostedFeature, IDisposable`：登记 `AuditRecord` 的仓储；被选中时把 `DatabaseAuditTrail` 绑定为 `IAuditSink` 与 `IAuditTrail`（`TryAdd` 先于内核默认的丢弃实现）并 `AddAuditing()`；启动后运行刷写循环，停止时先停循环、再停止接收并把队列里剩余的事件刷完。
- `AuditRecord : IEntity<Guid>`：只追加，不软删除、无并发戳；`TenantId` 是普通列、不实现 `IMultiTenant`；列：`Sequence`、`Timestamp`、`Action`、`Succeeded`、`ErrorCode` / `ErrorMessageKey` / `ErrorMessage` / `ErrorDetailsJson`、`TraceId`、`Resource`、`PrincipalId`、`TenantId`、`DetailsJson`。`Error.Inner` 不持久化。
- `DatabaseAuditTrail`（单例）：`Channel<AuditEvent>` 有界队列（`QueueCapacity` 默认 10000，`DropWrite`），丢弃计入 `DroppedCount`、警告每 10 秒最多一条；后台循环按 `BatchSize`（100）或 `FlushInterval`（1 秒，`PeriodicTimer` + `TimeProvider`）刷写，整批异常或逐项被拒只记日志、计入 `FailedCount` 并丢弃，不重试；`FlushAsync` 立即刷写并等待，与循环互斥。查询 `query.Validate()` 后把过滤项组合成 `Specification<AuditRecord>`，按 `Sequence` 升序、多取一条判断下一页；令牌不可解析 → `AUDITING_VALIDATION_001`，基础设施异常 → `AUDITING_UNAVAILABLE_001`。
- 读写都经本包 internal 的 `AuditStoreScope`：根提供者新作用域 → `IUnitOfWork.BeginAsync`（不带租户）→ 仓储 → `CommitAsync`，异常时回滚；不走管道、不加入调用方事务。
- 配置节 `XiHan:Auditing:Database`：`QueueCapacity`、`BatchSize`、`FlushIntervalSeconds`、`LogEvents`；中间件选项仍在 `XiHan:Auditing`。不新增错误码与消息键。
- 测试：`Auditing.Conformance` 套件（夹具的接收器是"写入后立刻刷写"的装饰器）、特性与队列 / 批量 / 定时 / 停止 / 落库失败 / 错误往返与翻译的用例、SQLite（`Persistence.SqlSugar`）上的实体映射与分页查询。

## 理由

- 有界队列是 `IAuditSink` 契约（有界、不阻塞）在数据库上的直接实现：业务调用的延迟与审计存储解耦，存储不可用时代价是丢审计而不是拒服务，并且丢了多少可观测。
- 批量写是批量优先（ADR-0012）的要求，也是数据库落库的成本模型：一批一个事务，比逐条写少一个数量级的往返。
- `Sequence` 把"写入顺序"定义成可以落在列上的东西：同节点与提交顺序一致，续页令牌不会越过尚未落库的事件；跨节点只按时钟序，这是不引入中心计数器的代价，README 写明。
- 手写 JSON 让包保持 AOT 兼容，同时与 `Plugins.Protocol` / `AI.Abstractions` 已有的 `object?` → JSON 分支一致。
- 还原策略与 `JobRunRecord`、`WorkflowFault` 一致：带键的错误保留翻译能力，字面错误只能保留文本；允许名单只加映射文件这一处。

## 影响

- 新包 `Auditing.Database` 与测试项目进解决方案、架构测试引用与 `next/README.md` 包表；`LocalizationTests._literalAllowed` 增加 `XiHan.Framework.Auditing.Database/Trail/AuditRecordMapping.cs`。
- `Auditing.InMemory` 不变，仍是默认实现；装配 `DatabaseAuditingFeature` 即顶替。
- BasicApp 6.0 的操作日志直接用本包：应用在自己的 `DbContext` / SqlSugar 表结构里登记 `AuditRecord`，JSON 与消息列用长文本类型，建议索引 `(Sequence)` 唯一、`(TraceId)`、`(Timestamp)`。
- RFC-0015 的"参考实现"应补上本包（规范修订由主会话统一做）。
