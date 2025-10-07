# XiHan.Framework.Tasks - 任务调度框架

## 📖 概述

XiHan.Framework.Tasks 是一个功能完善的任务调度框架，支持 Cron、固定间隔、延时等多种调度方式，具备分布式协调、任务重试、幂等控制、状态持久化等企业级特性。

## ✨ 核心特性

- 🕐 **多种调度方式**：支持 Cron、Interval、Delay、Manual 四种触发方式
- 🔄 **重试机制**：内置智能重试策略，支持指数退避
- 🔒 **分布式锁**：防止任务在集群环境下重复执行
- 💾 **状态持久化**：支持内存、数据库、Redis 多种存储方案
- 🎯 **中间件管道**：日志、重试、超时、锁、度量等可扩展中间件
- 📊 **性能监控**：实时统计任务执行情况和性能指标
- 🎨 **优雅集成**：与 ASP.NET Core 无缝集成

## 🚀 快速开始

### 1. 安装

```bash
dotnet add package XiHan.Framework.Tasks
```

### 2. 注册服务

```csharp
// Program.cs 或 Startup.cs
services.AddXiHanJobs(options =>
{
    options.Enabled = true;
    options.AutoDiscoverJobs = true;
    options.EnableMetrics = true;
});
```

### 3. 创建任务

#### 方式一：使用特性标记

```csharp
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Attributes;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

[JobName("DailyReportJob")]
[JobDescription("每日报表生成任务")]
[JobSchedule("0 0 2 * * ?")]  // 每天凌晨2点执行
[JobRetry(MaxRetryCount = 3)]
[JobTimeout(300000)]  // 5分钟超时
[JobConcurrent(false)]  // 不允许并发执行
public class DailyReportJob : IJob
{
    private readonly ILogger<DailyReportJob> _logger;
    private readonly IReportService _reportService;

    public DailyReportJob(ILogger<DailyReportJob> logger, IReportService reportService)
    {
        _logger = logger;
        _reportService = reportService;
    }

    public async Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("开始生成每日报表...");

            var report = await _reportService.GenerateDailyReportAsync(cancellationToken);

            _logger.LogInformation("每日报表生成完成");
            return JobResult.Success(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成每日报表失败");
            return JobResult.Failure(ex.Message, ex);
        }
    }
}
```

#### 方式二：手动注册

```csharp
// 在服务配置中
services.AddXiHanJobs()
    .AddJob<DailyReportJob>();

// 在应用启动后手动注册
var scheduler = app.Services.GetRequiredService<IJobScheduler>();

// Cron 任务
scheduler.RegisterCronJob<DailyReportJob>(
    jobName: "DailyReport",
    cronExpression: "0 0 2 * * ?",
    description: "每日报表生成"
);

// 固定间隔任务
scheduler.RegisterIntervalJob<DataSyncJob>(
    jobName: "DataSync",
    interval: TimeSpan.FromMinutes(5),
    description: "数据同步任务"
);

// 手动触发任务
await scheduler.TriggerJobAsync("DailyReport");
```

## 📝 详细示例

### Cron 表达式任务

```csharp
[JobName("EmailNotificationJob")]
[JobSchedule("0 */5 * * * ?")]  // 每5分钟执行一次
public class EmailNotificationJob : IJob
{
    public async Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
    {
        // 发送邮件通知
        return JobResult.Success();
    }
}
```

### 固定间隔任务

```csharp
[JobName("HealthCheckJob")]
[JobSchedule(300)]  // 每300秒执行一次
public class HealthCheckJob : IJob
{
    public async Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
    {
        // 健康检查逻辑
        return JobResult.Success();
    }
}
```

### 带参数的任务

```csharp
public class DataExportJob : IJob
{
    public async Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
    {
        // 从参数中获取导出配置
        var startDate = context.Parameters.TryGetValue("startDate", out var sd)
            ? (DateTime)sd!
            : DateTime.Today;

        var endDate = context.Parameters.TryGetValue("endDate", out var ed)
            ? (DateTime)ed!
            : DateTime.Today;

        // 执行数据导出
        return JobResult.Success();
    }
}

// 触发时传递参数
await scheduler.TriggerJobAsync("DataExport", new Dictionary<string, object?>
{
    ["startDate"] = DateTime.Today.AddDays(-30),
    ["endDate"] = DateTime.Today
});
```

## ⚙️ 高级配置

### 使用 Redis 锁（分布式环境）

```csharp
services.AddXiHanJobs()
    .UseLockProvider<RedisLockProvider>();
```

### 使用数据库存储

```csharp
services.AddXiHanJobs()
    .UseStore<SqlJobStore>();
```

### 自定义中间件

```csharp
public class CustomMiddleware : IJobMiddleware
{
    public async Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
    {
        // 执行前逻辑
        Console.WriteLine($"任务 {context.JobInstance.JobName} 即将执行");

        var result = await next(context);

        // 执行后逻辑
        Console.WriteLine($"任务 {context.JobInstance.JobName} 执行完成");

        return result;
    }
}

// 注册自定义中间件
services.AddXiHanJobs()
    .AddMiddleware<CustomMiddleware>();
```

## 📊 监控与度量

```csharp
// 获取任务度量信息
var metricsProvider = app.Services.GetRequiredService<JobMetricsProvider>();
var metrics = metricsProvider.GetMetrics("DailyReport");

Console.WriteLine($"总执行次数: {metrics.TotalExecutions}");
Console.WriteLine($"成功次数: {metrics.SuccessCount}");
Console.WriteLine($"失败次数: {metrics.FailureCount}");
Console.WriteLine($"平均耗时: {metrics.AverageDurationMs}ms");
Console.WriteLine($"成功率: {metrics.SuccessRate}%");
```

## 🎯 任务管理

```csharp
var scheduler = app.Services.GetRequiredService<IJobScheduler>();

// 暂停任务
scheduler.PauseJob("DailyReport");

// 恢复任务
scheduler.ResumeJob("DailyReport");

// 取消注册任务
scheduler.UnregisterJob("DailyReport");

// 获取下次执行时间
var nextTime = scheduler.GetNextFireTime("DailyReport");

// 获取所有任务
var allJobs = scheduler.GetAllJobs();
```

## 🔧 配置选项

```csharp
services.AddXiHanJobs(options =>
{
    // 是否启用任务调度
    options.Enabled = true;

    // 是否自动发现并注册任务
    options.AutoDiscoverJobs = true;

    // 任务扫描程序集名称模式
    options.JobAssemblyPatterns = new[] { "*.Jobs", "*.Tasks" };

    // 默认任务超时时间（毫秒）
    options.DefaultTimeoutMilliseconds = 300000;

    // 是否启用分布式锁
    options.EnableDistributedLock = false;

    // 历史记录保留天数
    options.HistoryRetentionDays = 30;

    // 是否启用性能监控
    options.EnableMetrics = true;

    // 任务执行节点名称
    options.NodeName = Environment.MachineName;
});
```

## 📚 常见 Cron 表达式

| 表达式              | 说明                            |
| ------------------- | ------------------------------- |
| `0 0 * * * ?`       | 每小时整点执行                  |
| `0 */5 * * * ?`     | 每 5 分钟执行                   |
| `0 0 2 * * ?`       | 每天凌晨 2 点执行               |
| `0 0 2 * * 1`       | 每周一凌晨 2 点执行             |
| `0 0 2 1 * ?`       | 每月 1 号凌晨 2 点执行          |
| `0 0 9-18 * * ?`    | 每天 9 点到 18 点每小时执行     |
| `0 0/30 9-18 * * ?` | 每天 9 点到 18 点每 30 分钟执行 |

## 🤝 最佳实践

1. **幂等性**：确保任务可以安全地重复执行
2. **超时控制**：为长时间运行的任务设置合理的超时时间
3. **错误处理**：妥善处理异常，返回明确的错误信息
4. **日志记录**：记录关键操作和异常信息
5. **参数验证**：在任务执行前验证输入参数
6. **资源释放**：及时释放数据库连接、文件句柄等资源
7. **分布式锁**：在集群环境下使用分布式锁防止重复执行

## 📄 License

MIT License
