# XiHan.Framework.Scheduling.Database

`scheduling.jobs@1` 与 `scheduling.leader@1` 的数据库 Provider（Ring 3，`role: provider`，RFC-0013）。作业、触发器、运行历史与节点以实体经 `persistence.relational@1` 的仓储读写，领导租约是同一个库里的一行；多个节点共用一个数据库，抢占与独占占用靠并发戳做跨进程的比较并交换。装配后顶替默认的 `Scheduling.InMemory`，引擎（misfire、并发阻塞、暂停与恢复、动态登记、取消、历史）仍是 `Scheduling.Abstractions` 的 `JobSchedulerEngine`。

```csharp
builder.Services.AddJob<NightlyCleanup>("cleanup.nightly", JobSchedule.FromCron("0 3 * * *"));
builder.AddXiHan(x => x
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseSchedulingFeature>());

// EF Core 应用在自己的 DbContext 里登记五个实体，并给 Data 列用长文本类型
modelBuilder.Entity<TriggerEntity>(static t =>
{
    t.Property(static x => x.Data).HasColumnType("text");
    t.HasIndex(static x => new { x.State, x.Local, x.NextFireAt });
});
```

```json
{
  "XiHan": {
    "Scheduling": {
      "PollIntervalSeconds": 1,
      "NodeId": "node-1",
      "MaxConcurrentRuns": 32,
      "AcquireBatchSize": 16,
      "MisfireThresholdSeconds": 60,
      "LeaseResource": "scheduling.jobs",
      "LeaseTimeToLiveSeconds": 30,
      "NodeTimeoutSeconds": 90,
      "HousekeepingIntervalSeconds": 60,
      "HistoryRetentionDays": 7
    }
  }
}
```

本包没有自己的配置节：引擎选项在 `XiHan:Scheduling`（`SchedulerOptions`），`NodeId` 同时是心跳、抢占与租约里的节点身份，默认 `机器名:进程 ID:8 位随机`，多节点部署时每个节点唯一即可。

| 类型 | 用途 |
| --- | --- |
| `DatabaseSchedulingFeature` | 登记五个实体的仓储，装配存储、选举与引擎；应用进入服务状态后引擎开始轮询，停止时等进行中的运行结束并释放租约。健康项 `scheduling.database.leader`：启动时同步读一次租约行，之后按 `XiHan:Scheduling:Database:HealthProbeIntervalSeconds`（默认 15，`DatabaseSchedulingOptions`）在后台重探——在独立作用域里开只读工作单元读行后回滚，不经管道；租约表不可达时条目 Degraded（`KERNEL_UNAVAILABLE_005`），应用仍在服务 |
| `Entities.JobEntity` | 作业行：主键是作业名，`Data` 是 JSON 文本，`Source` 存枚举整数值；`ActiveRunId` 的占用与释放靠并发戳；重试策略与执行时限是五个可空列（次数、等待与上限按毫秒、退避方式整数值，ADR-0060），触发器行与运行行另有可空的 `Attempt`（空按 1）与 `RetryOf` |
| `Entities.TriggerEntity` | 触发器行：主键是 `作业名/触发器名`，`Schedule` 是 `JobSchedule.ToString()` 的文本，`State` / `MisfirePolicy` / `RunAs` 存枚举整数值；抢占靠并发戳 |
| `Entities.JobRunEntity` | 运行记录行：`State` / `Trigger` 存枚举整数值，`Sequence` 是首次写入分配的写入序号（替换保留），同刻排序的次关键字——UUID v7 只到毫秒单调 |
| `Entities.SchedulerNodeEntity` | 节点心跳行 |
| `Entities.SchedulerLeaseEntity` | 租约行：主键是资源名，`Holder` 是节点 ID，`Token` 是本次获取的随机令牌，`ExpiresAt`，并发戳 |
| `Storage.DatabaseJobStore` | `IJobStore`（单例，`IsShared` 为真）：每个操作在根提供者的新作用域里经 `App.InvokeAsync` 走完整管道，仓储在 Transaction 阶段内使用，返回即提交 |
| `Leadership.DatabaseLeaderElection` | `ILeaderElection`（单例）：租约行的获取 / 续约 / 释放各是一次独立的管道调用 |

## 规则

- 每个存储操作一个事务：读是快照，写在动作返回时提交；动作内任一非竞争失败整体回滚。管道失败（应用不在服务状态、仓储异常、提交失败）→ `SCHEDULING_UNAVAILABLE_003`，`reason` 是原错误码或异常消息，原错误在 `Inner`；作业不存在、运行不存在等调度语义的错误原样返回。
- 抢占（`AcquireDueAsync`）：按 `State = Waiting`、非本地、`NextFireAt <= 到期时刻` 取候选（到期时刻升序、优先级降序、键升序，取名额的三倍），每批取还缺的名额带并发戳批量改成 `Acquired`（逐条比较并交换）；读到候选之后被别的节点先改走的得到冲突，跳过并用下一批补齐，不回滚已拿到的，拿满名额或候选用尽为止。多个节点同时调用时每条触发器只被一个节点拿到。
- 独占开始（`TryBeginRunAsync(exclusive: true)`）：作业的 `ActiveRunId` 指向一条仍在运行的记录即拒绝（结束了但没清掉的占用视为空闲）；否则带并发戳把占用换成本次运行，撞上别的节点就重读再判，最多三次。完成（`CompleteRunAsync`）整行写回运行记录、清掉指向它的占用、把该作业阻塞中的触发器改回等待。阻塞（`BlockTriggerAsync`）在同一个事务里重判占用：仍被占就带并发戳原样改写一次作业行、把触发器存为 `Blocked`，占用已释放就存为 `Waiting`；完成也带并发戳改写作业行之后才解除阻塞，两边谁后提交谁看到对方，阻塞不会写在解除之后（ADR-0062）。
- 保存是"新增或整体替换"：按标识批量取回已有行，有的把新内容复制到取回的行上再更新（并发戳留在取回的实例里，不与上一次读到的版本比对），没有的新增，同一批里同一标识后者覆盖前者；与别的节点撞上的项重读后再写，最多三轮。按标识批量查找每 1000 个一批；列表按全序排序分页读完。
- 运行历史：`JobRunQuery` 的每个过滤项都是 SQL 条件（状态集合是 `IN`，计划时刻按范围比较），计划时刻降序、同刻按写入序号降序，`Skip` / `Limit` 直接分页；运行中的按开始时刻升序；每个作业最近一次已结束的运行按结束时刻取；清理删 `CompletedAt < 时刻` 的行并返回条数。可空时间列的条件不写 `!= null`：SQL 的 `NULL <= 值` 本就不成立。
- 失联恢复由领导者做（`RecoverNodesAsync`）：失联节点运行中的记录改为 `Lost`（结束时刻 = 当时）并清掉作业上指向它的占用，它抢占的触发器改回等待（清掉抢占者，没有下一次时刻的补为当时），作业已无占用的阻塞触发器改回等待，删除节点行；要求恢复的作业由引擎以恢复触发器重新入队。心跳超过 `NodeTimeout` 即失联，本节点自己不算。
- 领导租约：行不存在就插入（别人先插入 → 被占用），存在且过期就带并发戳接管（戳冲突 → 被占用），存在未过期 → `SCHEDULING_CONFLICT_002`；不可重入。持有与否按本地时钟与本地记录的到期时刻判断；续约比对本次获取的令牌后推后到期时刻，令牌不符（已释放、已被接管）→ `SCHEDULING_CONFLICT_002` 并失效；释放只删自己的行，幂等。存储不可达 → `SCHEDULING_UNAVAILABLE_002`，不改变持有状态。
- 存储在自己的作用域与事务里执行，不加入调用方的工作单元；SQLite 等库级写锁的数据库下并发写入按忙等串行，生产库按行锁不受影响。
- 表结构由应用管理（`InitTables` 只用于开发与测试）：`Data` 列用长文本类型（`text` / `nvarchar(max)` / `longtext`）；建议索引 `TriggerEntity(State, Local, NextFireAt)`、`TriggerEntity(Job)`、`TriggerEntity(AcquiredBy)`、`JobRunEntity(Job, ScheduledAt)`、`JobRunEntity(State, NodeId)`、`JobRunEntity(CompletedAt)`。

## 依赖方向

依赖 `XiHan.Framework.Scheduling.Abstractions` 与 `XiHan.Framework.Persistence.Abstractions`；不引用任何数据库驱动，具体数据库由装配的持久化 Provider 决定。

## 状态

P5：可用，对 SQLite（`Persistence.SqlSugar`）通过 `Scheduling.Conformance` 的存储、调度器与领导选举三个套件。
