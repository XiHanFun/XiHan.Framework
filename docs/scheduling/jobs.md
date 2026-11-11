# 作业与计划

这一章讲怎样在应用里按计划运行后台工作。这一页讲作业、触发器与运行三个概念，怎样选 Provider、编写并登记作业，四种计划（间隔、cron、一次、手动）与起止窗口、排除日历，以及 misfire、并发与本地作业、失联恢复、重试与执行时限。读完你能给自己的特性加定时任务，并说清它在停机、超时、失败时的行为。前置知识是[执行管道](/startup/pipeline)与[依赖注入](/startup/dependency-injection)；下一页[集群与管理](/scheduling/cluster)讲运行时管理与多实例分发。

## 为什么需要

每个系统都有「到点要做」的事：夜里清理过期数据、每月出报表、每十分钟同步一次外部状态。用 `BackgroundService` 加 `Task.Delay` 写得很快，问题随后就来：进程重启后错过的那次要不要补；两个实例同时跑了两份怎么办；失败了谁重试、重试几次；运行记录在哪里能查。

框架的调度子系统把这些规则实现一份放在抽象包里：计划推算、misfire、并发阻塞、重试、失联恢复、运行历史。作业的每次运行与一次请求同构，经应用的完整管道执行，租户、授权、审计、追踪中间件对作业照常生效。Provider 只需实现作业存储与领导选举两个窄契约，内存实现与数据库实现因此行为一致。框架不引入 Quartz.NET，这些都是自实现的。

## 作业、触发器与运行

调度沿用 Quartz 的分法：作业（`IJob`、`JobDefinition`）描述「做什么」，是一段要运行的代码及其名字、数据、并发与重试规则；触发器（`JobTriggerDefinition`）描述「什么时候做」，一个作业可以挂多个触发器，每个有自己的计划、数据、优先级和 misfire 策略，各自推算下一次运行；作业没有触发器时只能手动触发。触发器每到期一次，或者被手动触发、失败重试、失联恢复一次，就产生一次运行（`JobRun`），写进历史（`JobRunRecord`）。

`JobDispatcher` 为每次运行创建新的 DI 作用域和新的调用上下文，经完整管道执行 `IJob.RunAsync`。上下文的关联 ID（`xihan.correlation`）就是运行 ID，来源是协议 `job`，目标是作业名。作业返回失败的 `Result` 记为失败；抛出的异常被翻译成 `SCHEDULING_UNEXPECTED_001`，同样记为失败。

## 选择 Provider

调度提供两个能力（RFC-0013《调度与领导选举》）：`scheduling.jobs@1`（`IJobScheduler`、`JobSchedulerEngine`、`IJobStore`）与 `scheduling.leader@1`（`ILeaderElection`）。默认实现都是 [Scheduling.InMemory](/packages/scheduling-inmemory)；[Scheduling.Database](/packages/scheduling-database) 同时提供两个能力的数据库实现，[Scheduling.Kubernetes](/packages/scheduling-kubernetes) 只替换领导选举（Lease 对象）。三种常见部署：

| 部署 | 装配 | 效果 |
| --- | --- | --- |
| 单实例 | `InMemorySchedulingFeature`（[Profiles.Full](/packages/profiles-full) 已包含） | 作业、触发器、历史都在进程内，重启后运行时登记的作业与历史丢失 |
| Kubernetes 多副本，作业只需在一个 Pod 上跑 | Full 组合加 `KubernetesSchedulingFeature` | 存储仍在各自进程内，只有持有 Lease 的 Pod 按计划运行集群唯一的作业 |
| 多实例，需要持久化与分发 | 持久化 Provider 加 `DatabaseSchedulingFeature` | 所有节点共用一个库，每条到期触发器由先抢到的节点运行，节点失联由领导者恢复 |

::: warning 内存存储不在实例之间共享
只装配 `Scheduling.InMemory` 时，每个实例都有自己的存储和进程内租约，都当自己是领导者，集群唯一的作业会在每个实例上各跑一份。多实例部署至少要换掉领导选举（`Scheduling.Kubernetes`），需要共享运行时作业、历史与失联恢复时换 `Scheduling.Database`。
:::

## 最小示例

我们写一个每天凌晨三点运行的清理作业。两步：实现 `IJob`；用 `AddJob<TJob>(name, schedule)` 登记并装配 `InMemorySchedulingFeature`。

```bash
dotnet add package XiHan.Framework.Scheduling.InMemory --prerelease
```

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.InMemory;

var builder = WebApplication.CreateBuilder(args);

// 最简写法：一个 cron 触发器，名为 default
builder.Services.AddJob<CleanupJob>(new JobName("cleanup.nightly"), JobSchedule.FromCron("0 3 * * *", "Asia/Shanghai"));

builder.AddXiHan(xihan => xihan.UseFeature<InMemorySchedulingFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();

public sealed class CleanupJob : IJob
{
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context)
        => ValueTask.FromResult(Result.Success());
}
```

应用进入服务状态后，引擎在首个周期把 `cleanup.nightly` 写入存储，上海时间每天 03:00 产生一次运行。

## 编写与登记作业

作业实现 `IJob`，是作用域服务，可以注入作用域内的其他服务。运行信息在 `JobRun` 里：`Id`、`Name`、`TriggerName`（手动运行为空）、`ScheduledAt`、`Trigger`（`Schedule`、`Manual`、`Recovery`、`Retry`）、`Attempt`（从 1 起）、`RetryOf`（重试时为首次运行的 ID）与 `Data`。

```csharp
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

// IReportService 是应用自己的服务，GenerateAsync 返回 ValueTask<Result>
public sealed class NightlyReportJob(IReportService reports) : IJob
{
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context)
    {
        // Data 已按 作业 ← 触发器 ← 手动触发 逐层合并
        var format = run.Data["format"]?.GetValue<string>() ?? "pdf";
        return reports.GenerateAsync(format, context.CancellationToken);
    }
}
```

`cancellationToken` 在三种情况下取消：有人请求取消这次运行（`CancelRunsAsync`）、超过作业的执行时限、宿主停止。作业要把它传给每一个下游调用，否则时限与取消都要等作业自己返回才生效。管道内的其他代码可以用 `JobDispatcher.FindRun(context)` 取到当前运行。

登记有四个扩展方法，都在 `IServiceCollection` 上，在 `Program.cs` 或特性的 `Configure` 里调用：

| 方法 | 作用 |
| --- | --- |
| `AddJob<TJob>(name, schedule)` | 登记作业类型和一个代码作业；计划不是 `Manual` 时生成一个名为 `default` 的触发器 |
| `AddJob<TJob>(JobDefinition)` | 登记作业类型和一个完整定义的代码作业（多个触发器、数据、并发、重试、时限） |
| `AddJobType<TJob>()` / `AddJobType<TJob>(key)` | 只登记作业类型（键缺省为类型全名），供运行时用 `ScheduleAsync` 新建的作业引用 |
| `AddJobCalendar(name, calendar)` | 按名登记排除日历，触发器的计划用 `Calendar` 引用 |

同名重复 `AddJob` 是编程错误，启动时抛出。代码登记的作业在引擎首个周期写入存储，来源记为 `Code`。完整定义的写法：

```csharp
using System.Text.Json.Nodes;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Calendars;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

var shanghai = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
builder.Services.AddJobCalendar("holidays", new HolidayCalendar([new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2)], shanghai));

// 两个触发器、作业数据、重试与执行时限
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
```

`JobDefinition` 的其余选项：

| 属性 | 缺省 | 说明 |
| --- | --- | --- |
| `Triggers` | 空 | 空为只能手动触发；同一作业内触发器名重复是 `SCHEDULING_VALIDATION_001` |
| `Data` | 空 | 传给作业的数据，被触发器与手动触发的数据逐层覆盖同名键 |
| `AllowConcurrent` | `false` | `false` 时整个集群同一作业同一时刻最多一次运行 |
| `RunOnEveryInstance` | `false` | `true` 时每个实例按自己的时钟各自运行，不经存储抢占 |
| `RequestsRecovery` | `false` | 运行所在节点失联时由领导者重新入队一次（`JobTrigger.Recovery`） |
| `Retry` | 空 | 失败运行的重试策略，见下文 |
| `Timeout` | 空 | 单次执行时限，大于零、不超过 30 天（`JobDefinition.TimeoutLimit`） |
| `Description` | 空 | 说明 |

作业名、触发器名与领导租约的资源名用同一个文法：`^[a-z0-9][a-z0-9._-]{0,127}$`。`JobName` 用构造或显式转换从字符串创建，不合法时抛 `ArgumentException`；名字来自外部输入时先用 `JobName.IsValid` 判断。

`JobTriggerDefinition` 另有 `Priority`（缺省 5，到期触发器多于本周期名额时大者先运行）、`MisfirePolicy`（缺省 `RunOnce`）、`Data` 与 `Description`；触发器的计划不能是 `Manual`。

## 计划

`JobSchedule` 有四种：

| 写法 | 含义 |
| --- | --- |
| `JobSchedule.Manual` | 只手动触发 |
| `JobSchedule.Every(interval, repeatCount)` | 固定延迟：上一次运行完成后再等一个间隔；`repeatCount` 是总运行次数，空为无限 |
| `JobSchedule.FromCron(expression, timeZoneId)` | cron 表达式；时区是 `TimeZoneInfo` 的 ID（IANA 或 Windows 均可），空为 UTC，找不到的时区抛 `ArgumentException` |
| `JobSchedule.Once(at)` | 在 `at` 运行一次 |

每种计划都可以用 `with` 加三个窗口条件：`StartAt`（之前不运行，间隔计划的首次落在 `StartAt + 间隔`）、`EndAt`（之后没有下一次，触发器转为 `Complete`）与 `Calendar`（排除日历名）。计划能和文本互相转换：`ToString()` 产生 `every 00:10:00 x5`、`cron 0 0 9 * * MON-FRI tz=Asia/Shanghai` 这样的文本，尾部可带 `start=`、`end=`、`calendar=`；`JobSchedule.Parse` / `TryParse` 读回。存储以这段文本持久化，管理界面可以直接让用户填它。

### cron 文法

`CronExpression` 精确到秒，采用 Quartz 风格的文法：5 段是 `分 时 日 月 周`（秒为 0），6 段是 `秒 分 时 日 月 周`，7 段再加 `年`（1970 到 2099）。每段支持 `*`、数值、列表 `a,b`、范围 `a-b`、步长 `*/n`、`a/n` 与 `a-b/n`；日与周可写 `?`（等同 `*`）；月可写 `JAN` 到 `DEC`，周可写 `SUN` 到 `SAT`，不区分大小写。日段另有 `L`（当月最后一天）、`L-n`、`nW`（离 n 日最近的工作日，不跨月）、`LW`；周段另有 `nL` / `FRIL`（当月最后一个周 n）与 `n#k` / `FRI#2`（当月第 k 个周 n，k 为 1 到 5）。日和周同时限定时任一匹配即可。

::: warning 不是 Quartz 文法的超集
`0` 与 `7` 都是周日，`1` 是周一。Quartz 的 `1` 是周日、`7` 是周六，从 Quartz 照搬带星期数字的表达式会错一天，而且不会报错；写成 `MON`、`FRI` 这类名字可以避开。另外周段单独的 `L` 与反向范围（`FRI-MON`、`22-2`）这里不支持，解析报错。
:::

带时区的 cron 按该时区的墙上时间匹配：夏令时跳过的时刻不运行，重复出现的时刻只运行第一次。向前最多搜索 5 年，找不到匹配即没有下一次。

### 排除日历

日历实现 `IJobCalendar.IsExcluded(at)`。推算下一次时跳过被排除的时刻，一次性计划的时刻被排除就不再运行；连续 10000 个候选时刻都被排除时视为没有下一次，触发器转为 `Complete`。

| 日历 | 排除 |
| --- | --- |
| `HolidayCalendar(dates, timeZone)` | 指定日期（整天） |
| `WeeklyCalendar(excludedDays, timeZone)` | 每周指定星期几；不给星期时排除周六、周日 |
| `DailyCalendar(from, to, timeZone)` | 每天一个时段（`from` 含、`to` 不含），`from` 晚于 `to` 表示跨午夜 |
| `CronCalendar(expression, timeZone)` | 与 cron 匹配的秒，`CronExpression.Parse("* * 0-7 * * *")` 排除每天 0 点到 8 点 |
| `CompositeCalendar(calendars)` | 任一成员排除即排除 |

时区参数都缺省为 UTC。触发器引用了未登记的日历名，登记时返回 `SCHEDULING_VALIDATION_001`。

## misfire

触发器的到期时刻落后当前时刻超过 `MisfireThreshold`（缺省 60 秒）时算 misfire，进程停机、作业暂停、节点失联、上一次运行没结束而被阻塞都会造成它。处理按触发器的 `MisfirePolicy`：

| 策略 | 行为 |
| --- | --- |
| `RunOnce`（缺省） | 立即运行一次，计划时刻记为现在，再从现在推算下一次 |
| `Skip` | 不补运行，从现在推算下一次 |
| `RunAll` | 按原计划时刻逐次补齐，每个轮询周期补一次，直到追平 |

## 并发、本地作业与失联恢复

`AllowConcurrent = false`（缺省）的作业在整个集群同一时刻最多一次运行。按计划的触发遇到上一次没结束时，触发器转为 `Blocked`，到期时刻不变；上一次结束时存储把它改回 `Waiting`，届时按 misfire 策略处理。间隔计划的触发器在运行标为完成之后才回到等待，不会被自己的上一次阻塞。手动 `TriggerAsync` 遇到同样情况直接返回 `SCHEDULING_CONFLICT_001`。

`RunOnEveryInstance = true` 的作业是本地作业，适合每个实例都要做的事（如刷新本地缓存）：每个实例按自己的时钟推算并运行，不经存储抢占，运行同样写入历史。本地作业的「不允许并发」只在本实例内判定，跳过的那次从现在推算下一次。本地触发器集合每 10 个轮询周期从存储刷新一次，本实例改动后立即刷新。

`RequestsRecovery = true` 的作业，运行所在节点失联时（心跳超过 `NodeTimeout`，缺省 90 秒），领导者把那次运行记为 `Lost`，再以一次性触发器（`recovery-` 前缀，`JobTrigger.Recovery`）重新入队一次。失联恢复只在共享存储（`Scheduling.Database`）下发生。

## 重试与执行时限

重试策略 `JobRetryPolicy`（`JobDefinition.Retry`）：

| 成员 | 取值 | 说明 |
| --- | --- | --- |
| `MaxRetries` | 1 到 100（`MaxRetriesLimit`） | 失败后最多再运行几次，连同首次最多运行 `MaxRetries + 1` 次 |
| `Delay` | 大于零，不超过 30 天（`DelayLimit`） | 第一次重试前的等待 |
| `Backoff` | `Fixed`（缺省）/ `Exponential` | `Fixed` 每次等 `Delay`；`Exponential` 第 n 次重试等 `Delay × 2^(n-1)` |
| `MaxDelay` | 空，或大于零且不超过 30 天 | 指数退避的上限 |

用 `JobRetryPolicy.Fixed(maxRetries, delay)` 或 `JobRetryPolicy.Exponential(maxRetries, delay, maxDelay)` 创建，越界值在构造时抛 `ArgumentOutOfRangeException`。

重试的规则：

- 只有失败的运行重试：作业返回失败、抛出异常或超过执行时限。被取消（`CancelRunsAsync`、宿主停止）与失联（`Lost`）的运行不重试。
- 重试是一次新运行：`Trigger` 为 `Retry`，`Attempt` 加一，`RetryOf` 指向首次运行的 ID，`Data` 与失败的那次相同。
- 集群唯一的作业把重试写成存储里的一次性触发器（`retry-` 前缀），任一节点抢占运行，进程重启不丢；`RunOnEveryInstance` 的作业在失败的那个实例上进程内等待，实例停止时未到期的重试直接丢弃。
- 手动 `TriggerAsync` 失败也按策略重试，调用方拿到的是第一次的结果。

执行时限 `Timeout` 到点时取消作业拿到的令牌。作业返回之前时限已过，运行记为失败，错误码 `SCHEDULING_TIMEOUT_001`，并按重试策略重试。.NET 不能安全地中止任务：作业不理会取消令牌时，运行要等它自己返回才结束，`MaxConcurrentRuns` 的名额也一直被占着。

## 常见问题

### 作业登记了却一直不运行

按顺序查：是否装配了 `InMemorySchedulingFeature` 或 `DatabaseSchedulingFeature`（只引用抽象包不运行作业）；应用是否已进入服务状态；cron 的时区是否写对（空为 UTC）；多实例且只装配内存实现时，看的是否不是领导者。用下一页的 `StatusAsync` 看触发器的 `NextFireAt` 与状态最直接。

### 用 `JobSchedule.Every` 的作业为什么节奏越来越慢

`Every` 是固定延迟：上一次运行完成后再等一个间隔，运行时长会累加进去。要固定节奏用 cron（每小时整点是 `0 0 * * * *`），下一次从计划时刻推算，不随运行时长漂移。

## 相关包与下一步

- 包参考：[Scheduling.Abstractions](/packages/scheduling-abstractions)、[Scheduling.InMemory](/packages/scheduling-inmemory)、[Scheduling.Database](/packages/scheduling-database)、[Scheduling.Kubernetes](/packages/scheduling-kubernetes)
- 作业运行经过的管道：[执行管道](/startup/pipeline)、[调用上下文](/startup/invocation-context)
- 下一步：[集群与管理](/scheduling/cluster)，运行时管理作业、查运行历史、多实例分发与领导选举
