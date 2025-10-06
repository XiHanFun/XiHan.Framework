# 调度任务使用说明

## 简介

XiHan.Framework.Tasks.ScheduledJobs 提供了基于 Quartz.NET 的调度任务功能，集成了自定义的 Cron 表达式解析器。

## 快速开始

### 1. 配置服务

在 `Startup.cs` 或 `Program.cs` 中配置调度任务服务：

```csharp
services.AddScheduledJobs(options =>
{
    options.Enabled = true;
    options.SchedulerName = "XiHanScheduler";
    options.ThreadPoolSize = 10;
    options.AllowConcurrentExecution = false;
});
```

### 2. 创建调度任务

继承 `XiHanScheduledJobBase` 并实现任务逻辑：

```csharp
using XiHan.Framework.Tasks.ScheduledJobs;
using Microsoft.Extensions.Logging;
using Quartz;

[ScheduledJob(
    JobName = "DailyReportJob",
    JobGroup = "Reports",
    CronExpression = "0 0 9 * * ?",  // 每天早上9点执行
    Description = "生成每日报表",
    AutoStart = true)]
public class DailyReportJob : XiHanScheduledJobBase
{
    public DailyReportJob(ILogger<DailyReportJob> logger) : base(logger)
    {
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        Logger.LogInformation("开始生成每日报表...");

        // 实现你的任务逻辑
        await Task.Delay(1000);

        Logger.LogInformation("每日报表生成完成");
    }

    protected override Task OnExecuteFailedAsync(IJobExecutionContext context, Exception exception)
    {
        Logger.LogError(exception, "生成每日报表失败");
        // 可以在这里添加告警、重试等逻辑
        return Task.CompletedTask;
    }
}
```

### 3. 注册任务

```csharp
// 方式一：手动注册单个任务
services.AddScheduledJob<DailyReportJob>();

// 方式二：自动扫描并注册程序集中所有带 ScheduledJobAttribute 的任务
services.AddScheduledJobsFromAssemblies(typeof(DailyReportJob).Assembly);
```

### 4. 动态管理任务

通过依赖注入 `IScheduledJobManager` 来动态管理任务：

```csharp
public class JobController : ControllerBase
{
    private readonly IScheduledJobManager _jobManager;

    public JobController(IScheduledJobManager jobManager)
    {
        _jobManager = jobManager;
    }

    // 添加任务
    [HttpPost("add")]
    public async Task<IActionResult> AddJob()
    {
        var result = await _jobManager.AddJobAsync<DailyReportJob>(
            jobName: "DailyReportJob",
            jobGroup: "Reports",
            cronExpression: "0 0 9 * * ?",
            description: "生成每日报表",
            startImmediately: false);

        return Ok(result);
    }

    // 立即执行任务
    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerJob(string jobName, string jobGroup)
    {
        var result = await _jobManager.TriggerJobAsync(jobName, jobGroup);
        return Ok(result);
    }

    // 暂停任务
    [HttpPost("pause")]
    public async Task<IActionResult> PauseJob(string jobName, string jobGroup)
    {
        var result = await _jobManager.PauseJobAsync(jobName, jobGroup);
        return Ok(result);
    }

    // 恢复任务
    [HttpPost("resume")]
    public async Task<IActionResult> ResumeJob(string jobName, string jobGroup)
    {
        var result = await _jobManager.ResumeJobAsync(jobName, jobGroup);
        return Ok(result);
    }

    // 删除任务
    [HttpDelete("remove")]
    public async Task<IActionResult> RemoveJob(string jobName, string jobGroup)
    {
        var result = await _jobManager.RemoveJobAsync(jobName, jobGroup);
        return Ok(result);
    }

    // 更新 Cron 表达式
    [HttpPut("update-cron")]
    public async Task<IActionResult> UpdateJobCron(string jobName, string jobGroup, string cronExpression)
    {
        var result = await _jobManager.UpdateJobCronAsync(jobName, jobGroup, cronExpression);
        return Ok(result);
    }

    // 获取所有任务
    [HttpGet("list")]
    public async Task<IActionResult> GetAllJobs()
    {
        var jobs = await _jobManager.GetAllJobsAsync();
        return Ok(jobs);
    }

    // 获取任务详情
    [HttpGet("info")]
    public async Task<IActionResult> GetJobInfo(string jobName, string jobGroup)
    {
        var job = await _jobManager.GetJobInfoAsync(jobName, jobGroup);
        return Ok(job);
    }
}
```

## Cron 表达式

### 格式说明

支持两种格式：

- 5 位格式：`分 时 日 月 周`
- 6 位格式：`秒 分 时 日 月 周`

### 常用示例

```csharp
// 预定义表达式
"@hourly"    // 每小时执行 (0 * * * *)
"@daily"     // 每天午夜执行 (0 0 * * *)
"@weekly"    // 每周日午夜执行 (0 0 * * 0)
"@monthly"   // 每月1号午夜执行 (0 0 1 * *)
"@yearly"    // 每年1月1号午夜执行 (0 0 1 1 *)

// 自定义表达式
"0 */5 * * *"        // 每5分钟执行一次
"0 9 * * *"          // 每天早上9点执行
"0 9 * * 1-5"        // 工作日早上9点执行
"0 0 1 * *"          // 每月1号午夜执行
"0 0 * * 0"          // 每周日午夜执行
"30 9 * * 1,3,5"     // 周一、三、五早上9:30执行
"0 0/30 8-17 * * ?"  // 每天8点到17点之间每30分钟执行
```

### Cron 工具方法

```csharp
using XiHan.Framework.Tasks.Crons;

// 验证 Cron 表达式
bool isValid = CronHelper.IsValidExpression("0 9 * * *");

// 获取下次执行时间
DateTime? nextTime = CronHelper.GetNextOccurrence("0 9 * * *");

// 获取未来 N 次执行时间
List<DateTime> nextTimes = CronHelper.GetNextOccurrences("0 9 * * *", 5);

// 判断指定时间是否匹配
bool isMatch = CronHelper.IsMatch("0 9 * * *", DateTime.Now);

// 获取表达式描述
string description = CronHelper.GetDescription("0 9 * * *");

// 使用构建器创建表达式
string cronExpr = CronExpressionBuilder.Create()
    .Minutes("0")
    .Hours("9")
    .Days("*")
    .Months("*")
    .DaysOfWeek("*")
    .Build();
```

## 特性说明

### ScheduledJobAttribute

| 属性             | 类型    | 说明           | 默认值    |
| ---------------- | ------- | -------------- | --------- |
| JobName          | string? | 任务名称       | 类名      |
| JobGroup         | string  | 任务分组       | "Default" |
| CronExpression   | string? | Cron 表达式    | -         |
| Description      | string? | 任务描述       | -         |
| StartImmediately | bool    | 是否立即执行   | false     |
| AutoStart        | bool    | 是否自动启动   | true      |
| Priority         | int     | 优先级（1-10） | 5         |

## 最佳实践

1. **任务幂等性**：确保任务可以安全地重复执行
2. **异常处理**：在 `ExecuteJobAsync` 中妥善处理异常
3. **日志记录**：记录任务开始、结束、异常等关键信息
4. **超时控制**：对于长时间运行的任务，考虑使用 CancellationToken
5. **资源清理**：及时释放资源，避免内存泄漏

## 注意事项

- 调度任务默认不允许并发执行，可通过配置 `AllowConcurrentExecution = true` 启用
- Cron 表达式使用 Quartz.NET 的格式，与 Linux Cron 略有不同
- 任务执行失败时会抛出异常，可通过 `OnExecuteFailedAsync` 处理
- 建议使用任务分组来组织和管理相关的任务
