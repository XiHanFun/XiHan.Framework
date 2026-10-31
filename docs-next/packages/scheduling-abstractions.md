# XiHan.Framework.Scheduling.Abstractions

调度子系统的契约与引擎，包括作业与触发器、计划（间隔、cron、一次、手动）、排除日历、misfire、经内核管道的派发、作业存储契约 `IJobStore`、集群分发引擎 `JobSchedulerEngine` 与租约式领导选举 `ILeaderElection`。能力 `scheduling.jobs@1` 与 `scheduling.leader@1` 由 Provider 提供，应用和 Provider 都引用本包。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Scheduling.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `scheduling.jobs@1`（`SchedulingCapabilities.Jobs`）与 `scheduling.leader@1`（`SchedulingCapabilities.Leader`）的契约，本包不提供能力 |
| 特性入口 | — |
| 配置节 | `XiHan:Scheduling`（`SchedulerOptions`，由 Provider 读取） |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.Messaging.Abstractions`；`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions`、`Microsoft.Extensions.Logging.Abstractions` 10.0.13 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

引擎和全部协议性规则都在本包里（ADR-0020），包括计划推算、misfire、并发阻塞、暂停与恢复、重试与执行时限、失联恢复、运行历史与事件。Provider 只实现两个窄契约：作业存储 `IJobStore`（作业、触发器、运行、节点的读写，以及原子抢占、独占开始、失联恢复）与领导选举 `ILeaderElection`，再调用 `AddJobSchedulerEngine(options)` 装配引擎、在特性的生命周期里启停它。所以内存实现与数据库实现的语义只有一份，由 `Scheduling.Conformance` 的三个套件约束。

每次运行由 `JobDispatcher` 在新作用域、新调用上下文里经应用的完整管道执行：关联 ID 是运行 ID，调用来源是 `InvocationProtocols.Job`，目标是作业名。作业返回失败或抛出异常都记为失败，异常翻译为 `SCHEDULING_UNEXPECTED_001`。

本包还被 `Workflow.Abstractions` 引用，用它的 `CronExpression` 实现延迟与定时启动。

## 何时使用

- 编写作业、登记作业与日历、在运行时管理作业：引用本包，再装配一个提供 `scheduling.jobs` 的 Provider。
- 实现新的作业存储或领导选举 Provider：实现 `IJobStore` / `ILeaderElection` 并继承 `Scheduling.Conformance` 的套件。
- 只想运行作业：本包没有可装配的特性，单独引用不会运行任何东西，需要 [Scheduling.InMemory](./scheduling-inmemory) 或 [Scheduling.Database](./scheduling-database)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Scheduling.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。各 Provider 包与 `Profiles.Full` 都传递引用本包。登记作业：

```csharp
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

var builder = WebApplication.CreateBuilder(args);
// 代码作业：同时登记类型
builder.Services.AddJob<CleanupJob>("cleanup.nightly", JobSchedule.FromCron("0 3 * * *", "Asia/Shanghai"));
// 只登记类型：供运行时 ScheduleAsync 新建的作业引用
builder.Services.AddJobType<ArchiveJob>();

public sealed class CleanupJob : IJob
{
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success());
}

public sealed class ArchiveJob : IJob
{
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success());
}
```

## 配置

`SchedulerOptions.FromConfiguration` 读 `XiHan:Scheduling`，值不合法时抛 `ArgumentException`：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Scheduling:PollIntervalSeconds` | 正数 | 1 | 轮询周期 |
| `XiHan:Scheduling:NodeId` | 字符串 | `机器名:进程 ID:8 位随机` | 节点身份，进程内固定 |
| `XiHan:Scheduling:MaxConcurrentRuns` | ≥ 1 | 32 | 本节点同时运行的上限 |
| `XiHan:Scheduling:AcquireBatchSize` | ≥ 1 | 16 | 一个周期最多抢占的触发器数 |
| `XiHan:Scheduling:MisfireThresholdSeconds` | ≥ 0 | 60 | misfire 阈值 |
| `XiHan:Scheduling:LeaseResource` | 作业名文法 | `scheduling.jobs` | 领导租约资源名 |
| `XiHan:Scheduling:LeaseTimeToLiveSeconds` | 正数 | 30 | 领导租约存活时间 |
| `XiHan:Scheduling:NodeTimeoutSeconds` | 正数 | 90 | 心跳超过它即失联 |
| `XiHan:Scheduling:HousekeepingIntervalSeconds` | 正数 | 60 | 领导者维护（失联恢复、历史清理）周期 |
| `XiHan:Scheduling:HistoryRetentionDays` | ≥ 0 | 7 | 运行历史保留天数，0 为不删 |

```json
{
  "XiHan": {
    "Scheduling": {
      "PollIntervalSeconds": 1,
      "NodeId": "node-1",
      "MaxConcurrentRuns": 32,
      "AcquireBatchSize": 16,
      "MisfireThresholdSeconds": 60,
      "HistoryRetentionDays": 7
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `IJob` | 作业：`RunAsync(JobRun, InvocationContext, CancellationToken)` → `ValueTask<Result>`，作用域服务 |
| `JobName`、`TriggerKey` | 作业名（`^[a-z0-9][a-z0-9._-]{0,127}$`，可从字符串隐式转换）；触发器键 `(Job, Trigger)` |
| `JobDefinition` | 作业定义：`Name`、`JobType`、`Triggers`、`Data`、`AllowConcurrent`、`RunOnEveryInstance`、`RequestsRecovery`、`Retry`、`Timeout`、`Description` |
| `JobTriggerDefinition` | 触发器：`Name`、`Schedule`、`Data`、`Priority`（缺省 5）、`MisfirePolicy`、`Description` |
| `JobRetryPolicy`、`JobRetryBackoff` | 重试：`MaxRetries` 1–100、`Delay`、`Fixed` / `Exponential`、`MaxDelay`，等待最长 30 天；`DelayFor(n)` |
| `JobSchedule`、`JobScheduleKind` | 计划：`Manual`、`Every`、`FromCron`、`Once`，加 `StartAt` / `EndAt` / `Calendar`；`ToString` / `Parse` 文本往返 |
| `CronExpression` | 秒精度 cron：5 / 6 / 7 段、`?`、`L`、`W`、`#`、名字、时区与夏令时；星期 0/7 = 周日 |
| `MisfirePolicy` | `RunOnce`（缺省）、`Skip`、`RunAll` |
| `IJobCalendar`、`HolidayCalendar`、`WeeklyCalendar`、`DailyCalendar`、`CronCalendar`、`CompositeCalendar`、`JobCalendars`、`JobPlanner` | 排除日历与按日历推算下一次 |
| `JobRun`、`JobTrigger` | 一次运行（`Id`、`Name`、`TriggerName`、`ScheduledAt`、`Trigger`、`Attempt`、`RetryOf`、`Data`）；触发来源 `Schedule` / `Manual` / `Recovery` / `Retry` |
| `JobDispatcher` | 经管道执行一次运行；`GetRun(context)` 在管道内取当前运行 |
| `IJobScheduler`、`JobSchedulerExtensions` | 运行时管理：登记、删除、暂停、恢复、触发、入队、取消、状态、历史、计数、恢复为代码定义；全部批量，单条是扩展方法 |
| `JobTriggerRequest`、`JobTriggerResult` | 手动触发的请求（名字与数据）与逐项结果 |
| `JobStatus`、`JobTriggerStatus`、`JobOutcome` | 作业状态（含 `NextRun`、`Paused`、`IsRunning`、`Last`）、触发器状态、一次运行的结果 |
| `JobRunRecord`、`JobRunQuery`、`JobRunState` | 运行历史记录、查询（`Limit` 缺省 100、最大 1000）、状态 `Running` / `Succeeded` / `Failed` / `Canceled` / `Lost` |
| `JobSource` | 作业来源：`Code`、`Runtime`、`Overridden` |
| `JobRegistry`、`JobTypeRegistry` | 代码作业登记表；作业类型登记表（`Keys` 列出可选类型） |
| `JobSchedulerEngine`、`SchedulerOptions` | 引擎（同时是 `IJobScheduler`）与选项；`NodeId`、`IsLeader`、`IsRunning`、`RunningCount`、`PollOnceAsync` |
| `IJobStore`、`JobRecord`、`TriggerRecord`、`TriggerState`、`SchedulerNodeRecord` | Provider 契约与存储形态 |
| `ILeaderElection`、`LeaderLease` | 租约式领导选举 |
| `SchedulingTopics`、`SchedulingEventData` | `scheduling.job.*` / `scheduling.run.*` 事件 |
| `SchedulingErrors`、`SchedulingMessages`、`SchedulingCapabilities` | 错误码、消息键（目录 `scheduling`）、能力 ID |

`SchedulingServiceCollectionExtensions` 提供 `AddJob<TJob>`（两个重载）、`AddJobType<TJob>`（两个重载）、`AddJobCalendar`、`GetJobRegistry`、`GetJobTypeRegistry`、`GetJobCalendars`，以及给 Provider 用的 `AddJobSchedulerEngine(options)`。

## 使用示例

带两个触发器、数据与重试的作业：

```csharp
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Calendars;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddJobCalendar("weekends", new WeeklyCalendar());
builder.Services.AddJob<ExportJob>(new JobDefinition("exports.daily", typeof(ExportJob))
{
    Triggers =
    [
        new JobTriggerDefinition("morning", JobSchedule.FromCron("0 30 8 * * *", "Asia/Shanghai") with { Calendar = "weekends" }),
        new JobTriggerDefinition("hourly", JobSchedule.Every(TimeSpan.FromHours(1))) { Data = new JsonObject { ["mode"] = "delta" }, Priority = 2 },
    ],
    Data = new JsonObject { ["mode"] = "full" },
    Retry = JobRetryPolicy.Fixed(5, TimeSpan.FromMinutes(2)),
    Timeout = TimeSpan.FromMinutes(15),
});

public interface IExporter
{
    ValueTask<Result> ExportAsync(string mode, CancellationToken cancellationToken);
}

public sealed class ExportJob(IExporter exporter) : IJob
{
    // hourly 触发器的数据覆盖作业数据的同名键 mode
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
        => exporter.ExportAsync(run.Data["mode"]?.GetValue<string>() ?? "full", cancellationToken);
}
```

`IExporter` 的实现由应用注册。

手动触发并读取结果：

```csharp
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

public sealed class ExportRunner(IJobScheduler scheduler)
{
    public async ValueTask<string?> RunNowAsync(CancellationToken cancellationToken)
    {
        // 在本实例运行并等待；作业正在运行且不允许并发时返回 SCHEDULING_CONFLICT_001
        Result outcome = await scheduler.TriggerAsync(new JobName("exports.daily"), cancellationToken);
        return outcome.Error?.Code;
    }
}
```

## 行为与限制

- 名字、计划、策略的取值在构造时校验，越界抛 `ArgumentException` / `ArgumentOutOfRangeException`：作业名最长 128；`Every` 的间隔为正、`repeatCount` 至少 1；`MaxRetries` 1–100；`Delay`、`MaxDelay`、`Timeout` 大于零且不超过 30 天；`JobRunQuery.Limit` 1–1000。
- cron 向前最多搜索 5 年；排除日历连续排除 10000 个候选时刻即视为没有下一次。
- 本包不做持久化：运行时登记的作业、历史、节点放在哪里由 `IJobStore` 决定。`IJobStore.IsShared` 为假时，集群唯一的作业只在持有领导租约的实例上按计划运行。
- 引擎在应用进入服务状态之前不按计划运行；停止时等待进行中的运行，宿主停止令牌触发后取消它们并再等它们收尾，最后释放租约。
- 执行时限靠取消令牌实现，不响应取消的作业要等它返回才结束。
- 事件只在装配了消息能力时发布，发布失败只记日志。

| 错误码 | 含义 |
| --- | --- |
| `SCHEDULING_NOTFOUND_001` | 作业未登记 |
| `SCHEDULING_NOTFOUND_002` | 作业类型未登记 |
| `SCHEDULING_NOTFOUND_003` | 触发器不存在 |
| `SCHEDULING_NOTFOUND_004` | 运行不存在或已结束 |
| `SCHEDULING_NOTFOUND_005` | 作业没有代码定义，不能恢复 |
| `SCHEDULING_VALIDATION_001` | 作业定义不合法（触发器名重复、日历未登记） |
| `SCHEDULING_CONFLICT_001` | 作业正在运行且不允许并发 |
| `SCHEDULING_CONFLICT_002` | 领导租约被占用 |
| `SCHEDULING_CONFLICT_003` | 运行被取消 |
| `SCHEDULING_UNAVAILABLE_001` | 调度器未运行（已登记，当前实现不产生） |
| `SCHEDULING_UNAVAILABLE_002` | 领导选举的存储不可达 |
| `SCHEDULING_UNAVAILABLE_003` | 作业存储不可达 |
| `SCHEDULING_UNAVAILABLE_004` | 作业存储容量已满 |
| `SCHEDULING_TIMEOUT_001` | 运行超过执行时限 |
| `SCHEDULING_UNEXPECTED_001` | 作业抛出未处理异常 |

## 相关

- [调度与后台作业](../guide/scheduling)
- [Scheduling.InMemory](./scheduling-inmemory)
- [Scheduling.Database](./scheduling-database)
- [Scheduling.Kubernetes](./scheduling-kubernetes)
- [Workflow.Abstractions](./workflow-abstractions)
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0013《调度与领导选举》；ADR-0020、ADR-0053、ADR-0060
