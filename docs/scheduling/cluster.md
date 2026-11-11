# 集群与管理

这一页接着[作业与计划](/scheduling/jobs)讲两件事：一是运行时管理，用 `IJobScheduler` 登记、暂停、恢复、触发、取消作业，查运行历史，恢复改过的代码作业；二是多实例部署，引擎怎样轮询与抢占、`IJobStore` 共享与否的差别、三种领导选举、调度事件、配置与错误码。读完你能给管理界面提供完整的作业操作，并把调度部署到多个实例上而不重复运行。

## 为什么需要

作业一旦上线，就有人要在运行时动它：临时跑一次、出问题时暂停、改一下计划、看最近几次为什么失败。多实例部署又带来另一组问题：三个副本各跑一遍夜间清理，或者谁都以为别人在跑；跑着作业的节点宕了，那次运行就此蒸发。

框架把运行时管理收进一个批量优先的接口 `IJobScheduler`；多实例的协调交给两个窄契约：共享存储靠原子抢占保证每条到期触发器只被一个节点拿到，不共享的存储靠领导租约保证只有一个实例按计划运行。引擎的轮询、心跳、抢占、维护都在抽象包里实现一份，换 Provider 不改行为。

## 运行时管理：IJobScheduler

`IJobScheduler`（实现是 `JobSchedulerEngine`）是单例，所有方法都是批量的：以集合为入参，逐项返回结果，单条写法是 `JobSchedulerExtensions` 里的扩展方法。

| 方法 | 作用 | 主要错误 |
| --- | --- | --- |
| `ScheduleAsync(definitions)` | 新增或整体替换作业与触发器，其他节点下一个周期看到 | `SCHEDULING_NOTFOUND_002`、`SCHEDULING_VALIDATION_001` |
| `UnscheduleAsync(names)` | 删除作业与触发器；进行中的运行照常结束，历史保留 | `SCHEDULING_NOTFOUND_001` |
| `PauseAsync` / `ResumeAsync(names)` | 暂停或恢复作业的全部触发器；暂停期间手动触发仍可用，恢复后按 misfire 策略处理 | `SCHEDULING_NOTFOUND_001` |
| `PauseTriggersAsync` / `ResumeTriggersAsync(keys)` | 按 `TriggerKey(job, trigger)` 暂停或恢复单个触发器 | `SCHEDULING_NOTFOUND_003` |
| `TriggerAsync(requests)` | 在本实例立即运行并等待结果，按顺序逐项；不受暂停和领导权限制 | `SCHEDULING_NOTFOUND_001`、`SCHEDULING_CONFLICT_001`、`SCHEDULING_UNAVAILABLE_001` |
| `EnqueueAsync(requests)` | 插入一个立即到期的一次性触发器（`manual-` 前缀），由集群任一节点运行，不等待；返回触发器名 | `SCHEDULING_NOTFOUND_001` |
| `CancelRunsAsync(runIds)` | 本实例的运行立即取消令牌，其他节点的在下一个轮询周期取消 | `SCHEDULING_NOTFOUND_004` |
| `StatusesAsync(names)` / `StatusAsync(name)` / `ListStatusesAsync(query)` | 作业状态：是否暂停、各触发器的状态与下一次时刻、运行中数量、最近一次完成的结果；按名批量（与入参同序、未登记为无值）或按名字分页列出 | `SCHEDULING_VALIDATION_003`（`ListStatusesAsync` 分页越界或外来令牌） |
| `RunsAsync(query)` / `CountRunsAsync(query)` | 运行历史（只读视图 `JobRunView`）与同一组过滤条件下的总数 | `SCHEDULING_VALIDATION_002`（`RunsAsync` 分页越界） |
| `RestoreAsync(names)` | 把运行时改过的代码作业恢复为代码定义 | `SCHEDULING_NOTFOUND_005` |

`JobTriggerRequest(Name, Data)` 能从 `JobName` 隐式转换，手动触发带的数据覆盖作业数据的同名键。`TriggerAsync` 在本实例同步运行并拿到结果，适合「立即执行」；`EnqueueAsync` 只写存储，由集群任一节点运行，适合「排队跑一次」。

## 最小示例

我们为每个租户在运行时建一个同步作业。运行时作业引用的类型要事先用 `AddJobType<TJob>()` 登记：

```csharp
builder.Services.AddJobType<TenantSyncJob>();
```

```csharp
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

public sealed class TenantSyncJob : IJob
{
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success());
}

public sealed class TenantSyncPlanner(IJobScheduler scheduler)
{
    public async ValueTask<Result> EnableAsync(string tenantId, CancellationToken cancellationToken)
    {
        // 作业名不合法会抛 ArgumentException：租户 ID 只能含小写字母、数字、点、下划线与连字符
        var definition = new JobDefinition($"tenant-{tenantId}.sync", JobSchedule.Every(TimeSpan.FromMinutes(10)), typeof(TenantSyncJob))
        {
            Data = new JsonObject { ["tenant"] = tenantId },
            Retry = JobRetryPolicy.Fixed(3, TimeSpan.FromSeconds(30)),
        };
        return await scheduler.ScheduleAsync(definition, cancellationToken);
    }

    public async ValueTask<Result> DisableAsync(string tenantId, CancellationToken cancellationToken)
        => await scheduler.UnscheduleAsync($"tenant-{tenantId}.sync", cancellationToken);
}
```

调用 `EnableAsync("acme")` 后，`tenant-acme.sync` 每十分钟运行一次，失败后每 30 秒重试、最多三次。类型未登记时 `ScheduleAsync` 返回 `SCHEDULING_NOTFOUND_002`。多节点部署时每个节点都要登记同一组类型：节点抢到类型未登记的触发器时把它改为 `Error` 并记录原因。

查状态用 `StatusAsync(name)`，返回 `Result<Maybe<JobStatus>>`，`JobStatus` 里有作业的只读视图 `Job`、`NextRun`、`Paused`、`RunningCount`、各触发器的 `NextFireAt` 与 `State`、最近一次完成的结果；一次取多个用 `StatusesAsync(names)`，管理界面列全部作业用 `ListStatusesAsync(new JobStatusQuery { PageSize = 50 })`，把返回的 `ContinuationToken` 原样传给下一页，读到为空为止。

批量方法都返回与入参等长、同序的逐项结果。取消在开始前检查，已取消时抛出 `OperationCanceledException`；开始之后取消，已开始的项照常完成并给出结果，尚未开始的项以 `KERNEL_CANCELLED_001` 结束，方法不抛出。

查历史用 `RunsAsync(query)`，返回只读视图 `JobRunView`，失败的结果在 `Outcome` 里：`JobRunQuery` 按作业、状态集合、计划时刻范围（`From` 含、`To` 不含）、触发器名、执行节点、有无错误码过滤，`Skip` 缺省 0，`Limit` 缺省 100、最大 1000（`JobRunQuery.MaxLimit`）；结果按计划时刻降序。`CountRunsAsync` 用同一组过滤项给出总数，不看分页。分页越界不在构造时抛出，`RunsAsync` 返回 `SCHEDULING_VALIDATION_002`（HTTP 400）：查询常由请求绑定而来，越界是调用方可以改正的输入。

## 代码作业的运行时修改与指纹

管理界面常要改代码作业的计划。`ScheduleAsync` 替换一个代码作业时，来源记为 `Overridden`，并保留代码定义的指纹（类型键、数据、标志、说明、各触发器的名字、计划文本、数据、优先级、misfire 策略与说明的 SHA-256；重试策略与执行时限设置了才计入）。下次启动同步代码作业时：

1. 存储里是 `Overridden` 且指纹与当前代码定义相同：代码没改，保留运行时的修改。
2. 其余情况：按代码写入，来源回到 `Code`；覆盖了运行时修改的记一条警告日志。
3. 暂停状态一律保留。

`RestoreAsync` 随时把作业恢复为代码定义，保留暂停状态与同名触发器的进度；作业没有代码定义（来源 `Runtime`）时返回 `SCHEDULING_NOTFOUND_005`。删除代码作业只到下次启动为止，启动时会按代码重新写入。`JobTypeRegistry.Keys` 列出已登记的类型键，供界面新建作业时选择。

## 集群：引擎与存储

`JobSchedulerEngine` 由 Provider 特性装配，在应用进入服务状态后开始轮询，每个 `PollInterval`（缺省 1 秒）执行一个周期：

1. 应用不在服务状态（启动中、排空中）时什么都不做，只释放持有的领导租约，另一个在服务的实例下一个周期就接手。
2. 首个周期把代码登记的作业写入存储；失败时记日志，下个周期重试。
3. 存储共享时写节点心跳。
4. 续约领导租约，没有时尝试获取（资源 `LeaseResource`，存活 `LeaseTimeToLive`）。
5. 领导者每 `HousekeepingInterval`（缺省 60 秒）维护一次：恢复失联节点，删除结束早于 `HistoryRetention`（缺省 7 天）的历史。
6. 取消本节点上被请求取消的运行。
7. 运行到期的本地触发器与本实例的本地重试。
8. 抢占到期触发器并在后台运行：名额 = `min(AcquireBatchSize, MaxConcurrentRuns − 本节点运行中数量)`。

`IJobStore.IsShared` 决定集群唯一的作业怎样保证只跑一次：

| 存储 | `IsShared` | 只跑一次的机制 |
| --- | --- | --- |
| `Scheduling.Database` | `true` | 抢占：`AcquireDueAsync` 原子地把到期触发器改成 `Acquired(nodeId)`，每条只被一个节点拿到；`TryBeginRunAsync(exclusive: true)` 原子地占用作业的 `ActiveRunId`，实现全集群串行。节点按周期写心跳，超过 `NodeTimeout` 即失联 |
| `Scheduling.InMemory` | `false` | 领导权：只有持有租约的实例按计划运行集群唯一的作业，其他实例把抢到的按计划触发器放回等待；入队、重试这类一次性触发器只有本进程看得到，不受领导权限制 |

触发器的状态：

| 状态 | 含义 |
| --- | --- |
| `Waiting` | 等待到期 |
| `Paused` | 已暂停 |
| `Acquired` | 已被某节点抢占；间隔计划在运行结束前保持这个状态 |
| `Blocked` | 到期但作业不允许并发、上一次未结束 |
| `Complete` | 没有下一次（一次性已运行、次数用尽、超过 `EndAt`） |
| `Error` | 无法运行（作业类型未登记），原因在 `Error` 字段 |

停止时引擎先停轮询，再等待进行中的运行结束；宿主的停止令牌触发时取消这些运行，最后释放领导租约。启动之前与开始停止之后，本实例的 `TriggerAsync` 返回 `SCHEDULING_UNAVAILABLE_001`（HTTP 503）、不起新的运行；`EnqueueAsync` 只写存储，不受影响。

数据库 Provider 经 `persistence.relational@1` 的仓储读写，每个存储操作是一次独立的管道调用与事务，不加入调用方的工作单元；需要建五张表（作业、触发器、运行、节点、租约），每个节点的 `XiHan:Scheduling:NodeId` 必须唯一，见 [Scheduling.Database](/packages/scheduling-database)。

## 领导选举

`ILeaderElection.TryAcquireAsync(resource, timeToLive)` 在资源空闲或租约已过期时授予 `LeaderLease`，被他人持有时返回 `SCHEDULING_CONFLICT_002`。`LeaderLease` 有 `Holder`、`ExpiresAt`、`IsHeld`，`RenewAsync` 延长一个存活时间（已过期或被接管时同样返回 `SCHEDULING_CONFLICT_002`），`ReleaseAsync` 立即放弃。租约不可重入。

| 实现 | 持有者 | 存储 | 不可达时 |
| --- | --- | --- | --- |
| `InMemoryLeaderElection` | 每次获取新生成的随机值 | 进程内字典，只在本进程内互斥 | — |
| `DatabaseLeaderElection` | 节点 ID（`XiHan:Scheduling:NodeId`）加随机令牌 | 与作业同库的一行，并发戳裁决 | `SCHEDULING_UNAVAILABLE_002`，持有状态不变 |
| `KubernetesLeaderElection` | Pod 名 | `coordination.k8s.io/v1` Lease，resourceVersion 裁决并发 | `SCHEDULING_UNAVAILABLE_002`，下个周期重试 |

应用自己的单例工作也可以用它竞争领导权：

```csharp
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;

public sealed class IndexRebuilder(ILeaderElection election)
{
    public async ValueTask<bool> TryRebuildAsync(CancellationToken cancellationToken)
    {
        var acquired = await election.TryAcquireAsync(new JobName("search.rebuild"), TimeSpan.FromMinutes(5), cancellationToken);
        if (!acquired.TryGetValue(out var lease))
        {
            return false;   // 别的实例正在做，或选举存储不可达
        }

        await using (lease)
        {
            return true;    // 长于存活时间的工作要定期 RenewAsync，续约失败时停下
        }
    }
}
```

在 Kubernetes 上，示例应用 KubernetesApp 在 Pod 内装配 `KubernetesSchedulingFeature`，在本地退回 Full 组合自带的进程内选举；`JobSchedulerEngine.IsLeader` 与 `NodeId` 能放进端点展示当前谁是领导者：

```csharp
using XiHan.Framework.Profiles.Full;
using XiHan.Framework.Scheduling.Kubernetes;

// 在 Pod 里（API Server 的环境变量存在）用 Lease 做领导选举；本地运行退回进程内选举
var inCluster = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") is not null;
builder.AddXiHan(xihan =>
{
    xihan.UseFullProfile();
    if (inCluster)
    {
        xihan.UseFeature<KubernetesSchedulingFeature>();
    }
});
```

Lease 名缺省是 `xihan-scheduler`，用 `XiHan:Scheduling:Kubernetes:Lease` 修改；Pod 的 ServiceAccount 需要对 `coordination.k8s.io` 下的 `leases` 有 `get`、`update`、`patch`、`create` 权限，`xihan pack` 能按应用清单生成这份 Role，见 [Kubernetes](/deployment/kubernetes)。

## 事件

装配了消息能力时，引擎经 `IMessagePublisher` 发布 CloudEvents 信封：`Source` 为 `/scheduling`，`Subject` 为运行 ID（作业级事件为作业名），`Data` 是 `SchedulingEventData` 的 JSON（作业、触发器名、运行 ID、触发来源、状态、各时刻、错误码与消息、`Attempt`、`RetryOf`、节点）。没有发布者时跳过，发布失败只记日志。

| 主题（`SchedulingTopics`） | 时机 |
| --- | --- |
| `scheduling.job.scheduled` / `scheduling.job.unscheduled` | 作业登记或替换 / 删除 |
| `scheduling.job.paused` / `scheduling.job.resumed` | 作业暂停 / 恢复 |
| `scheduling.run.started` | 运行开始 |
| `scheduling.run.succeeded` / `scheduling.run.failed` / `scheduling.run.canceled` | 运行结束 |
| `scheduling.run.lost` | 运行所在节点失联 |

订阅方式见[发布与订阅](/messaging/pubsub)。Kafka 关了自动建主题时这 9 个主题要预先建好，见 [Messaging.Kafka](/packages/messaging-kafka)。

## 配置

引擎选项在 `XiHan:Scheduling`（`SchedulerOptions`），两个 Provider 读同一节；值不合法时启动抛 `ArgumentException`。

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `PollIntervalSeconds` | 正数 | 1 | 轮询周期 |
| `NodeId` | 字符串 | `机器名:进程 ID:8 位随机` | 节点身份：心跳、抢占、租约持有者都用它；多节点时每个节点唯一 |
| `MaxConcurrentRuns` | ≥ 1 | 32 | 本节点同时运行的上限 |
| `AcquireBatchSize` | ≥ 1 | 16 | 一个周期最多抢占的触发器数 |
| `MisfireThresholdSeconds` | ≥ 0 | 60 | 落后超过它算 misfire |
| `LeaseResource` | 作业名文法 | `scheduling.jobs` | 领导租约的资源名 |
| `LeaseTimeToLiveSeconds` | 正数 | 30 | 领导租约的存活时间 |
| `NodeTimeoutSeconds` | 正数 | 90 | 心跳超过它即失联 |
| `HousekeepingIntervalSeconds` | 正数 | 60 | 领导者维护周期 |
| `HistoryRetentionDays` | ≥ 0 | 7 | 运行历史保留天数，0 为不删 |

各 Provider 自己的键（`XiHan:Scheduling:InMemory:MaxRuns`、`XiHan:Scheduling:Database:HealthProbeIntervalSeconds`、`XiHan:Scheduling:Kubernetes:*`）见对应包页。

## 错误码

| 错误码 | 含义 |
| --- | --- |
| `SCHEDULING_NOTFOUND_001` | 作业未登记 |
| `SCHEDULING_NOTFOUND_002` | 作业类型未登记 |
| `SCHEDULING_NOTFOUND_003` | 触发器不存在 |
| `SCHEDULING_NOTFOUND_004` | 运行不存在或已结束 |
| `SCHEDULING_NOTFOUND_005` | 作业没有代码定义，不能恢复 |
| `SCHEDULING_VALIDATION_001` | 作业定义不合法：触发器名重复、日历未登记 |
| `SCHEDULING_VALIDATION_002` | 运行历史查询的分页越界（HTTP 400） |
| `SCHEDULING_VALIDATION_003` | 作业状态列表的分页越界或续页令牌无效（HTTP 400） |
| `SCHEDULING_CONFLICT_001` | 作业正在运行且不允许并发 |
| `SCHEDULING_CONFLICT_002` | 领导租约被其他持有者占用 |
| `SCHEDULING_CONFLICT_003` | 运行被取消 |
| `SCHEDULING_UNAVAILABLE_001` | 本实例的调度器尚未启动或已开始停止，`TriggerAsync` 不起新的运行 |
| `SCHEDULING_UNAVAILABLE_002` | 领导选举的存储（数据库、API Server）不可达或超时 |
| `SCHEDULING_UNAVAILABLE_003` | 作业存储不可达 |
| `SCHEDULING_UNAVAILABLE_004` | 作业存储容量已满（内存实现的运行历史全部在运行中） |
| `SCHEDULING_TIMEOUT_001` | 运行超过作业的执行时限 |
| `SCHEDULING_UNEXPECTED_001` | 作业抛出未处理异常 |

错误文案在消息目录 `scheduling` 里，十种区域都有译文，见[全球化和本地化](/common/localization)。

## 常见问题

### 三个副本各跑了一遍夜间作业

只装配了 `Scheduling.InMemory`：每个副本有自己的存储和进程内租约，都当自己是领导者。在 Kubernetes 上加 `KubernetesSchedulingFeature`；需要运行时登记的作业在副本间可见、节点宕机后恢复运行，换 `DatabaseSchedulingFeature`。

### 领导者 Pod 滚动更新时会不会停一阵

不会等租约到期。排空中的实例在每个周期释放租约，另一个在服务的实例下一个周期就接手。只有实例被强杀、没来得及释放时，才要等 `LeaseTimeToLive`（缺省 30 秒）过期。

### `TriggerAsync` 返回 `SCHEDULING_UNAVAILABLE_001`

本实例的引擎还没启动或已经开始停止，这两段时间没有轮询照看运行，所以不起新的。排空期间要排队运行的用 `EnqueueAsync`，它只写存储，由别的节点运行。

### 运行时改了作业计划，重新发布后被覆盖了

代码定义变了。只有代码定义的指纹与存储里 `Overridden` 记录保存的指纹相同，运行时修改才保留；代码一改就以代码为准并记警告。要让修改长期有效，把它写回代码；临时调整用 `PauseAsync`，暂停状态一律保留。

## 相关包与下一步

- 包参考：[Scheduling.Abstractions](/packages/scheduling-abstractions)、[Scheduling.InMemory](/packages/scheduling-inmemory)、[Scheduling.Database](/packages/scheduling-database)、[Scheduling.Kubernetes](/packages/scheduling-kubernetes)
- 事件的订阅：[发布与订阅](/messaging/pubsub)；部署到 Kubernetes：[Kubernetes](/deployment/kubernetes)
- Provider 必过的套件：[一致性套件与门禁](/testing/conformance)
- 规范：RFC-0013《调度与领导选举》
- 下一步：[定义与发布](/workflow/definitions)，工作流的 `Delay` 活动与定时启动复用这里的 cron 文法
