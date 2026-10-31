# 调度与后台作业

本章讲怎样在应用里按计划运行后台工作：编写作业、登记作业与触发器、选择计划与 misfire 策略、重试与执行时限、运行时管理作业、多实例部署下的集群分发与领导选举。读者是要给特性加定时任务，或者要在多个实例间分配作业的开发者。

完整 API 见 [Scheduling.Abstractions 包](../packages/scheduling-abstractions)。

## 作业、触发器与运行

调度子系统沿用 Quartz 的分法：作业描述「做什么」，触发器描述「什么时候做」。一个作业可以挂多个触发器，每个触发器有自己的计划、数据、优先级和 misfire 策略，各自推算下一次运行。作业没有触发器时只能手动触发，定义照样留在存储里。触发器每到期一次，或者被手动触发、失败重试、失联恢复一次，就产生一次运行（`JobRun`）。

运行与一次请求同构。`JobDispatcher` 为每次运行创建新的 DI 作用域和新的调用上下文，经应用的完整管道执行 `IJob.RunAsync`，所以租户、授权、审计、追踪中间件对作业照常生效。上下文的关联 ID（`xihan.correlation`）就是运行 ID，来源是 `InvocationProtocols.Job`，目标是作业名，日志和追踪里能按运行 ID 串起一次执行。作业返回失败的 `Result` 记为失败；抛出的异常被翻译成 `SCHEDULING_UNEXPECTED_001`，同样记为失败。

引擎和这些规则都在抽象包里（ADR-0020）：计划推算、misfire、并发阻塞、重试、失联恢复、历史只实现一份。Provider 只需实现作业存储 `IJobStore` 与领导选举 `ILeaderElection`，内存实现和数据库实现因此行为一致，并且通过同一个一致性套件。5.0 不引入 Quartz.NET，cron、日历、misfire、集群分发都是自实现的，也没有 `Scheduling.Quartz` 包。

## 选择 Provider

调度提供两个能力（RFC-0013《调度与领导选举》）：

| 能力 | 服务 | 默认实现 | 显式 Provider |
| --- | --- | --- | --- |
| `scheduling.jobs@1` | `IJobScheduler`、`JobSchedulerEngine`、`IJobStore` | [Scheduling.InMemory](../packages/scheduling-inmemory) | [Scheduling.Database](../packages/scheduling-database) |
| `scheduling.leader@1` | `ILeaderElection` | Scheduling.InMemory（进程内租约） | Scheduling.Database（租约行）、[Scheduling.Kubernetes](../packages/scheduling-kubernetes)（Lease 对象） |

三种常见部署：

| 部署 | 装配 | 效果 |
| --- | --- | --- |
| 单实例 | `InMemorySchedulingFeature`（[`Profiles.Full`](../packages/profiles-full) 已包含） | 作业、触发器、历史都在进程内，重启后运行时登记的作业与历史丢失 |
| Kubernetes 多副本，作业只需在一个 Pod 上跑 | Full 组合加 `KubernetesSchedulingFeature` | 存储仍在各自进程内，只有持有 Lease 的 Pod 按计划运行集群唯一的作业；运行时登记的作业不在副本间共享 |
| 多实例，需要持久化与分发 | 持久化 Provider 加 `DatabaseSchedulingFeature` | 所有节点共用一个库，每条到期触发器由先抢到的节点运行，节点失联由领导者恢复 |

::: warning 内存存储不在实例之间共享
只装配 `Scheduling.InMemory` 时，每个实例都有自己的存储和自己的进程内租约，每个实例都当自己是领导者，集群唯一的作业会在每个实例上各跑一份。多实例部署至少要换掉领导选举（`Scheduling.Kubernetes`），需要跨实例共享运行时作业、历史与失联恢复时换 `Scheduling.Database`。
:::

## 编写与登记作业

作业实现 `IJob`，是作用域服务，可以注入作用域内的其他服务。运行信息在 `JobRun` 里：`Id`、`Name`、`TriggerName`（手动运行为空）、`ScheduledAt`、`Trigger`（`Schedule`、`Manual`、`Recovery`、`Retry`）、`Attempt`（第几次尝试，从 1 起）、`RetryOf`（重试时为首次运行的 ID）与 `Data`。

```csharp
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

public interface IReportService
{
    ValueTask<Result> GenerateAsync(string format, CancellationToken cancellationToken);
}

public sealed class NightlyReportJob(IReportService reports) : IJob
{
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
    {
        // Data 已按 作业 ← 触发器 ← 手动触发 逐层合并
        var format = run.Data["format"]?.GetValue<string>() ?? "pdf";
        return reports.GenerateAsync(format, cancellationToken);
    }
}
```

`cancellationToken` 在三种情况下取消：有人请求取消这次运行（`CancelRunsAsync`）、超过作业的执行时限、宿主停止。作业要把它传给下游调用，否则时限与取消都要等作业自己返回才生效。管道内的其他代码可以用 `JobDispatcher.GetRun(context)` 取到当前运行。

登记有四个扩展方法，都在 `IServiceCollection` 上，可以在 `Program.cs` 里调用，也可以在特性的 `Configure` 里调用：

| 方法 | 作用 |
| --- | --- |
| `AddJob<TJob>(name, schedule)` | 登记作业类型和一个代码作业；计划不是 `Manual` 时生成一个名为 `default` 的触发器 |
| `AddJob<TJob>(JobDefinition)` | 登记作业类型和一个完整定义的代码作业（多个触发器、数据、并发、重试、时限） |
| `AddJobType<TJob>()` / `AddJobType<TJob>(key)` | 只登记作业类型（键缺省为类型全名），供运行时用 `ScheduleAsync` 新建的作业引用 |
| `AddJobCalendar(name, calendar)` | 按名登记排除日历，触发器的计划用 `Calendar` 引用 |

同名重复 `AddJob` 是编程错误，启动时抛出。代码登记的作业在引擎首个周期写入存储，来源记为 `Code`。

```csharp
using System.Text.Json.Nodes;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Calendars;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.InMemory;

var builder = WebApplication.CreateBuilder(args);
var shanghai = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

builder.Services.AddJobCalendar("holidays", new HolidayCalendar([new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2)], shanghai));

// 最简写法：一个 cron 触发器
builder.Services.AddJob<CleanupJob>("cleanup.nightly", JobSchedule.FromCron("0 3 * * *", "Asia/Shanghai"));

// 完整定义：两个触发器、作业数据、重试与执行时限
builder.Services.AddJob<NightlyReportJob>(new JobDefinition("reports.nightly", typeof(NightlyReportJob))
{
    Triggers =
    [
        new JobTriggerDefinition("workdays", JobSchedule.FromCron("0 0 6 * * MON-FRI", "Asia/Shanghai") with { Calendar = "holidays" }),
        new JobTriggerDefinition("month-end", JobSchedule.FromCron("0 0 22 L * ?", "Asia/Shanghai"))
        {
            Data = new JsonObject { ["format"] = "xlsx" },
            Priority = 8,
            MisfirePolicy = MisfirePolicy.Skip,
        },
    ],
    Data = new JsonObject { ["format"] = "pdf" },
    Retry = JobRetryPolicy.Exponential(3, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30)),
    Timeout = TimeSpan.FromMinutes(20),
    RequestsRecovery = true,
});

builder.AddXiHan(xihan => xihan.UseFeature<InMemorySchedulingFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();

public sealed class CleanupJob : IJob
{
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success());
}
```

`NightlyReportJob` 来自上一个示例，`IReportService` 的实现由应用自己注册。

`JobDefinition` 的其余选项：

| 属性 | 缺省 | 说明 |
| --- | --- | --- |
| `Triggers` | 空 | 空为只能手动触发；同一作业内触发器名重复是 `SCHEDULING_VALIDATION_001` |
| `Data` | 空 | 传给作业的数据，被触发器 `Data`、手动触发的数据逐层覆盖同名键 |
| `AllowConcurrent` | `false` | `false` 时整个集群同一作业同一时刻最多一次运行 |
| `RunOnEveryInstance` | `false` | `true` 时每个实例按自己的时钟各自运行，不经存储抢占 |
| `RequestsRecovery` | `false` | 运行所在节点失联时由领导者重新入队一次（`JobTrigger.Recovery`） |
| `Retry` | 空 | 失败运行的重试策略，见[重试与执行时限](#重试与执行时限) |
| `Timeout` | 空 | 单次执行时限，大于零、不超过 30 天（`JobDefinition.TimeoutLimit`） |
| `Description` | 空 | 说明 |

作业名、触发器名与领导租约的资源名用同一个文法：`^[a-z0-9][a-z0-9._-]{0,127}$`（`JobName.MaxLength` = 128）。`JobName` 可以从字符串隐式转换，不合法时抛 `ArgumentException`；名字来自外部输入时先用 `JobName.IsValid` 判断。

`JobTriggerDefinition` 有 `Priority`（缺省 5，`JobTriggerDefinition.DefaultPriority`；到期的触发器多于本周期可运行名额时，大者先运行）、`MisfirePolicy`（缺省 `RunOnce`）、`Data` 与 `Description`。触发器的计划不能是 `Manual`：没有触发器的作业已经是只手动触发。

## 计划

`JobSchedule` 有四种：

| 写法 | 含义 |
| --- | --- |
| `JobSchedule.Manual` | 只手动触发 |
| `JobSchedule.Every(interval, repeatCount)` | 固定延迟：上一次运行完成后再等一个间隔；`repeatCount` 是总运行次数，空为无限 |
| `JobSchedule.FromCron(expression, timeZoneId)` | cron 表达式；时区是 `TimeZoneInfo` 的 ID（IANA 或 Windows 均可），空为 UTC，找不到的时区抛 `ArgumentException` |
| `JobSchedule.Once(at)` | 在 `at` 运行一次 |

每种计划都可以用 `with` 加三个窗口条件：`StartAt`（之前不运行，间隔计划的首次落在 `StartAt + 间隔`）、`EndAt`（之后没有下一次，触发器转为 `Complete`）与 `Calendar`（排除日历名）。计划能和文本互相转换：`ToString()` 产生 `every 00:10:00 x5`、`cron 0 0 9 * * MON-FRI tz=Asia/Shanghai`、`once 2026-11-01T00:00:00.0000000+00:00`，尾部可带 `start=`、`end=`、`calendar=`；`JobSchedule.Parse` / `TryParse` 读回。存储以这段文本持久化，管理界面也可以直接让用户填它。

### cron 文法

`CronExpression` 精确到秒，采用 Quartz 风格的文法：

| 段数 | 各段 |
| --- | --- |
| 5 段 | `分 时 日 月 周`，秒为 0 |
| 6 段 | `秒 分 时 日 月 周` |
| 7 段 | 6 段再加 `年`（1970 到 2099） |

每段支持 `*`、数值、列表 `a,b`、范围 `a-b`、步长 `*/n`、`a/n` 与 `a-b/n`；日与周可写 `?`（等同 `*`）；月可写 `JAN` 到 `DEC`，周可写 `SUN` 到 `SAT`，不区分大小写。日段另有 `L`（当月最后一天）、`L-n`、`nW`（离 n 日最近的工作日，不跨月）、`LW`；周段另有 `nL` / `FRIL`（当月最后一个周 n）与 `n#k` / `FRI#2`（当月第 k 个周 n，k 为 1 到 5）。日和周同时限定时任一匹配即可。

::: warning 星期数字按 Unix 约定
`0` 与 `7` 都是周日，`1` 是周一。Quartz 的 `1` 是周日，从 Quartz 照搬带星期数字的表达式会错一天，写成 `MON`、`FRI` 这类名字可以避开。
:::

带时区的 cron 按该时区的墙上时间匹配：夏令时跳过的时刻不运行，重复出现的时刻只运行第一次。向前最多搜索 5 年，找不到匹配（如 2 月 30 日）即没有下一次。

### 排除日历

日历实现 `IJobCalendar.IsExcluded(at)`。推算下一次时跳过被排除的时刻，间隔计划的下一次也顺延到第一个不被排除的时刻；一次性计划的时刻被排除就不再运行。连续 10000 个候选时刻都被排除时视为没有下一次，触发器转为 `Complete`。

| 日历 | 排除 |
| --- | --- |
| `HolidayCalendar(dates, timeZone)` | 指定日期（整天） |
| `WeeklyCalendar(excludedDays, timeZone)` | 每周指定星期几；不给星期时排除周六、周日 |
| `DailyCalendar(from, to, timeZone)` | 每天一个时段（`from` 含、`to` 不含），`from` 晚于 `to` 表示跨午夜 |
| `CronCalendar(expression, timeZone)` | 与 cron 匹配的秒，如 `* * 0-7 * * *` 排除每天 0 点到 8 点 |
| `CompositeCalendar(calendars)` | 任一成员排除即排除 |

时区参数都缺省为 UTC。触发器引用了未登记的日历名，登记时返回 `SCHEDULING_VALIDATION_001`。

## misfire

触发器的到期时刻落后当前时刻超过 `MisfireThreshold`（缺省 60 秒）时算 misfire。进程停机、作业暂停、节点失联、上一次运行没结束而被阻塞，都会造成 misfire。处理按触发器的 `MisfirePolicy`：

| 策略 | 行为 |
| --- | --- |
| `RunOnce`（缺省） | 立即运行一次，计划时刻记为现在，再从现在推算下一次 |
| `Skip` | 不补运行，从现在推算下一次 |
| `RunAll` | 按原计划时刻逐次补齐，每个轮询周期补一次，直到追平 |

没有 misfire 时，下一次从计划时刻推算而不是从实际开始时刻推算，cron 的节奏不会漂移。

## 并发、本地作业与失联恢复

`AllowConcurrent = false`（缺省）的作业在整个集群同一时刻最多一次运行。按计划的触发遇到上一次没结束时，触发器转为 `Blocked`，到期时刻不变；上一次结束时存储把它改回 `Waiting`，届时按 misfire 策略处理。手动 `TriggerAsync` 遇到同样情况直接返回 `SCHEDULING_CONFLICT_001`。

`RunOnEveryInstance = true` 的作业是本地作业：每个实例按自己的时钟推算并运行，不经存储抢占，运行同样写入历史。本地作业的「不允许并发」只在本实例内判定，跳过的那次从现在推算下一次。本地触发器集合每 10 个轮询周期从存储刷新一次，本实例改动后立即刷新，所以别的节点运行时改了本地作业，本节点最多 10 个周期后看到。

`RequestsRecovery = true` 的作业，运行所在节点失联时（心跳超过 `NodeTimeout`，缺省 90 秒），领导者把那次运行记为 `Lost`，再以一次性触发器（`recovery-` 前缀，`JobTrigger.Recovery`）重新入队一次，数据与原运行相同。失联恢复只在共享存储（`Scheduling.Database`）下发生。

## 重试与执行时限

`JobDefinition.Retry` 与 `Timeout` 对应 4.x 任务的最大重试次数与超时（ADR-0060）。

重试策略 `JobRetryPolicy`：

| 成员 | 取值 | 说明 |
| --- | --- | --- |
| `MaxRetries` | 1 到 100（`JobRetryPolicy.MaxRetriesLimit`） | 失败后最多再运行几次，连同首次一组最多运行 `MaxRetries + 1` 次 |
| `Delay` | 大于零，不超过 30 天（`JobRetryPolicy.DelayLimit`） | 第一次重试前的等待 |
| `Backoff` | `Fixed`（缺省）/ `Exponential` | `Fixed` 每次等 `Delay`；`Exponential` 第 n 次重试等 `Delay × 2^(n-1)` |
| `MaxDelay` | 空，或大于零且不超过 30 天 | 指数退避的上限；空时只受 30 天约束 |

用 `JobRetryPolicy.Fixed(maxRetries, delay)` 或 `JobRetryPolicy.Exponential(maxRetries, delay, maxDelay)` 创建，越界值在构造时抛 `ArgumentOutOfRangeException`；`DelayFor(n)` 返回第 n 次重试前的等待。

重试的规则：

- 只有失败的运行重试：作业返回失败、抛出异常或超过执行时限。被取消（`CancelRunsAsync`、宿主停止）与失联（`Lost`）的运行不重试。
- 重试是一次新运行：`Trigger` 为 `Retry`，`Attempt` 加一，`RetryOf` 指向首次运行的 ID，`Data` 与失败的那次相同。运行历史里按 `RetryOf` 能把一组尝试串起来。
- 集群唯一的作业把重试写成存储里的一次性触发器（`retry-` 前缀），到期时刻是失败时刻加等待，任一节点抢占运行，进程重启不丢。不允许并发的作业照常独占。
- `RunOnEveryInstance` 的作业在失败的那个实例上进程内等待，不进存储；实例停止时未到期的重试直接丢弃。
- 手动 `TriggerAsync` 失败也按策略重试，调用方拿到的是第一次的结果。

执行时限 `Timeout` 到点时取消作业拿到的令牌。作业返回之前时限已过，运行记为失败，错误码 `SCHEDULING_TIMEOUT_001`，并按重试策略重试。.NET 不能安全地中止任务：作业不理会取消令牌时，运行要等它自己返回才结束，`MaxConcurrentRuns` 的名额也一直被占着。时限内先发生了取消请求或宿主停止，运行仍记为取消。

## 运行时管理：IJobScheduler

`IJobScheduler`（实现是 `JobSchedulerEngine`）是单例，所有操作都是批量的：以集合为入参，逐项返回结果，单条写法是 `JobSchedulerExtensions` 里的扩展方法。

| 方法 | 作用 | 主要错误 |
| --- | --- | --- |
| `ScheduleAsync(definitions)` | 新增或整体替换作业与触发器，立即生效；集群里的其他节点下一个周期看到 | `SCHEDULING_NOTFOUND_002`、`SCHEDULING_VALIDATION_001` |
| `UnscheduleAsync(names)` | 删除作业与触发器；进行中的运行照常结束，历史保留 | `SCHEDULING_NOTFOUND_001` |
| `PauseAsync` / `ResumeAsync(names)` | 暂停或恢复作业的全部触发器；暂停期间手动触发仍可用，恢复后错过的按 misfire 策略处理 | `SCHEDULING_NOTFOUND_001` |
| `PauseTriggersAsync` / `ResumeTriggersAsync(keys)` | 按 `TriggerKey(job, trigger)` 暂停或恢复单个触发器 | `SCHEDULING_NOTFOUND_003` |
| `TriggerAsync(requests)` | 在本实例立即运行并等待结果，按顺序逐项；不受暂停和领导权限制 | `SCHEDULING_NOTFOUND_001`、`SCHEDULING_CONFLICT_001` |
| `EnqueueAsync(requests)` | 插入一个立即到期的一次性触发器（`manual-` 前缀），由集群任一节点运行，不等待；返回触发器名 | `SCHEDULING_NOTFOUND_001` |
| `CancelRunsAsync(runIds)` | 本实例的运行立即取消令牌，其他节点的在下一个轮询周期取消 | `SCHEDULING_NOTFOUND_004` |
| `StatusAsync(name)` / `StatusesAsync()` | 作业状态：记录、是否暂停、各触发器的状态与上一次 / 下一次时刻与次数、运行中数量、最近一次完成的结果 | — |
| `RunsAsync(query)` / `CountRunsAsync(query)` | 运行历史与同一组过滤条件下的总数 | — |
| `RestoreAsync(names)` | 把运行时改过的代码作业恢复为代码定义 | `SCHEDULING_NOTFOUND_005` |

`JobTriggerRequest(Name, Data)` 能从 `JobName` 隐式转换，手动触发带的数据覆盖作业数据的同名键。

运行时为每个租户建一个同步作业：

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

运行时作业引用的类型要事先用 `builder.Services.AddJobType<TenantSyncJob>()` 登记，否则 `ScheduleAsync` 返回 `SCHEDULING_NOTFOUND_002`。多节点部署时每个节点都要登记同一组类型：节点抢到一个类型未登记的触发器时，把触发器改为 `Error` 并记录原因，不再运行它。

查询状态与历史：

```csharp
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

public sealed class ReportJobMonitor(IJobScheduler scheduler)
{
    public async ValueTask<(DateTimeOffset? NextRun, long Failures, IReadOnlyList<JobRunRecord> Recent)> SummaryAsync(CancellationToken cancellationToken)
    {
        var name = new JobName("reports.nightly");
        var status = (await scheduler.StatusAsync(name, cancellationToken)).ValueOrThrow();
        var failed = new JobRunQuery { Job = name, States = [JobRunState.Failed] };
        var failures = (await scheduler.CountRunsAsync(failed, cancellationToken)).ValueOrThrow();
        var recent = (await scheduler.RunsAsync(failed with { Limit = 20 }, cancellationToken)).ValueOrThrow();
        return (status.HasValue ? status.ValueOrThrow().NextRun : null, failures, recent);
    }
}
```

`JobRunQuery` 按作业、状态集合、计划时刻范围（`From` 含、`To` 不含）、触发器名、执行节点、有无错误码过滤，`Skip` 缺省 0，`Limit` 缺省 100、最大 1000（`JobRunQuery.MaxLimit`）；结果按计划时刻降序。`CountRunsAsync` 用同一组过滤项，不看分页（ADR-0053）。

### 代码作业的运行时修改

管理界面常要改代码作业的计划。`ScheduleAsync` 替换一个代码作业时，来源记为 `Overridden`，并保留代码定义的指纹 `CodeFingerprint`。指纹是类型键、数据、标志、说明、各触发器（名字、计划文本、数据、优先级、misfire 策略、说明）的 SHA-256；重试策略与执行时限设置了才计入。下次启动同步代码作业时：

1. 存储里是 `Overridden` 且指纹与当前代码定义相同：代码没改，保留运行时的修改。
2. 其余情况：按代码写入，来源回到 `Code`；覆盖了运行时修改的记一条警告日志。
3. 暂停状态一律保留。

`RestoreAsync` 随时把作业恢复为代码定义，保留暂停状态与同名触发器的进度；作业没有代码定义（来源 `Runtime`）时返回 `SCHEDULING_NOTFOUND_005`。删除代码作业只到下次启动为止，启动时会按代码重新写入。`JobTypeRegistry.Keys` 列出已登记的类型键，供界面新建作业时选择。

## 集群：引擎与存储

`JobSchedulerEngine` 由 Provider 特性装配，在应用进入服务状态后开始轮询，每个 `PollInterval`（缺省 1 秒）执行一个周期：

1. 应用不在服务状态时什么都不做。
2. 首个周期把代码登记的作业写入存储；失败时记日志，下个周期重试。
3. 存储共享时写节点心跳。
4. 续约领导租约，没有时尝试获取（资源 `LeaseResource`，缺省 `scheduling.jobs`；存活 `LeaseTimeToLive`，缺省 30 秒）。
5. 领导者每 `HousekeepingInterval`（缺省 60 秒）维护一次：恢复失联节点，删除结束早于 `HistoryRetention`（缺省 7 天，0 为不删）的历史。
6. 取消本节点上被请求取消的运行。
7. 运行到期的本地触发器与本实例的本地重试。
8. 抢占到期触发器并在后台运行：名额 = `min(AcquireBatchSize, MaxConcurrentRuns − 本节点运行中数量)`，缺省为 16 与 32。

`IJobStore.IsShared` 决定集群唯一的作业怎样保证只跑一次。共享存储（数据库实现）靠抢占：`AcquireDueAsync` 原子地把到期触发器改成 `Acquired(nodeId)`，多个节点同时调用时每条只被一个节点拿到；`TryBeginRunAsync(exclusive: true)` 原子地占用作业的 `ActiveRunId`，实现全集群串行。不共享的存储（内存实现）靠领导权：只有持有租约的实例按计划运行集群唯一的作业，没有领导权的实例把抢到的按计划触发器放回等待；入队、重试这类一次性触发器只有本进程看得到，不受领导权限制。

触发器的状态：

| 状态 | 含义 |
| --- | --- |
| `Waiting` | 等待到期 |
| `Paused` | 已暂停 |
| `Acquired` | 已被某节点抢占；间隔计划在运行结束前保持这个状态 |
| `Blocked` | 到期但作业不允许并发、上一次未结束 |
| `Complete` | 没有下一次（一次性已运行、次数用尽、超过 `EndAt`） |
| `Error` | 无法运行（作业类型未登记），原因在 `TriggerRecord.Error` |

停止时引擎先停轮询，再等待进行中的运行结束；宿主的停止令牌触发时取消这些运行，再给它们收尾的时间，最后释放领导租约。

## 领导选举

`ILeaderElection.TryAcquireAsync(resource, timeToLive)` 在资源空闲或租约已过期时授予 `LeaderLease`，被他人持有时返回 `SCHEDULING_CONFLICT_002`。`LeaderLease` 有 `Holder`、`ExpiresAt`、`IsHeld`，`RenewAsync` 延长一个存活时间（已过期或被接管时返回 `SCHEDULING_CONFLICT_002`），`ReleaseAsync` 立即放弃，可以重复调用。租约不可重入。

| 实现 | 持有者 | 存储 | 不可达时 |
| --- | --- | --- | --- |
| `InMemoryLeaderElection` | 每次获取新生成的随机值 | 进程内字典，只在本进程内互斥 | — |
| `DatabaseLeaderElection` | 节点 ID（`XiHan:Scheduling:NodeId`），每次获取另带随机令牌 | 与作业同库的一行，并发戳接管过期租约 | `SCHEDULING_UNAVAILABLE_002`，持有状态不变 |
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
            // 别的实例正在做，或选举存储不可达
            return false;
        }

        await using (lease)
        {
            // 长于存活时间的工作要定期 RenewAsync，并在续约失败时停下
            return true;
        }
    }
}
```

在 Kubernetes 上，示例应用 `next/samples/KubernetesApp` 在 Pod 内装配 `KubernetesSchedulingFeature`，在本地退回 Full 组合自带的进程内选举，并用 `/leader` 端点展示 `JobSchedulerEngine.IsLeader`：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Full;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Kubernetes;

var builder = WebApplication.CreateBuilder(args);
var inCluster = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") is not null;
builder.AddXiHan(xihan =>
{
    xihan.UseFullProfile();
    if (inCluster)
    {
        xihan.UseFeature<KubernetesSchedulingFeature>();
    }
});

var app = builder.Build();
app.UseXiHan();
app.MapGet("/leader", (JobSchedulerEngine scheduler, ILeaderElection election) =>
    new LeaderView(scheduler.NodeId, election.GetType().Name, scheduler.IsLeader));
app.Run();

public sealed record LeaderView(string NodeId, string Election, bool IsLeader);
```

Lease 名缺省是 `xihan-scheduler`，用 `XiHan:Scheduling:Kubernetes:Lease` 修改；Pod 的 ServiceAccount 需要对该 Lease 有 `get`、`update`、`patch` 权限，外加 `leases` 的 `create`。`xihan pack` 按应用清单里的 `leader.lease` 生成这份 Role 与环境变量，细节见 [Scheduling.Kubernetes 包](../packages/scheduling-kubernetes)与[部署与打包](./deployment)。

## 事件

装配了消息能力时，引擎经 `IMessagePublisher` 发布 CloudEvents 信封：`Source` 为 `/scheduling`，`Subject` 为运行 ID（作业级事件为作业名），`Data` 是 `SchedulingEventData` 的 JSON（作业、触发器名、运行 ID、触发来源、状态、各时刻、错误码与消息、`Attempt`、`RetryOf`、节点）。没有发布者时跳过，发布失败只记日志，不影响运行。

| 主题（`SchedulingTopics`） | 时机 |
| --- | --- |
| `scheduling.job.scheduled` / `scheduling.job.unscheduled` | 作业登记或替换 / 删除 |
| `scheduling.job.paused` / `scheduling.job.resumed` | 作业暂停 / 恢复 |
| `scheduling.run.started` | 运行开始 |
| `scheduling.run.succeeded` / `scheduling.run.failed` / `scheduling.run.canceled` | 运行结束 |
| `scheduling.run.lost` | 运行所在节点失联 |

订阅方式见[消息与事件](./messaging)。

## 配置

引擎选项在 `XiHan:Scheduling`（`SchedulerOptions`），内存与数据库两个 Provider 读同一节。值不合法（非数字、超出范围）时启动抛 `ArgumentException`。

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

```json
{
  "XiHan": {
    "Scheduling": {
      "PollIntervalSeconds": 1,
      "MaxConcurrentRuns": 32,
      "MisfireThresholdSeconds": 60,
      "LeaseTimeToLiveSeconds": 30,
      "HistoryRetentionDays": 7
    }
  }
}
```

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
| `SCHEDULING_CONFLICT_001` | 作业正在运行且不允许并发 |
| `SCHEDULING_CONFLICT_002` | 领导租约被其他持有者占用 |
| `SCHEDULING_CONFLICT_003` | 运行被取消 |
| `SCHEDULING_UNAVAILABLE_001` | 调度器未运行（已登记，当前实现不产生） |
| `SCHEDULING_UNAVAILABLE_002` | 领导选举的存储（数据库、API Server）不可达或超时 |
| `SCHEDULING_UNAVAILABLE_003` | 作业存储不可达 |
| `SCHEDULING_UNAVAILABLE_004` | 作业存储容量已满（内存实现的运行历史全部在运行中） |
| `SCHEDULING_TIMEOUT_001` | 运行超过作业的执行时限 |
| `SCHEDULING_UNEXPECTED_001` | 作业抛出未处理异常 |

错误的消息模板在消息目录 `scheduling` 里，十种区域都有译文，见[国际化](./localization)。

## 与其他能力的关系

- 工作流的 `Delay` 活动与 `Start` 节点的定时启动复用本包的 `CronExpression`，文法相同，见[工作流](./workflow)。
- 数据库 Provider 经 `persistence.relational@1` 的仓储读写，每个存储操作是一次独立的管道调用与事务，不加入调用方的工作单元，见[持久化](./persistence)。
- 作业运行经过完整管道，多租户、授权、审计的行为与[执行管道](./pipeline)一致。
- 4.x 的 `IJobWorker` 换成 `IJob`，`LeaderOnly` 取反后是 `RunOnEveryInstance`，4.x 任务的最大重试次数与超时对应 `Retry` 与 `Timeout`；完整对照见[从 4.x 迁移](./upgrade)。

## 相关

- [Scheduling.Abstractions](../packages/scheduling-abstractions)
- [Scheduling.InMemory](../packages/scheduling-inmemory)
- [Scheduling.Database](../packages/scheduling-database)
- [Scheduling.Kubernetes](../packages/scheduling-kubernetes)
- [测试与一致性套件](./testing)
- 规范：RFC-0013《调度与领导选举》；ADR-0020、ADR-0053、ADR-0060
