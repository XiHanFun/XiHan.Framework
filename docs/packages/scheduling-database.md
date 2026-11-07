# XiHan.Framework.Scheduling.Database

调度的数据库 Provider，提供 `scheduling.jobs@1` 与 `scheduling.leader@1`。作业、触发器、运行历史、节点与领导租约存放在应用的关系库里，经 `persistence.relational@1` 的仓储读写；多个节点共用一个库，每条到期触发器由先抢到的节点运行。

适用于多实例部署、需要持久化运行时作业与运行历史的场景。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Scheduling.Database` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `scheduling.jobs@1`、`scheduling.leader@1`（`role: provider`）；依赖 `kernel.hosting@1`、`persistence.relational@1` |
| 特性入口 | `DatabaseSchedulingFeature`（清单 ID `xihan.scheduling.database`） |
| 配置节 | `XiHan:Scheduling`（引擎选项）、`XiHan:Scheduling:Database`（`HealthProbeIntervalSeconds`）；清单 `contributes.config` 声明两者的最近公共节 `XiHan:Scheduling`（RFC-0002「配置节」） |
| 依赖 | `XiHan.Framework.Scheduling.Abstractions`、`XiHan.Framework.Persistence.Abstractions`；不引用数据库驱动 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

引擎仍是抽象包的 `JobSchedulerEngine`，本包实现它的两个契约：

- `DatabaseJobStore`（`IJobStore`，`IsShared` 为真）。每个存储操作在根提供者的新作用域里经 `App.InvokeAsync` 走一遍完整管道，仓储在 Transaction 阶段内使用，返回即提交。存储不加入调用方的工作单元，跨节点的互斥因此成立：一个节点提交的抢占与占用，别的节点下一次读立刻看得见。
- `DatabaseLeaderElection`（`ILeaderElection`）。租约是同一个库里的一行，获取与续约各是一次独立的管道调用；释放不经管道（在新作用域里开可写工作单元删行后提交）：它发生在排空中的周期与引擎停止时，那时管道已拒绝调用，经管道就删不掉行、别的节点要等租约到期（ADR-0091）。

集群分发靠并发戳做的比较并交换：

| 操作 | 做法 |
| --- | --- |
| 抢占 `AcquireDueAsync` | 按 `State = Waiting`、非本地、`NextFireAt <= 到期时刻` 取候选（到期时刻升序、优先级降序、键升序，取名额的三倍），带并发戳批量改成 `Acquired`；被别的节点先改走的跳过并用下一批补齐，拿满名额或候选用尽为止 |
| 独占开始 `TryBeginRunAsync(exclusive: true)` | 作业的 `ActiveRunId` 指向一条仍在运行的记录即拒绝；否则带并发戳换成本次运行，撞上别的节点就重读再判，最多三次 |
| 完成 `CompleteRunAsync` | 写回运行记录、清掉指向它的占用、把该作业阻塞中的触发器改回等待 |
| 阻塞 `BlockTriggerAsync` | 同一个事务里重判占用：仍被占就带并发戳改写一次作业行、把触发器存为 `Blocked`，占用已释放就存为 `Waiting`；与完成互斥，阻塞不会写在解除之后 |
| 失联恢复 `RecoverNodesAsync` | 由领导者执行：失联节点运行中的记录改为 `Lost`，清掉占用，它抢占的触发器改回等待，删除节点行 |
| 领导租约 | 行不存在就插入；存在且过期就带并发戳接管；存在未过期即 `SCHEDULING_CONFLICT_002`。续约比对本次获取的随机令牌，令牌不符（已释放、已被接管）即失去租约 |

节点身份是 `XiHan:Scheduling:NodeId`，同时用于心跳、抢占者与租约持有者；缺省为 `机器名:进程 ID:8 位随机`，多节点部署时保证每个节点唯一即可。

## 何时使用

- 多实例部署，需要集群唯一的作业只跑一次、运行时作业与历史在节点间共享、节点宕机后自动恢复。
- 单实例但要求进程重启后保留运行时登记的作业与运行历史。
- 只需要在 Kubernetes 副本间选一个领导、不需要共享存储时，[Scheduling.Kubernetes](./scheduling-kubernetes) 加内存存储更轻。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Scheduling.Database --prerelease
dotnet add package XiHan.Framework.Persistence.SqlSugar --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。装配一个持久化 Provider 和本包；装配后顶替默认的 `Scheduling.InMemory`：

```csharp
using SqlSugar;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Scheduling.Database;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseSchedulingFeature>());
```

连接串来自绑定 `db`（`XiHan:Bindings:db`），持久化 Provider 的写法见[持久化](/persistence/overview)。用 EF Core 的应用在自己的 `DbContext` 里登记本包的五个实体，并给 `Data` 列用长文本类型：

```csharp
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Scheduling.Database.Entities;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobEntity>(static t => t.Property(static x => x.Data).HasColumnType("text"));
        modelBuilder.Entity<TriggerEntity>(static t => t.Property(static x => x.Data).HasColumnType("text"));
        modelBuilder.Entity<JobRunEntity>(static t => t.Property(static x => x.Data).HasColumnType("text"));
        modelBuilder.Entity<SchedulerNodeEntity>();
        modelBuilder.Entity<SchedulerLeaseEntity>();
        // 索引由实体上的 [EntityIndex] 建出
        modelBuilder.ApplyPersistenceConventions(Database);
    }
}
```

## 配置

本包读两节。引擎选项在 `XiHan:Scheduling`（完整列表见 [Scheduling.Abstractions](./scheduling-abstractions#配置)）；本包自己的只有健康探针间隔：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Scheduling:NodeId` | 字符串 | `机器名:进程 ID:8 位随机` | 节点身份，每个节点唯一 |
| `XiHan:Scheduling:LeaseTimeToLiveSeconds` | 正数 | 30 | 领导租约存活时间 |
| `XiHan:Scheduling:NodeTimeoutSeconds` | 正数 | 90 | 心跳超过它即失联 |
| `XiHan:Scheduling:HousekeepingIntervalSeconds` | 正数 | 60 | 领导者维护周期 |
| `XiHan:Scheduling:Database:HealthProbeIntervalSeconds` | 正数 | 15 | 健康项重探间隔，也是单次探测的超时 |

```json
{
  "XiHan": {
    "Bindings": { "db": "Host=localhost;Database=app;Username=app" },
    "Scheduling": {
      "NodeId": "node-1",
      "PollIntervalSeconds": 1,
      "MaxConcurrentRuns": 32,
      "AcquireBatchSize": 16,
      "LeaseTimeToLiveSeconds": 30,
      "NodeTimeoutSeconds": 90,
      "HistoryRetentionDays": 7,
      "Database": { "HealthProbeIntervalSeconds": 15 }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `DatabaseSchedulingFeature` | 特性：登记五个实体的仓储，装配存储、选举、引擎与健康项；应用进入服务状态后引擎开始轮询；应用开始排空后的周期与停止时释放租约（删行），另一个节点随即接手 |
| `DatabaseSchedulingOptions` | `HealthProbeInterval` |
| `Storage.DatabaseJobStore` | `IJobStore` 实现（单例） |
| `Leadership.DatabaseLeaderElection` | `ILeaderElection` 实现（单例），持有者是节点 ID |
| `Entities.JobEntity` | 作业行：主键是作业名，`Data` 是 JSON 文本，`ActiveRunId` 的占用靠并发戳；重试策略与执行时限是五个可空列（次数、等待与上限按毫秒、退避方式整数值） |
| `Entities.TriggerEntity` | 触发器行：主键 `作业名/触发器名`，`Schedule` 是 `JobSchedule.ToString()` 的文本，枚举存整数值，另有可空的 `Attempt`（空按 1）与 `RetryOf` |
| `Entities.JobRunEntity` | 运行记录行：`Sequence` 是首次写入分配的写入序号，作同一时刻排序的次关键字（UUID v7 只到毫秒单调） |
| `Entities.SchedulerNodeEntity` | 节点心跳行 |
| `Entities.SchedulerLeaseEntity` | 租约行：主键是资源名，`Holder`、`Token`、`ExpiresAt`、并发戳 |

## 使用示例

两个节点连同一个库，各自用不同的节点 ID 启动，代码完全相同：

```json
{ "XiHan": { "Scheduling": { "NodeId": "orders-a" } } }
```

```json
{ "XiHan": { "Scheduling": { "NodeId": "orders-b" } } }
```

一个节点运行时登记的作业，另一个节点下一个周期就能抢到它的触发器；节点 `orders-a` 宕机超过 90 秒后，持有租约的 `orders-b` 把它运行中的记录标为 `Lost`，`RequestsRecovery = true` 的作业重新入队一次。

## 行为与限制

- 每个存储操作一个事务：读是快照，写在动作返回时提交，动作内任一非竞争失败整体回滚。管道失败（应用不在服务状态、仓储异常、提交失败）→ `SCHEDULING_UNAVAILABLE_003`，原错误在 `Inner`；调度语义的错误（作业不存在、运行不存在）原样返回。
- 租约存储不可达 → `SCHEDULING_UNAVAILABLE_002`，不改变持有状态；持有与否按本地时钟与本地记录的到期时刻判断。
- 管道失败 `SCHEDULING_UNAVAILABLE_003` 的 `reason` 是原错误码或异常消息。
- 租约：不可重入；续约比对本次获取的令牌后推后到期时刻，令牌不符（已释放、已被接管）即失效并返回 `SCHEDULING_CONFLICT_002`；释放只删自己的行，幂等。
- 失联恢复：失联节点运行中的记录结束时刻记为恢复当时；它抢占的触发器改回等待时清掉抢占者，没有下一次时刻的补为当时；作业已无占用的阻塞触发器改回等待；要求恢复的作业由引擎以恢复触发器重新入队。心跳超过 `NodeTimeout` 即失联，本节点自己不算。
- 阻塞与完成都带并发戳改写作业行之后才继续，两边谁后提交谁看到对方（ADR-0062）；独占开始中「结束了但没清掉的占用」视为空闲。
- 保存的并发戳留在取回的实例里，不与上一次读到的版本比对；同一批里同一标识后者覆盖前者；按标识批量查找每 1000 个一批，列表按全序排序分页读完。
- 运行历史：状态集合是 `IN`，计划时刻按范围比较；运行中的按开始时刻升序；每个作业最近一次已结束的运行按结束时刻取；清理返回删除条数。可空时间列的条件不写 `!= null`（SQL 的 `NULL <= 值` 本就不成立）。
- `JobEntity.Source`、`TriggerEntity.State` / `MisfirePolicy` / `RunAs`、`JobRunEntity.State` / `Trigger` 存枚举整数值，`Data` 是 JSON 文本；重试策略与执行时限在作业行上是五个可空列，触发器行与运行行另有可空的 `Attempt`（空按 1）与 `RetryOf`。
- 保存是「新增或整体替换」：按标识批量取回已有行（每 1000 个一批），有的更新、没有的新增，与别的节点撞上的项重读后再写，最多三轮。
- 运行历史的每个过滤项都是 SQL 条件，按计划时刻降序、同刻按写入序号降序分页；清理删除 `CompletedAt` 早于保留期的行。
- 表结构由应用管理，`InitTables` 只用于开发与测试。`Data` 列用长文本类型（`text` / `nvarchar(max)` / `longtext`）。索引由实体上的 `[EntityIndex]` 声明，SqlSugar 的 `InitTables` 与 EF Core 的 `ApplyPersistenceConventions(Database)` 都按它建（ADR-0072）：`IX_TriggerEntity_St_Lo_NeFiAt`（`(State, Local, NextFireAt)`，到期抢占的轮询）、`UX_TriggerEntity_Jo_Na`（`(Job, Name)` 唯一）、`IX_JobRunEntity_St_NoId`（`(State, NodeId)`，取消请求的轮询、失联节点的运行）、`IX_JobRunEntity_Jo_CoAt`（`(Job, CompletedAt)`，每个作业最近一次结束的运行）、`IX_JobRunEntity_CoAt`（`CompletedAt`，保留期清理）、`IX_JobRunEntity_ScAt`（`ScheduledAt`，运行记录倒序分页）；作业、节点、租约表只按主键读写。
- SQLite 这类库级写锁的数据库下，并发写入按忙等串行；行锁数据库不受影响。
- 健康项 `scheduling.database.leader`：启动时同步读一次租约行，之后按 `HealthProbeIntervalSeconds` 在后台重探（独立作用域、只读工作单元、读后回滚、不经管道）；租约表不可达时条目为 Degraded（`KERNEL_UNAVAILABLE_005`），应用仍在服务。
- 对 SQLite（`Persistence.SqlSugar`）通过 `Scheduling.Conformance` 的存储、调度器与领导选举三个套件。

## 相关

- [调度与后台作业](/scheduling/jobs)
- [Scheduling.Abstractions](./scheduling-abstractions)
- [Scheduling.InMemory](./scheduling-inmemory)
- [Persistence.SqlSugar](./persistence-sqlsugar)
- [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)
- [Workflow.Database](./workflow-database)
- 规范：RFC-0013《调度与领导选举》；ADR-0020、ADR-0060
