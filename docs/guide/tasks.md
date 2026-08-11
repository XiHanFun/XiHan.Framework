# 定时任务与后台作业

框架里有三条彼此独立的「后台执行」能力线，选错了会很难受：定时调度、一次性后台作业、后台常驻服务。这章讲怎么选、怎么写、以及租户上下文和分布式锁这两个最容易翻车的地方。

完整 API 与配置全表见 [Tasks 包](../packages/tasks)。

## 三条能力线怎么选

| 你的需求 | 用哪条线 | 入口类型 |
| --- | --- | --- |
| 每天凌晨跑一次、每 5 分钟跑一次、启动后延迟一次 | 定时调度 ScheduledJobs | `IJobWorker` + `IJobScheduler` |
| 把一次性的耗时活儿甩出请求线程，要持久化、失败自动退避重试 | 后台作业 BackgroundJobs | `IBackgroundJobManager.EnqueueAsync` |
| 常驻循环，持续从自己的队列批量拉取并**并发**处理 | 后台常驻服务 BackgroundServices | `XiHanBackgroundServiceBase<T>` |

判断依据只有两条：

- **谁决定什么时候跑？** 时间表决定 → 定时调度；业务代码入队时决定 → 后台作业；自己写的循环决定 → 常驻服务。
- **跑一次还是反复跑？** 一次性 fire-and-forget → 后台作业；周期性 → 定时调度。

三者可以在同一个应用里并存，互不干扰。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Tasks
```

```csharp
[DependsOn(typeof(XiHanTasksModule))]
public class MyModule : XiHanModule;
```

模块启用后自动发生两件事：

| 阶段 | 做了什么 |
| --- | --- |
| `PreConfigureServices` | `AddXiHanBackgroundJobs(config)`：注册 `IBackgroundJobManager`、托管服务 `BackgroundJobWorker`、默认内存存储，并挂上作业处理器的自动发现钩子（必须早于业务模块的约定注册） |
| `ConfigureServices` | `AddXiHanTasks(config)`：注册 `IJobScheduler`、`IJobExecutor`、`IJobStore`、`IJobLockProvider`、五个内置中间件，以及托管服务 `JobHostedService` |

::: warning 常驻服务基类不在自动注册范围内
`XiHanBackgroundServiceBase<T>` 只是一个基类，`AddXiHanTasks` / `AddXiHanBackgroundJobs` 都不会替你注册它。你的服务需要自己 `services.AddHostedService<MyService>()`。
:::

## 定时调度

### 写一个任务

实现 `IJobWorker`，用特性描述调度方式：

```csharp
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Attributes;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

[JobName("DailyReport")]
[JobDescription("每日报表生成")]
[JobSchedule("0 2 * * *")]          // 每天 02:00
[JobTimeout(600000)]                // 10 分钟
[JobRetry(MaxRetryCount = 2)]
[JobConcurrent(false)]              // 不允许并发重入
public class DailyReportJob : IJobWorker
{
    private readonly IReportService _reports;

    public DailyReportJob(IReportService reports) => _reports = reports;

    public async Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
    {
        var count = await _reports.GenerateDailyAsync(cancellationToken);
        return JobResult.Success(count);
    }
}
```

特性一览：

| 特性 | 作用 | 不写时 |
| --- | --- | --- |
| `[JobName("x")]` | 任务唯一名 | 程序集扫描会**跳过**这个类型 |
| `[JobSchedule("0 2 * * *")]` | Cron 触发 | 见下方警告 |
| `[JobSchedule(60)]` | 固定间隔（秒） | — |
| `[JobSchedule]` | 手动触发（只能靠 `TriggerJobAsync` 拉起） | — |
| `[JobRetry]` | `MaxRetryCount`=3、`RetryIntervalMilliseconds`=1000、`UseExponentialBackoff`=true | 用 `JobRetryPolicy.Default`（同上默认值） |
| `[JobTimeout(ms)]` | 单次执行超时 | 300000（5 分钟） |
| `[JobConcurrent(false)]` | 禁止同名任务并发 | `AllowConcurrent = true` |
| `[JobPriority(JobPriority.High)]` | 优先级 | `Normal` |
| `[JobDescription("…")]` | 描述文本 | 无 |

::: danger 没有 [JobSchedule] 的任务永远不会跑
`JobInfo.TriggerType` 的枚举默认值是 `Cron`，而 `CronExpression` 为空 → 算不出下次触发时间 → 任务「注册成功但永不执行」。

这种情况调度器会打一条 Warning：`任务 {JobName}（Cron）无法计算下次触发时间，将不会被自动调度`。任务不跑先看这条日志。
:::

::: warning 一次性延迟触发没有对应特性
`JobScheduleAttribute` 的三个构造函数只产出 `Cron` / `Interval` / `Manual` 三种触发类型，它的 `DelaySeconds` 属性只会填上 `JobInfo.Delay`，不会把触发类型改成 `Delay` —— 结果是这个延迟被忽略。

要用一次性延迟触发，直接构造 `JobInfo` 并显式设置 `TriggerType = JobTriggerType.Delay` 和 `Delay`，交给 `RegisterJob` 注册。
:::

### 注册任务

**特性只是描述，注册才让它跑起来。** 三种注册方式：

```csharp
public override void OnApplicationInitialization(ApplicationInitializationContext context)
{
    var scheduler = context.ServiceProvider.GetRequiredService<IJobScheduler>();

    // ① 程序集扫描：只收带 [JobName] 的 IJobWorker 实现类
    scheduler.RegisterJobsFromAssembly(typeof(DailyReportJob).Assembly);

    // ② 代码快捷注册
    scheduler.RegisterCronJob<DailyReportJob>("DailyReport", "0 2 * * *", "每日报表");
    scheduler.RegisterIntervalJob<HeartbeatJob>("Heartbeat", TimeSpan.FromMinutes(1));

    // ③ 完整控制：截止时间、重复上限、固定租户、默认参数只能这样传
    scheduler.RegisterJob(new JobInfo
    {
        JobName = "TenantCleanup",
        JobType = typeof(TenantCleanupJob),
        TriggerType = JobTriggerType.Interval,
        Interval = TimeSpan.FromHours(6),
        TenantId = 1001,
        RepeatCount = 20,                                  // -1 表示不限
        EndTime = DateTimeOffset.UtcNow.AddDays(30),
        AllowConcurrent = false,
        DefaultParameters = new Dictionary<string, object?> { ["scope"] = "logs" }
    });
}
```

::: warning 没有「自动发现」
`RegisterJobsFromAssembly` 需要你自己调用，且没有配置开关能替代它。配置节 `XiHan:Tasks:ScheduledJobs` 里的 `AutoDiscoverJobs` / `JobAssemblyPatterns` 会被绑定，但调度器不读取它们（见 [配置](#配置)）。
:::

### 运行期控制

```csharp
scheduler.PauseJob("DailyReport");
scheduler.ResumeJob("DailyReport");
scheduler.UnregisterJob("DailyReport");

DateTimeOffset? next = scheduler.GetNextFireTime("DailyReport");
IReadOnlyList<JobInfo> all = scheduler.GetAllJobs();

// 手动触发一次，返回任务实例标识；参数里的 tenantId 会覆盖任务自身的租户
string instanceId = await scheduler.TriggerJobAsync("DailyReport",
    new Dictionary<string, object?> { ["tenantId"] = 1001 });
```

### Cron 表达式

自研 `CronHelper` 解析，不依赖第三方调度库：

| 支持项 | 说明 |
| --- | --- |
| 5 位 | `分 时 日 月 周` |
| 6 位 | `秒 分 时 日 月 周` |
| 预定义 | `@yearly` `@annually` `@monthly` `@weekly` `@daily` `@midnight` `@hourly` |
| 名称 | `JAN`–`DEC`、`SUN`–`SAT` |
| 特殊符号 | `*` `-` `,` `/` `?` |

::: warning 不支持 L / W / #
解析器只认上表这几个符号。表达式里写 `L`（月末）、`W`（最近工作日）、`#`（第 N 个星期几）会解析失败，`CronScheduler.GetNextFireTime` 吞掉异常返回 `null` —— 任务注册成功但算不出下次触发时间，表现为「永不执行」，日志里只有那条无法计算下次触发时间的 Warning。
:::

::: warning Cron 按服务器本地时区求值
`"0 2 * * *"` 表示**服务器本地时间** 02:00，不是 UTC 02:00。容器里没设 `TZ` 会按 UTC 跑，和预期差一个时区偏移。
:::

调度器用一个 `Timer` **每秒**巡检一次到点任务，触发精度为秒级。`Delay` 触发是一次性的：触发过一次后不再续排。

## 一次性后台作业

适合「用户点了提交，邮件慢慢发」这类场景：入队立刻返回，执行完全交给后台 Worker。

### 定义参数与处理器

```csharp
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Attributes;

// 作业的稳定标识来自参数类型，建议显式标注
[BackgroundJobName("SendWelcomeEmail")]
public class SendWelcomeEmailArgs
{
    public long UserId { get; set; }

    public string Email { get; set; } = default!;
}

// 继承 AsyncBackgroundJob<TArgs>：自动瞬时注册 + 自动纳入作业发现
public class SendWelcomeEmailJob : AsyncBackgroundJob<SendWelcomeEmailArgs>
{
    private readonly IEmailSender _sender;

    public SendWelcomeEmailJob(IEmailSender sender) => _sender = sender;

    public override async Task ExecuteAsync(SendWelcomeEmailArgs args)
    {
        await _sender.SendAsync(args.Email, "欢迎注册", "…");
    }
}
```

### 入队

```csharp
public class RegistrationAppService
{
    private readonly IBackgroundJobManager _jobs;

    public RegistrationAppService(IBackgroundJobManager jobs) => _jobs = jobs;

    public async Task RegisterAsync(long userId, string email)
    {
        // …写库等业务逻辑…

        // 入队即返回作业标识，不等待执行
        await _jobs.EnqueueAsync(new SendWelcomeEmailArgs { UserId = userId, Email = email });

        // 也可以指定优先级和延迟
        await _jobs.EnqueueAsync(
            new SendWelcomeEmailArgs { UserId = userId, Email = email },
            priority: BackgroundJobPriority.High,
            delay: TimeSpan.FromMinutes(5));
    }
}
```

优先级取值：`Low`(5) / `BelowNormal`(10) / `Normal`(15，默认) / `AboveNormal`(20) / `High`(25)，领取时按值降序。

### 执行与失败语义

Worker 启动后先等 `FirstWaitDurationMilliseconds`（默认 5000），之后按 `JobPollPeriodMilliseconds`（默认 5000）轮询：抢单活锁 → 批量领取 → 逐个执行。

失败处理分两类，**结果完全不同**：

| 失败类型 | 触发条件 | 处理 |
| --- | --- | --- |
| 业务失败 | 处理器 `ExecuteAsync` 里抛出的任何异常（执行器统一包成 `BackgroundJobExecutionException`） | 按指数退避排下次重试 |
| 致命错误 | 作业名找不到对应配置、参数 JSON 反序列化失败 | **立即标记 `IsAbandoned` 放弃**，不重试 |

退避公式与放弃阈值：

```text
下次等待秒数 = DefaultFirstWaitDurationSeconds × DefaultWaitFactor^(TryCount - 1)
              = 60 × 2^(TryCount - 1)

若 下次执行时间 - 作业创建时间 > DefaultTimeoutSeconds（默认 172800 秒 = 2 天）→ 放弃
```

::: tip 没有「最大重试次数」
只有**累计耗时上限**。退避间隔指数增长，所以一个持续失败的作业大约 12 次尝试后就会撞上 2 天阈值被放弃，而不是跑满某个固定次数。
:::

::: danger 改参数类型的名字会丢作业
不标 `[BackgroundJobName]` 时，作业名回退成参数类型的完整名称。改类名或换命名空间后，已入库未执行的旧作业会「找不到作业配置」→ 按致命错误直接放弃。关键作业一律显式标注固定名称。
:::

## 后台常驻服务

继承 `XiHanBackgroundServiceBase<T>`，只实现「取任务」和「处理任务」：

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Tasks.BackgroundServices;

public class OutboxConsumer : XiHanBackgroundServiceBase<OutboxConsumer>
{
    private readonly IOutboxQueue _queue;

    public OutboxConsumer(
        ILogger<OutboxConsumer> logger,
        IOptions<XiHanBackgroundServiceOptions> options,
        IOutboxQueue queue)
        : base(logger, options)
    {
        _queue = queue;
    }

    // 批量取任务；返回空集合表示当前无活儿，基类按 IdleDelayMilliseconds 空转
    protected override async Task<IEnumerable<IBackgroundTaskItem>> FetchWorkItemsAsync(
        int maxCount, CancellationToken cancellationToken)
        => await _queue.DequeueAsync(maxCount, cancellationToken);

    protected override async Task ProcessItemAsync(
        IBackgroundTaskItem item, CancellationToken cancellationToken)
        => await _queue.HandleAsync(item, cancellationToken);

    // 可选：失败回调
    protected override void OnTaskFailed(IBackgroundTaskItem item, Exception exception)
        => Logger.LogError(exception, "发件箱条目 {TaskId} 处理失败", item.TaskId);
}
```

```csharp
services.Configure<XiHanBackgroundServiceOptions>(o => o.MaxConcurrentTasks = 8);
services.AddHostedService<OutboxConsumer>();
```

基类替你做的事：并发上限门控、批量拉取、失败重试（默认开，指数退避 ×2、上限 5 分钟）、优雅停机（等在跑的任务收尾，超过 `ShutdownTimeoutMilliseconds` 强停）、运行统计（`GetStatistics()` / `GetServiceStatus()`）、运行时热调（`IDynamicServiceConfig` 改并发数、空闲延迟、启停开关）。

基类**不**替你做的事：

| 缺口 | 你要做什么 |
| --- | --- |
| 单活 / 多实例去重 | 在 `FetchWorkItemsAsync` 里做原子领取，或自己加分布式锁 |
| 租户上下文 | 在 `ProcessItemAsync` 里自己 `ICurrentTenant.Change(...)` |
| 单任务超时 | `EnableTaskTimeout` 默认 `false`、`TaskTimeoutMilliseconds` 默认 `0`，需要就显式打开 |

::: tip 多个后台服务共享同一份选项
构造函数收的是 `IOptions<XiHanBackgroundServiceOptions>`，所有派生服务默认拿到同一个实例。要让某个服务用不同的并发数，在它自己的构造函数里传 `Options.Create(new XiHanBackgroundServiceOptions { … })`。
:::

## 关键机制

### 执行管道

定时任务不是被直接调用的，而是穿过一条中间件管道：

```text
LoggingMiddleware → TimeoutMiddleware → LockMiddleware → RetryMiddleware → MetricsMiddleware → IJobWorker.ExecuteAsync
```

顺序决定语义，有两条不直观的结论：

- **超时在重试外层** —— `[JobTimeout]` 是整个「首次执行 + 全部重试」的总预算，不是单次尝试的预算。
- **锁在重试外层** —— 一次抢锁覆盖所有重试轮次；反过来，**抢锁失败不会被重试**，会直接变成一次失败执行记入历史。

重试判定看两样东西：抛出异常，或返回了 `IsSuccess == false` 的 `JobResult`。总尝试次数是 `MaxRetryCount + 1`。

自定义中间件：

```csharp
public class TracingMiddleware : IJobMiddleware
{
    public async Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
    {
        using var activity = MyTracer.StartActivity(context.JobInstance.JobName);
        return await next(context);
    }
}
```

```csharp
services.AddXiHanTasks(config).AddMiddleware<TracingMiddleware>();
```

注册顺序即执行顺序，追加的中间件排在五个内置中间件之后，也就是**最靠近业务代码**的位置。

### 多租户上下文切换

两条线的租户来源不一样，这是最容易混淆的地方：

| 能力线 | 租户从哪来 | 切换时机 |
| --- | --- | --- |
| 定时调度 | 触发时按优先级解析：① 参数里的 `tenantId` → ② `JobInfo.TenantId` → ③ 当前异步上下文租户 | `JobExecutor` 用 `ICurrentTenant.Change(...)` 包住整条管道 |
| 后台作业 | **入队那一刻**的 `ICurrentTenant.Id`，写进 `BackgroundJobInfo.TenantId` | Worker 执行前 `ICurrentTenant.Change(job.TenantId)` |

`TenantId` 为空表示宿主（Host）级任务。

::: warning 任务类的构造函数在切租户之前执行
`JobExecutor` 先用 `ActivatorUtilities.CreateInstance` 构造 `IJobWorker` 实例，再切换租户上下文。构造函数里做的任何依赖租户的查询都会落在错误的上下文里 —— **把租户相关的活儿全放进 `ExecuteAsync`**。
:::

::: warning 任务类上的 AOP 特性不生效
同样因为走 `ActivatorUtilities.CreateInstance`，任务类型本身不经过容器解析，拿不到拦截器代理。打在 `IJobWorker` 实现类上的 `[UnitOfWork]`、`[Cacheable]` 之类特性会静默失效。

正确做法：把需要事务/缓存的逻辑放进从容器注入的应用服务里，任务类只负责调用它。见 [AOP 与拦截器](./aop) 和 [工作单元与事务](./uow)。
:::

### 分布式锁与单活

框架里有两处独立的锁，都走 Caching 的统一 `IDistributedLock`：

| 位置 | 触发条件 | 键 / TTL | 抢不到时 |
| --- | --- | --- | --- |
| `LockMiddleware`（定时任务） | 仅当 `AllowConcurrent = false` | `job:lock:{任务名}`，TTL = `TimeoutMilliseconds + 5000ms` | 返回失败结果并记入历史 |
| `BackgroundJobWorker`（后台作业） | 每一轮轮询 | `DistributedLockName`（默认 `XiHanBackgroundJobWorker`），TTL = `DistributedLockExpirySeconds`（默认 300 秒） | 本轮跳过，下一轮再试 |

定时任务的防重入还有第二道闸：`CompositeJobScheduler` 在触发前会查 `IJobStore.GetRunningInstancesAsync`，有运行中实例就直接跳过本次触发（这一道只看本进程的存储）。

::: danger 没接 Redis 时锁退化为进程内锁
`IDistributedLock` 在 Redis 未启用时退化为进程内实现。多实例部署下的后果：定时任务每个实例各跑一遍、后台作业 Worker 不再单活、同一批作业被重复执行。

单机开发无所谓，**多实例生产必须启用 Redis**。另外注意 `AllowConcurrent` 默认是 `true`，也就是默认**根本不加锁** —— 需要单活的任务必须显式写 `[JobConcurrent(false)]`。
:::

### 存储替换

默认实现全部是进程内内存，进程重启即丢：

| 存的是什么 | 默认实现 | 换成别的 |
| --- | --- | --- |
| 任务实例与执行历史 | `InMemoryJobStore` | `AddXiHanTasks(config).UseStore<MyJobStore>()` |
| 任务锁提供者 | `CachingJobLockProvider` | `.UseLockProvider<MyLockProvider>()` |
| 待执行的后台作业 | `InMemoryBackgroundJobStore` | `services.UseRedisBackgroundJobStore()`，或自实现 `IBackgroundJobStore` |

框架侧用的是 `TryAdd` 语义，业务模块的 `ConfigureServices` 在框架模块之后执行，后注册者胜出。

一行切到 Redis 持久化（复用 Caching 注册的连接）：

```csharp
services.UseRedisBackgroundJobStore(o =>
{
    o.KeyPrefix = "MyApp:BackgroundJobs";
    o.AbandonedRetentionDays = 7;
});
```

自实现 `IBackgroundJobStore` 时，`GetWaitingJobsAsync` 必须遵守契约，否则多实例会重复执行：

- 过滤：`ApplicationName` 匹配 且 `!IsAbandoned` 且 `NextTryTime <= 当前时间`
- 排序：`Priority` 降序 → `TryCount` 升序 → `NextTryTime` 升序
- 限量：最多 `maxResultCount` 条
- **原子领取**：领到的作业对其他实例不可见

## 配置

| 配置节 | 绑定到 | 是否生效 |
| --- | --- | --- |
| `XiHan:BackgroundJobs` | `BackgroundJobWorkerOptions` | 模块自动绑定，全部生效 |
| `XiHan:Tasks:ScheduledJobs` | `XiHanJobOptions` | 绑定了，但当前实现不读取（见下方警告） |
| 无配置节 | `XiHanBackgroundServiceOptions` | 需自行 `services.Configure<…>(…)` |

常用的后台作业配置：

```json
{
  "XiHan": {
    "BackgroundJobs": {
      "IsJobExecutionEnabled": true,
      "ApplicationName": "MyApp",
      "JobPollPeriodMilliseconds": 5000,
      "MaxJobFetchCount": 1000,
      "DefaultFirstWaitDurationSeconds": 60,
      "DefaultWaitFactor": 2.0,
      "DefaultTimeoutSeconds": 172800,
      "DistributedLockExpirySeconds": 300
    }
  }
}
```

- `IsJobExecutionEnabled = false` 只停执行，入队照常可用 —— 数据迁移窗口期很好用。
- `ApplicationName` 用于多个应用共用同一份存储时互相隔离；**入队端和 Worker 端读的是同一份配置**，所以天然一致，但不同应用之间彼此看不见对方的作业。

::: warning XiHan:Tasks:ScheduledJobs 目前不影响运行时行为
`XiHanJobOptions` 的字段（`Enabled`、`AutoDiscoverJobs`、`JobAssemblyPatterns`、`DefaultTimeoutMilliseconds`、`HistoryRetentionDays`、`EnableMetrics`、`NodeName`）会被绑定成选项对象，但调度器、执行器、存储都不读取它们。实际生效的是：

- 超时 → `JobInfo.TimeoutMilliseconds`（`[JobTimeout]`，默认 300000）
- 任务注册 → 必须显式调用 `RegisterJobsFromAssembly` / `RegisterCronJob` / `RegisterIntervalJob` / `RegisterJob`
- 历史清理 → 自行调用 `IJobStore.CleanupHistoryAsync(retentionDays)`
- 执行节点名 → `JobInstance.ExecutionNode`，由调度器写入 `Environment.MachineName`

另外 `IJobEventPublisher` 的默认实现 `DefaultJobEventPublisher` 是空实现，`JobMetricsProvider` 已注册但内置中间件不向它写入 —— 需要任务指标请自行实现 `IJobMiddleware` 采集。
:::

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 任务注册成功却从不执行 | 漏了 `[JobSchedule]`：`TriggerType` 取默认值 `Cron` 而表达式为空，算不出下次时间；日志里有 Warning |
| `RegisterJobsFromAssembly` 没扫到我的任务 | 类型缺 `[JobName]`，扫描时被静默跳过 |
| `[JobSchedule]` 上设了 `DelaySeconds` 却没延迟触发 | 该属性不会把触发类型改成 `Delay`，需直接构造 `JobInfo` 指定 `TriggerType = JobTriggerType.Delay` |
| Cron 触发时间比预期早/晚几小时 | Cron 按服务器本地时区求值，容器里没设 `TZ` 就是 UTC |
| 多实例下定时任务重复执行 | `AllowConcurrent` 默认 `true` 不加锁；或 Redis 未启用，锁退化为进程内 |
| 历史里出现「无法获取任务锁」的失败记录 | 锁在重试外层，抢锁失败直接记为一次失败执行，不会重试 |
| `[JobTimeout]` 设了 5 分钟，任务却跑了 15 分钟才超时 | 超时是「首次 + 全部重试」的总预算，与重试次数无关；实际观感差异来自重试间隔 |
| 任务里的 `[UnitOfWork]` / `[Cacheable]` 不生效 | 任务实例由 `ActivatorUtilities.CreateInstance` 构造，不经容器代理；把逻辑挪进注入的服务 |
| 任务构造函数里查不到租户数据 | 租户上下文在构造之后才切换，把逻辑挪进 `ExecuteAsync` |
| 重启后待执行的后台作业全没了 | 默认 `InMemoryBackgroundJobStore` 是进程内的，换 `UseRedisBackgroundJobStore()` 或自实现 |
| 入队的作业迟迟不执行 | `IsJobExecutionEnabled = false`；或首轮等待 5 秒 + 轮询间隔 5 秒的正常延迟；或多实例下锁被别的实例持有 |
| 作业只试了一次就被放弃 | 属于致命错误：作业名找不到配置（改过参数类型名且没标 `[BackgroundJobName]`），或参数反序列化失败 |
| 作业反复重试很久才放弃 | 没有次数上限，只有累计耗时上限 `DefaultTimeoutSeconds`（默认 2 天） |
| 常驻服务在多实例下重复消费 | 基类不做单活，需要在 `FetchWorkItemsAsync` 里原子领取 |
| 常驻服务里拿不到当前租户 | 基类不做租户切换，自己在 `ProcessItemAsync` 里 `ICurrentTenant.Change(...)` |

## 下一步

- [多租户](./multi-tenancy)：`ICurrentTenant` 与上下文切换的完整规则
- [缓存与分布式锁](./caching)：任务锁和 Worker 单活锁的底座
- [工作单元与事务](./uow)：任务里怎么正确开事务
- [Tasks 包](../packages/tasks)：完整 API 清单与全部配置项
