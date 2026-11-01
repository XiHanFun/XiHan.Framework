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
modelBuilder.ApplyPersistenceConventions();
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
| `DatabaseWorkflowFeature` | 登记九个实体的仓储，装配存储、事务、实例锁与引擎；应用进入服务状态后启动 `DueBookmarkPollingLoop`，停止时等进行中的轮询结束并释放领导权。健康项 `workflow.database.poller`：启动时同步读一次 `workflow.poller` 的租约行，之后按 `HealthProbeIntervalSeconds`（默认 15）在后台重探——在独立作用域里开只读工作单元读行后回滚，不经管道；租约表不可达时条目 Degraded（`KERNEL_UNAVAILABLE_005`），应用仍在服务 |
| `Entities.WorkflowDefinitionRow` / `WorkflowInstanceRow` / `WorkflowNodeInstanceRow` / `WorkflowBookmarkRow` | 两个分区共用的列：主键都是模型的 ID；查询列加 `Document`（JSON）；书签行另有 `NodeId` 与所属实例的 `DefinitionName`、`InstanceStatus`（待办查询下推用，存储维护）；实例、节点实例与书签另有写入序号 `Sequence`（首次写入分配、替换保留）作同刻排序的次关键字——UUID v7 只到毫秒单调，不能当写入顺序用 |
| `Entities.WorkflowDefinitionRecord` / `WorkflowInstanceRecord` / `WorkflowNodeInstanceRecord` / `WorkflowBookmarkRecord` | 租户分区，实现 `IMultiTenant`：持久化按工作单元的租户写入 `TenantId` 并过滤，租户有独立库时落它的分片连接 |
| `Entities.PlatformWorkflowDefinitionRecord` / `PlatformWorkflowInstanceRecord` / `PlatformWorkflowNodeInstanceRecord` / `PlatformWorkflowBookmarkRecord` | 平台分区，没有租户列，落平台库 |
| `Entities.WorkflowLockRecord` | 租约行：`Id` 是实例 ID 或资源名，`Holder`、`ExpiresAt`、并发戳 |
| `Storage.DatabaseWorkflowDefinitionStore` / `DatabaseWorkflowInstanceStore` / `DatabaseWorkflowBookmarkStore` | 作用域服务；在所在作用域的工作单元里经仓储读写，不经管道、不自己提交；分区参数为空读写平台表，否则读写租户表，且必须等于工作单元的租户（不等抛 `InvalidOperationException`） |
| `DatabaseWorkflowTransaction` | `IWorkflowTransaction`：在调用里且 Transaction 阶段已开启工作单元时可加入；只读工作单元里写入返回 `PERSISTENCE_UNEXPECTED_001` |
| `Locking.DatabaseWorkflowInstanceLock` | 租约式分布式锁：插入行即拿到，过期的行按并发戳接管，持有期间每三分之一租约续约，释放删自己的行；每一步在根提供者的新作用域里开可写工作单元自己提交，不经管道、不随业务回滚 |
| `Polling.PollerLeadership` / `DueBookmarkPollingLoop` | 资源 `workflow.poller` 的租约（全局一份）；每个周期先拿或续领导权，持有才 `RunCycleAsync`：逐个分区轮询到期书签，到了维护周期按保留期清理 |
| `DatabaseWorkflowOptions` | `XiHan:Workflow:Database`：`PollIntervalSeconds`（1）、`BatchSize`（100）、`LockLeaseSeconds`（60）、`HolderId`（`机器名:进程 ID:8 位随机十六进制`）、`HealthProbeIntervalSeconds`（15） |

引擎选项在 `XiHan:Workflow`（`MaxNodeExecutionsPerBurst`、`MaxSubWorkflowDepth`、`LockTimeoutSeconds`、`NotResumableBackoffSeconds`、`ExpressionCacheSize`）。

## 规则

- 保存是“新增或整体替换”：按 ID 批量取回已有行，有的整行更新、没有的新增，同一批里同一 ID 后者覆盖前者；保存在调用方的事务里，失败随调用一起回滚。写入的记录的 `TenantId` 必须等于分区（不等抛 `ArgumentException`）。删除对不存在的 ID 忽略；删实例级联删节点实例，`DeleteByInstanceAsync` 删该实例全部书签。
- 读出来的模型来自 JSON 反序列化，天然是快照。按 ID 批量查找每 1000 个一批；列表与级联查询按全序排序分页读完。
- 实例列表：`WorkflowInstanceQuery` 的每个过滤项都是 SQL 条件（状态集合是 `IN`，时间范围按列比较），创建时间降序、同刻按写入序号降序，`Skip` / `Limit` 直接分页；`CountAsync` 是 SQL 计数。
- 待办查询：`ListUserTasksAsync` / `CountUserTasksAsync` 是 SQL 条件、排序、分页与计数，只取 `InstanceStatus = 'Running'` 的用户任务书签。书签保存时按 ID 批量取回所属实例的行填入定义名与状态；实例保存后把新增的与状态或定义名变了的实例同步到它们的书签行（只更新不一致的行），都在调用方的事务里。
- 书签：到期查询 `DueAt <= now` 按到期时间升序，其余按创建时间升序，同刻按写入序号；节点实例按开始时间升序，同刻按写入序号（同一实例的写入由实例锁串行化，序号即创建顺序）。可空时间列的条件不写 `!= null`：SQL 的 `NULL <= 值` 本就不成立，而 SqlSugar 会把它翻译成 `<> @参数(NULL)`。
- 仓储失败 → `WORKFLOW_UNAVAILABLE_001`，`reason` 是原错误码，原错误在 `Inner`；只读工作单元的拒绝 `PERSISTENCE_UNEXPECTED_001` 原样返回。引擎在调用之外被调用时自己开一次调用：应用不在服务状态 → `WORKFLOW_UNAVAILABLE_001`。
- 实例锁：行不存在就插入（别人先插入 → 重试），存在且过期就带并发戳接管（戳冲突 → 重试），存在未过期就等待（50ms 起每次翻倍、封顶 500ms），超过 `LockTimeout` → `WORKFLOW_TIMEOUT_001`。续约失败只记日志；释放只删自己的行，重复释放无害。每一步都是独立事务，别的节点立刻看得见。
- 轮询领导权只为省资源：多个节点同时轮询也不会重复执行，到期书签的恢复由引擎的实例锁串行化。
- 租约行与业务写入不在同一个事务里：业务事务写过之后再写租约行，SQLite 这类只有一个写者的数据库会等到自己的事务结束（忙等超时）。在 SQLite 上要经 `IEntityConnectionSelector` 把 `WorkflowLockRecord` 放到另一个库文件，调用之外的作用域以那个库为根连接（数据库测试的 `SqliteDatabase` 这样做）；PostgreSQL 等行级锁的数据库不需要。
- 表结构由应用管理（`InitTables` 只用于开发与测试）：`Document` 列用长文本类型（`text` / `nvarchar(max)` / `longtext`）；索引由实体上的 `[EntityIndex]` 声明（ADR-0072），SqlSugar 的 `InitTables` 与 EF Core 的 `ApplyPersistenceConventions()` 都按它建：`UX_WorkflowDefinitionRecord_Na_TeId_Ve`（`(Name, TenantId, Version)` 唯一，租户列由 `IMultiTenant` 写入、不为空）与 `UX_PlatformWorkflowDefinitionRecord_Na_Ve`（`(Name, Version)` 唯一）——名字空间里按名字与版本查找、取最大版本与最新发布版本，每一行都受约束；`IX_WorkflowInstanceRecord_PaInId`（`ParentInstanceId`，级联子实例）、`IX_WorkflowInstanceRecord_CoId`（`CorrelationId`，按关联 ID 启动时查重）、`IX_WorkflowInstanceRecord_TeId_EnAt`（保留期清理）、`IX_WorkflowInstanceRecord_TeId_CrAt`（列表倒序分页）、`IX_WorkflowNodeInstanceRecord_InId`（`InstanceId`）、`IX_WorkflowBookmarkRecord_InId`（`InstanceId`）、`IX_WorkflowBookmarkRecord_Ki_Ke`（`(Kind, Key)`，信号、受理人、子流程节点）、`IX_WorkflowBookmarkRecord_TeId_DuAt`（到期书签的轮询）；平台表有同样的索引，时间列不带租户前缀（`IX_PlatformWorkflowInstanceRecord_EnAt` 等）；租约表只按主键读写。有独立库的租户，四张租户表要建在它自己的库里（`InitTables` 只作用于默认连接）。实例列表的其他过滤组合（如按定义与状态）由应用按查询习惯自己加。

## 依赖方向

依赖 `XiHan.Framework.Workflow.Abstractions`、`XiHan.Framework.Persistence.Abstractions` 与 `XiHan.Framework.MultiTenancy.Abstractions`；不引用任何数据库驱动，具体数据库由装配的持久化 Provider 决定。

## 状态

P5：可用，对 SQLite（`Persistence.SqlSugar`）通过 `Workflow.Conformance` 的存储与引擎全部用例（含加入调用方事务的用例）。从上一版升级的表结构与数据迁移见 ADR-0077。
