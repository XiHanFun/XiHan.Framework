# XiHan.Framework.Workflow.Database

`workflow.execution@1` 的数据库 Provider（Ring 3，`role: provider`，RFC-0021）。定义、实例、节点实例与书签以实体在调用方的工作单元里经 `persistence.relational@1` 的仓储读写，随业务提交或回滚（ADR-0077）：每张表是过滤与排序用的列加整份模型的 JSON 文档（`WorkflowJson`）。数据按分区落表：租户的记录实现 `IMultiTenant`、随租户分库，平台的记录在平台库的 `Platform*` 表，两边互不可见。实例锁与到期书签轮询的领导权以平台库里的同一张租约表实现，多个节点共用。可选依赖 `multitenancy.context@1`：装配了多租户时轮询器逐个分区覆盖租户目录里启用的租户。装配后顶替默认的 `Workflow.InMemory`。

```csharp
builder.AddXiHan(x => x
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseWorkflowFeature>());

// EF Core 应用在自己的 DbContext 里登记九个实体，并给 Document 列用长文本类型；索引由实体上的 [EntityIndex] 经协议约定建出
modelBuilder.Entity<WorkflowInstanceRecord>(static i => i.Property(static x => x.Document).HasColumnType("text"));
modelBuilder.ApplyPersistenceConventions(Database);
```

```json
{
  "XiHan": {
    "Workflow": {
      "LockTimeoutSeconds": 10,
      "Database": { "PollIntervalSeconds": 1, "BatchSize": 100, "LockLeaseSeconds": 60, "HolderId": "node-1" }
    }
  }
}
```

| 类型 | 用途 |
| --- | --- |
| `DatabaseWorkflowFeature` | 登记九个实体的仓储，装配存储、事务、实例锁与引擎；启动时起 `DueBookmarkPollingLoop`（应用不在服务状态的周期不轮询、不记告警，也不拿不续领导权、持有就释放），停止时等进行中的轮询结束并释放领导权。健康项 `workflow.database.poller`：启动时同步读一次 `workflow.poller` 的租约行，之后按 `HealthProbeIntervalSeconds`（默认 15）在后台重探——在独立作用域里开只读工作单元读行后回滚，不经管道；租约表不可达时条目 Degraded（`KERNEL_UNAVAILABLE_005`），应用仍在服务 |
| `Entities.WorkflowDefinitionRow` / `WorkflowInstanceRow` / `WorkflowNodeInstanceRow` / `WorkflowBookmarkRow` | 两个分区共用的列：主键都是模型的 ID；查询列加 `Document`（JSON）；定义行另有 `Category`（精确过滤）与 `SearchText`（名字、显示名、说明折叠后拼成的关键字查询列，ADR-0096）；书签行另有 `NodeId`、任务标题查询列 `SearchTitle` 与所属实例的 `DefinitionName`、`InstanceStatus`、`SearchInstanceTitle`（待办查询下推用，存储维护）；实例行另有 `SearchTitle`（实例标题折叠后的值，ADR-0096）；实例、节点实例与书签另有写入序号 `Sequence`（首次写入分配、替换保留）作同刻排序的次关键字——UUID v7 只到毫秒单调，不能当写入顺序用 |
| `Entities.WorkflowDefinitionRecord` / `WorkflowInstanceRecord` / `WorkflowNodeInstanceRecord` / `WorkflowBookmarkRecord` | 租户分区，实现 `IMultiTenant`：持久化按工作单元的租户写入 `TenantId` 并过滤，租户有独立库时落它的分片连接 |
| `Entities.PlatformWorkflowDefinitionRecord` / `PlatformWorkflowInstanceRecord` / `PlatformWorkflowNodeInstanceRecord` / `PlatformWorkflowBookmarkRecord` | 平台分区，没有租户列，落平台库 |
| `Entities.WorkflowLockRecord` | 租约行：`Id` 是实例 ID 或资源名，`Holder`、`ExpiresAt`、并发戳 |
| `Storage.DatabaseWorkflowDefinitionStore` / `DatabaseWorkflowInstanceStore` / `DatabaseWorkflowBookmarkStore` | 作用域服务；在所在作用域的工作单元里经仓储读写，不经管道、不自己提交；分区参数为空读写平台表，否则读写租户表，且必须等于工作单元的租户（不等抛 `InvalidOperationException`） |
| `DatabaseWorkflowTransaction` | `IWorkflowTransaction`：在调用里且 Transaction 阶段已开启工作单元时可加入；只读工作单元里写入返回 `PERSISTENCE_UNEXPECTED_001` |
| `Locking.DatabaseWorkflowInstanceLock` | 租约式分布式锁：插入行即拿到，过期的行按并发戳接管，持有期间每三分之一租约续约，释放删自己的行；每一步在根提供者的新作用域里开可写工作单元自己提交，不经管道、不随业务回滚 |
| `Polling.PollerLeadership` / `DueBookmarkPollingLoop` | 资源 `workflow.poller` 的租约（全局一份）；每个周期（`DueBookmarkPollingLoop.RunCycleAsync`）先看应用状态：不在服务状态（启动中、排空中）就释放领导权、什么也不做，否则拿或续领导权，持有才 `DueBookmarkPoller.RunCycleAsync`：逐个分区轮询到期书签，到了维护周期按保留期清理（ADR-0087） |
| `DatabaseWorkflowOptions` | `XiHan:Workflow:Database`：`PollIntervalSeconds`（1）、`BatchSize`（100）、`LockLeaseSeconds`（60）、`HolderId`（`机器名:进程 ID:8 位随机十六进制`）、`LockConnection`（租约表的命名连接，缺省在平台库；空白视为未配置）、`HealthProbeIntervalSeconds`（15） |

引擎选项在 `XiHan:Workflow`（`MaxNodeExecutionsPerBurst`、`MaxSubWorkflowDepth`、`LockTimeoutSeconds`、`NotResumableBackoffSeconds`、`ExpressionCacheSize`）。

## 规则

- 保存是“新增或整体替换”：按 ID 批量取回已有行，有的整行更新、没有的新增，同一批里同一 ID 后者覆盖前者；保存在调用方的事务里，失败随调用一起回滚。写入的记录的 `TenantId` 必须等于分区（不等抛 `ArgumentException`）。删除对不存在的 ID 忽略；删实例级联删节点实例，`DeleteByInstanceAsync` 删该实例全部书签。
- 读出来的模型来自 JSON 反序列化，天然是快照。按 ID 批量查找每 1000 个一批；列表与级联查询按全序排序分页读完。
- 实例列表：`WorkflowInstanceQuery` 的每个过滤项都是 SQL 条件（状态集合与关联 ID 集合是 `IN`，关联 ID 的空集合不匹配，只取顶层是 `ParentInstanceId IS NULL`，标题关键字对实例行的 `SearchTitle` 做包含判断，时间范围按列比较；ADR-0096），创建时间降序、同刻按写入序号降序，`Skip` / `Limit` 直接分页；`CountAsync` 是 SQL 计数。
- 定义列表：`ListSummariesAsync` / `CountAsync` 是 SQL 条件、排序、分页与计数（ADR-0096）：关键字对 `SearchText` 做包含判断（`WorkflowKeyword.Join(name, displayName, description)`，两边都已折叠，SqlSugar 翻译出的 `LIKE` 里没有通配字符），分类、名字、状态等值；名字升序、版本降序、再按 ID；摘要从读回的 `Document` 取。两列可空只为给已有行的表加列，升级前写入的行要从 `Document` 回填（`Category` 取 `category`，`SearchText` 按上式），没回填的行不被分类与关键字匹配。
- 待办查询：`ListUserTasksAsync` / `CountUserTasksAsync` 是 SQL 条件、排序、分页与计数，只取 `InstanceStatus = 'Running'` 的用户任务书签；关键字对 `SearchTitle`（载荷 `title` 折叠后的值）与 `SearchInstanceTitle`（所属实例行的 `SearchTitle`）各自判非空后做包含判断、取或（ADR-0096）。书签保存时按 ID 批量取回所属实例的行填入定义名、状态与实例标题查询列；实例保存后把新增的与状态、定义名或标题查询列变了的实例同步到它们的书签行（只更新不一致的行），都在调用方的事务里。
- 书签：到期查询 `DueAt <= now` 按到期时间升序，其余按创建时间升序，同刻按写入序号；节点实例按开始时间升序，同刻按写入序号（同一实例的写入由实例锁串行化，序号即创建顺序）。可空时间列的条件不写 `!= null`：SQL 的 `NULL <= 值` 本就不成立，而 SqlSugar 会把它翻译成 `<> @参数(NULL)`。
- 仓储失败 → `WORKFLOW_UNAVAILABLE_001`，`reason` 是原错误码，原错误在 `Inner`；只读工作单元的拒绝 `PERSISTENCE_UNEXPECTED_001` 原样返回。引擎在调用之外被调用时自己开一次调用：应用不在服务状态 → `WORKFLOW_UNAVAILABLE_001`。
- 实例锁：行不存在就插入（别人先插入 → 重试），存在且过期就带并发戳接管（戳冲突 → 重试），存在未过期就等待（50ms 起每次翻倍、封顶 500ms），超过 `LockTimeout` → `WORKFLOW_TIMEOUT_001`。续约失败只记日志；释放只删自己的行，重复释放无害。每一步都是独立事务，别的节点立刻看得见。
- 轮询领导权只为省资源：多个节点同时轮询也不会重复执行，到期书签的恢复由引擎的实例锁串行化。
- 租约行与业务写入不在同一个事务里：业务事务写过之后再写租约行，SQLite 这类只有一个写者的数据库会等到自己的事务结束（忙等超时后 `WORKFLOW_TIMEOUT_001`）。SQLite 上要配 `XiHan:Workflow:Database:LockConnection`（ADR-0084）：特性把 `WorkflowLockRecord` 登记到这个命名连接（`InitTables` 把租约表建在那里），租约的每一步与健康探针开绑定这个连接的工作单元（`UnitOfWorkOptions(Connection: …)`），事务只开在租约库上，业务调用不碰它。SqlSugar 方式一在 `XiHan:Persistence:SqlSugar:Connections:<name>` 声明这个连接（数据库测试的 `SqliteDatabase` 这样配），EF Core 在 `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` 声明（ADR-0087，数据库测试的 `EntityFrameworkCoreSqliteDatabase` 这样配：租约步骤的作用域里 `DbContext` 按租约库建出）；内存持久化不支持命名连接，配了即装配失败（`PERSISTENCE_UNAVAILABLE_008`），连接不在 SqlSugar 客户端里或没有在 EF Core 里声明时启动即失败（`PERSISTENCE_VALIDATION_004`）。不配置时租约表在平台库，PostgreSQL 等行级锁的数据库不需要配。
- 表结构由应用管理（`InitTables` 只用于开发与测试）：`Document` 列用长文本类型（`text` / `nvarchar(max)` / `longtext`）；索引由实体上的 `[EntityIndex]` 声明（ADR-0072），SqlSugar 的 `InitTables` 与 EF Core 的 `ApplyPersistenceConventions(Database)` 都按它建：`UX_WorkflowDefinitionRecord_Na_TeId_Ve`（`(Name, TenantId, Version)` 唯一，租户列由 `IMultiTenant` 写入、不为空）与 `UX_PlatformWorkflowDefinitionRecord_Na_Ve`（`(Name, Version)` 唯一）——名字空间里按名字与版本查找、取最大版本与最新发布版本，每一行都受约束；`IX_WorkflowInstanceRecord_PaInId`（`ParentInstanceId`，级联子实例）、`IX_WorkflowInstanceRecord_CoId`（`CorrelationId`，按关联 ID 启动时查重）、`IX_WorkflowInstanceRecord_TeId_EnAt`（保留期清理）、`IX_WorkflowInstanceRecord_TeId_CrAt`（列表倒序分页）、`IX_WorkflowInstanceRecord_TeId_StId_CrAt`（按发起人倒序分页，ADR-0096）、`IX_WorkflowNodeInstanceRecord_InId`（`InstanceId`）、`IX_WorkflowBookmarkRecord_InId`（`InstanceId`）、`IX_WorkflowBookmarkRecord_Ki_Ke`（`(Kind, Key)`，信号、受理人、子流程节点）、`IX_WorkflowBookmarkRecord_TeId_DuAt`（到期书签的轮询）；平台表有同样的索引，时间列不带租户前缀（`IX_PlatformWorkflowInstanceRecord_EnAt` 等）；租约表只按主键读写。有独立库的租户，四张租户表要建在它自己的库里（`InitTables` 只作用于默认连接）。实例列表的其他过滤组合（如按定义与状态）由应用按查询习惯自己加。

## 依赖方向

依赖 `XiHan.Framework.Workflow.Abstractions`、`XiHan.Framework.Persistence.Abstractions` 与 `XiHan.Framework.MultiTenancy.Abstractions`；不引用任何数据库驱动，具体数据库由装配的持久化 Provider 决定。

## 状态

P5：可用，对 SQLite（`Persistence.SqlSugar`）通过 `Workflow.Conformance` 的存储与引擎全部用例（含加入调用方事务的用例），对 SQLite（`Persistence.EntityFrameworkCore`，租约表在命名连接的另一个库文件）通过引擎与任务全部用例；对 SQLite（`Persistence.SqlSugar`）加内存消息与发件箱的宿主也通过引擎与任务全部用例（ADR-0096 第六节）。从上一版升级的表结构与数据迁移见 ADR-0077。
