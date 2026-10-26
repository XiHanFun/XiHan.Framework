# XiHan.Framework.Workflow.Database

`workflow.execution@1` 的数据库 Provider（Ring 3，`role: provider`，RFC-0021）。定义、实例、节点实例与书签以实体经 `persistence.relational@1` 的仓储读写：每张表是过滤与排序用的列加整份模型的 JSON 文档（`WorkflowJson`）；实例锁与到期书签轮询的领导权以同一张租约表实现，多个节点共用一个数据库。装配后顶替默认的 `Workflow.InMemory`。

```csharp
builder.AddXiHan(x => x
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseWorkflowFeature>());

// EF Core 应用在自己的 DbContext 里登记五个实体，并给 Document 列用长文本类型
modelBuilder.Entity<WorkflowInstanceRecord>(static i =>
{
    i.Property(static x => x.Document).HasColumnType("text");
    i.HasIndex(static x => new { x.DefinitionName, x.Status, x.CreatedAt });
});
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
| `DatabaseWorkflowFeature` | 登记五个实体的仓储，装配存储、实例锁与引擎；应用进入服务状态后启动 `DueBookmarkPollingLoop`，停止时等进行中的轮询结束并释放领导权 |
| `Entities.WorkflowDefinitionRecord` / `WorkflowInstanceRecord` / `WorkflowNodeInstanceRecord` / `WorkflowBookmarkRecord` | 主键都是模型的 ID；查询列加 `Document`（JSON）；实例、节点实例与书签另有写入序号 `Sequence`（首次写入分配、替换保留）作同刻排序的次关键字——UUID v7 只到毫秒单调，不能当写入顺序用；租户只是普通列，不实现 `IMultiTenant`，存储不按租户过滤 |
| `Entities.WorkflowLockRecord` | 租约行：`Id` 是实例 ID 或资源名，`Holder`、`ExpiresAt`、并发戳 |
| `Storage.DatabaseWorkflowDefinitionStore` / `DatabaseWorkflowInstanceStore` / `DatabaseWorkflowBookmarkStore` | 单例；每个操作在根提供者的新作用域里经 `App.InvokeAsync` 走完整管道，仓储在 Transaction 阶段内使用，返回即提交 |
| `Locking.DatabaseWorkflowInstanceLock` | 租约式分布式锁：插入行即拿到，过期的行按并发戳接管，持有期间每三分之一租约续约，释放删自己的行 |
| `Polling.PollerLeadership` / `DueBookmarkPollingLoop` | 资源 `workflow.poller` 的租约；每个周期先拿或续领导权，持有才 `PollOnceAsync` |
| `DatabaseWorkflowOptions` | `XiHan:Workflow:Database`：`PollIntervalSeconds`（1）、`BatchSize`（100）、`LockLeaseSeconds`（60）、`HolderId`（`机器名:进程 ID:8 位随机十六进制`） |

引擎选项在 `XiHan:Workflow`（`MaxNodeExecutionsPerBurst`、`MaxSubWorkflowDepth`、`LockTimeoutSeconds`、`NotResumableBackoffSeconds`、`ExpressionCacheSize`）。

## 规则

- 保存是“新增或整体替换”：按 ID 批量取回已有行，有的整行更新、没有的新增，同一批里同一 ID 后者覆盖前者；一个保存一个事务，任一项失败整批回滚。删除对不存在的 ID 忽略；删实例级联删节点实例，`DeleteByInstanceAsync` 删该实例全部书签。
- 读出来的模型来自 JSON 反序列化，天然是快照。按 ID 批量查找每 1000 个一批；列表与级联查询按全序排序分页读完。
- 实例列表：`WorkflowInstanceQuery` 的每个过滤项都是 SQL 条件（状态集合是 `IN`，时间范围按列比较），创建时间降序、同刻按写入序号降序，`Skip` / `Limit` 直接分页；`CountAsync` 是 SQL 计数。
- 书签：到期查询 `DueAt <= now` 按到期时间升序，其余按创建时间升序，同刻按写入序号；节点实例按开始时间升序，同刻按写入序号（同一实例的写入由实例锁串行化，序号即创建顺序）。可空时间列的条件不写 `!= null`：SQL 的 `NULL <= 值` 本就不成立，而 SqlSugar 会把它翻译成 `<> @参数(NULL)`。
- 管道失败（含应用不在服务状态、仓储异常、提交失败）→ `WORKFLOW_UNAVAILABLE_001`，`reason` 是原错误码或异常消息，原错误在 `Inner`。
- 实例锁：行不存在就插入（别人先插入 → 重试），存在且过期就带并发戳接管（戳冲突 → 重试），存在未过期就等待（50ms 起每次翻倍、封顶 500ms），超过 `LockTimeout` → `WORKFLOW_TIMEOUT_001`。续约失败只记日志；释放只删自己的行，重复释放无害。每一步都是独立事务，别的节点立刻看得见。
- 轮询领导权只为省资源：多个节点同时轮询也不会重复执行，到期书签的恢复由引擎的实例锁串行化。
- 存储在自己的作用域与事务里执行，不加入调用方的工作单元；在一个持有写锁的事务里调用引擎时，SQLite 等库级写锁的数据库会等到忙等超时，生产库按行锁不受影响。
- 表结构由应用管理（`InitTables` 只用于开发与测试）：`Document` 列用长文本类型（`text` / `nvarchar(max)` / `longtext`）；建议索引 `WorkflowDefinitionRecord(Name, Version)`、`WorkflowInstanceRecord(DefinitionName, Status, CreatedAt)`、`WorkflowNodeInstanceRecord(InstanceId)`、`WorkflowBookmarkRecord(InstanceId)`、`(Kind, Key)`、`(DueAt)`。

## 依赖方向

依赖 `XiHan.Framework.Workflow.Abstractions` 与 `XiHan.Framework.Persistence.Abstractions`；不引用任何数据库驱动，具体数据库由装配的持久化 Provider 决定。

## 状态

P5：可用，对 SQLite（`Persistence.SqlSugar`）通过 `Workflow.Conformance` 的存储与引擎全部用例。
