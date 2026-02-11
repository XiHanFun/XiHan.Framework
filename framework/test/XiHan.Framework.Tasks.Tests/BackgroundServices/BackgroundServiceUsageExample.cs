#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BackgroundServiceUsageExample
// Guid:573c52f1-e237-408a-98c6-0e35576d96d7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/17 18:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Tasks.BackgroundServices;
using XiHan.Framework.Utils.Diagnostics.RetryPolicys;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 后台服务使用示例
/// 展示如何配置、启动和管理后台服务
/// </summary>
public class BackgroundServiceUsageExample
{
    /// <summary>
    /// 基本使用示例
    /// </summary>
    public static async Task BasicUsageExample()
    {
        // 1. 创建服务集合
        var services = new ServiceCollection();

        // 2. 配置日志
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // 3. 配置后台服务选项
        services.Configure<XiHanBackgroundServiceOptions>(options =>
        {
            options.MaxConcurrentTasks = 3;
            options.IdleDelayMilliseconds = 2000;
            options.MaxRetryCount = 3;
            options.RetryDelayMilliseconds = 1000;
            options.EnableRetry = true;
            options.EnableTaskTimeout = true;
            options.TaskTimeoutMilliseconds = 10000; // 10秒超时
            options.ShutdownTimeoutMilliseconds = 30000; // 30秒关闭超时
        });

        // 4. 注册邮件发送器
        services.AddSingleton<IEmailSender, MockEmailSender>();

        // 5. 注册后台服务
        services.AddHostedService<EmailSendingService>();

        // 6. 构建服务提供者
        var serviceProvider = services.BuildServiceProvider();

        // 7. 创建主机
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(serviceCollection =>
            {
                // 将已配置的服务添加到主机
                foreach (var service in services)
                {
                    serviceCollection.Add(service);
                }
            })
            .Build();

        Console.WriteLine("=== 基本后台服务示例 ===");
        Console.WriteLine("启动邮件发送服务...");

        // 8. 启动主机
        var cancellationTokenSource = new CancellationTokenSource();
        var hostTask = host.RunAsync(cancellationTokenSource.Token);

        // 9. 等待服务启动
        await Task.Delay(2000);

        // 10. 获取服务实例并添加一些邮件到队列
        var emailService = serviceProvider.GetRequiredService<EmailSendingService>();

        emailService.QueueEmail("customer@example.com", "订单确认", "您的订单已确认。", EmailPriority.High);
        emailService.QueueEmail("user@example.com", "新功能通知", "我们推出了新功能！", EmailPriority.Normal);
        emailService.QueueEmail("newsletter@example.com", "周报", "本周系统运行报告。", EmailPriority.Low);

        Console.WriteLine($"已添加 3 封邮件到队列，当前队列长度: {emailService.GetQueuedEmailCount()}");

        // 11. 运行一段时间
        Console.WriteLine("服务运行中，按任意键停止...");
        Console.ReadKey();

        // 12. 停止服务
        Console.WriteLine("正在停止服务...");
        cancellationTokenSource.Cancel();

        try
        {
            await hostTask;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("服务已成功停止。");
        }
    }

    /// <summary>
    /// 高级使用示例（包含动态配置和自定义重试策略）
    /// </summary>
    public static async Task AdvancedUsageExample()
    {
        Console.WriteLine("\n=== 高级后台服务示例 ===");

        var services = new ServiceCollection();

        // 配置日志
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 配置后台服务选项
        services.Configure<XiHanBackgroundServiceOptions>(options =>
        {
            options.MaxConcurrentTasks = 2;
            options.IdleDelayMilliseconds = 1000;
            options.MaxRetryCount = 5;
            options.RetryDelayMilliseconds = 2000;
            options.EnableRetry = true;
            options.EnableTaskTimeout = true;
            options.TaskTimeoutMilliseconds = 15000;
            options.ShutdownTimeoutMilliseconds = 30000;
        });

        // 注册动态配置（可选）
        services.AddSingleton<IDynamicServiceConfig>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<XiHanBackgroundServiceOptions>>();
            return new DynamicServiceConfig(options);
        });

        // 注册自定义重试策略（可选）
        services.AddSingleton(provider =>
        {
            // 创建指数退避重试策略：最多重试5次，初始延迟2秒，每次翻倍，最大延迟1分钟
            return RetryPolicyFactory.WithExponentialBackoff(
                maxRetries: 5,
                baseDelay: TimeSpan.FromSeconds(2),
                backoffMultiplier: 2.0,
                maxDelay: TimeSpan.FromMinutes(1));
        });

        // 注册邮件发送器
        services.AddSingleton<IEmailSender, MockEmailSender>();

        // 注册后台服务（使用完整构造函数）
        services.AddSingleton<EmailSendingService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<EmailSendingService>>();
            var options = provider.GetRequiredService<IOptions<XiHanBackgroundServiceOptions>>();
            var emailSender = provider.GetRequiredService<IEmailSender>();
            var dynamicConfig = provider.GetRequiredService<IDynamicServiceConfig>();
            var retryPolicy = provider.GetRequiredService<RetryPolicy>();

            return new EmailSendingService(logger, options, emailSender, dynamicConfig, retryPolicy);
        });

        services.AddHostedService<EmailSendingService>(provider => provider.GetRequiredService<EmailSendingService>());

        var serviceProvider = services.BuildServiceProvider();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(serviceCollection =>
            {
                foreach (var service in services)
                {
                    serviceCollection.Add(service);
                }
            })
            .Build();

        Console.WriteLine("启动高级邮件发送服务...");

        var cancellationTokenSource = new CancellationTokenSource();
        var hostTask = host.RunAsync(cancellationTokenSource.Token);

        await Task.Delay(2000);

        // 获取服务实例
        var emailService = serviceProvider.GetRequiredService<EmailSendingService>();

        // 添加邮件到队列
        for (var i = 1; i <= 10; i++)
        {
            var priority = i <= 3 ? EmailPriority.High :
                          i <= 7 ? EmailPriority.Normal : EmailPriority.Low;

            emailService.QueueEmail($"user{i}@example.com", $"邮件 #{i}", $"这是第 {i} 封测试邮件", priority);
        }

        Console.WriteLine($"已添加 10 封邮件到队列");

        // 演示动态配置调整
        var dynamicConfig = emailService.GetDynamicConfig();

        Console.WriteLine("\n=== 动态配置演示 ===");

        // 等待一段时间
        await Task.Delay(5000);

        // 增加并发数
        Console.WriteLine("调整最大并发数从 2 增加到 5...");
        dynamicConfig.UpdateMaxConcurrentTasks(5);

        await Task.Delay(3000);

        // 暂停任务处理
        Console.WriteLine("暂停任务处理...");
        dynamicConfig.SetTaskProcessingEnabled(false);

        await Task.Delay(3000);

        // 恢复任务处理
        Console.WriteLine("恢复任务处理...");
        dynamicConfig.SetTaskProcessingEnabled(true);

        await Task.Delay(3000);

        // 调整空闲延迟
        Console.WriteLine("调整空闲延迟从 1000ms 减少到 500ms...");
        dynamicConfig.UpdateIdleDelay(500);

        // 显示统计信息
        Console.WriteLine("\n=== 服务统计信息 ===");
        await Task.Delay(5000);

        var statistics = emailService.GetStatistics();
        var status = emailService.GetServiceStatus();

        Console.WriteLine($"服务名称: {status.ServiceName}");
        Console.WriteLine($"任务处理启用: {status.IsTaskProcessingEnabled}");
        Console.WriteLine($"最大并发数: {status.MaxConcurrentTasks}");
        Console.WriteLine($"当前运行任务数: {status.CurrentRunningTasks}");
        Console.WriteLine($"空闲延迟: {status.IdleDelayMilliseconds}ms");
        Console.WriteLine($"重试启用: {status.RetryEnabled}");
        Console.WriteLine($"运行时长: {statistics.Uptime}");
        Console.WriteLine($"已处理任务: {statistics.TotalTasksProcessed}");
        Console.WriteLine($"失败任务: {statistics.TotalTasksFailed}");
        Console.WriteLine($"重试任务: {statistics.TotalTasksRetried}");
        Console.WriteLine($"成功率: {statistics.SuccessRate:F2}%");
        Console.WriteLine($"平均处理时间: {statistics.AverageProcessingTimeMs:F2}ms");

        if (serviceProvider.GetRequiredService<IEmailSender>() is MockEmailSender mockEmailSender)
        {
            Console.WriteLine($"实际发送邮件数: {mockEmailSender.GetSentEmailCount()}");
        }

        Console.WriteLine("\n按任意键停止服务...");
        Console.ReadKey();

        Console.WriteLine("正在停止服务...");
        cancellationTokenSource.Cancel();

        try
        {
            await hostTask;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("服务已成功停止。");
        }
    }

    /// <summary>
    /// 生产环境配置示例
    /// </summary>
    public static void ProductionConfigurationExample()
    {
        Console.WriteLine("\n=== 生产环境配置示例 ===");

        var services = new ServiceCollection();

        // 生产环境日志配置
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            // builder.AddSerilog(); // 可以添加 Serilog
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 生产环境后台服务配置
        services.Configure<XiHanBackgroundServiceOptions>(options =>
        {
            options.MaxConcurrentTasks = Environment.ProcessorCount * 2; // 基于 CPU 核心数
            options.IdleDelayMilliseconds = 5000; // 生产环境可以设置更长的间隔
            options.MaxRetryCount = 3;
            options.RetryDelayMilliseconds = 30000; // 30秒重试间隔
            options.EnableRetry = true;
            options.EnableTaskTimeout = true;
            options.TaskTimeoutMilliseconds = 300000; // 5分钟超时
            options.ShutdownTimeoutMilliseconds = 60000; // 1分钟关闭超时
        });

        // 生产环境重试策略
        services.AddSingleton(provider =>
        {
            return RetryPolicyFactory.WithExponentialBackoff(
                maxRetries: 3,
                baseDelay: TimeSpan.FromSeconds(30),
                backoffMultiplier: 2.0,
                maxDelay: TimeSpan.FromMinutes(10));
        });

        // 注册真实的邮件发送器（生产环境应该使用 SMTP 客户端）
        // services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IEmailSender, MockEmailSender>(); // 示例中仍使用模拟器

        // 注册后台服务
        services.AddHostedService<EmailSendingService>();

        Console.WriteLine("生产环境配置:");
        Console.WriteLine($"- 最大并发数: {Environment.ProcessorCount * 2}");
        Console.WriteLine("- 空闲延迟: 5秒");
        Console.WriteLine("- 重试次数: 3次");
        Console.WriteLine("- 重试间隔: 30秒起，指数退避");
        Console.WriteLine("- 任务超时: 5分钟");
        Console.WriteLine("- 关闭超时: 1分钟");
        Console.WriteLine("- 启用日志: Console + EventLog");
    }

    /// <summary>
    /// 运行所有示例
    /// </summary>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("XiHan 后台服务框架使用示例");
        Console.WriteLine("==============================");

        try
        {
            // 基本示例
            await BasicUsageExample();

            // 高级示例
            await AdvancedUsageExample();

            // 生产环境配置示例
            ProductionConfigurationExample();

            Console.WriteLine("\n所有示例运行完成！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"示例运行出错: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}

/*
使用方法：

1. 基本使用：
```csharp
await BackgroundServiceUsageExample.BasicUsageExample();
```

2. 高级使用：
```csharp
await BackgroundServiceUsageExample.AdvancedUsageExample();
```

3. 运行所有示例：
```csharp
await BackgroundServiceUsageExample.RunAllExamples();
```

4. ASP.NET Core 集成：
```csharp
// Program.cs
builder.Services.Configure<XiHanBackgroundServiceOptions>(
    builder.Configuration.GetSection("BackgroundService"));

builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<EmailSendingService>();

var app = builder.Build();
```

5. appsettings.json 配置：
```json
{
  "BackgroundService": {
    "MaxConcurrentTasks": 5,
    "IdleDelayMilliseconds": 2000,
    "MaxRetryCount": 3,
    "RetryDelayMilliseconds": 5000,
    "EnableRetry": true,
    "EnableTaskTimeout": true,
    "TaskTimeoutMilliseconds": 30000,
    "ShutdownTimeoutMilliseconds": 60000
  }
}
```

6. 运行时动态调整：
```csharp
var emailService = serviceProvider.GetService<EmailSendingService>();
var config = emailService.GetDynamicConfig();

// 调整并发数
config.UpdateMaxConcurrentTasks(10);

// 暂停/恢复处理
config.SetTaskProcessingEnabled(false);
config.SetTaskProcessingEnabled(true);

// 调整延迟
config.UpdateIdleDelay(1000);

// 获取状态
var status = emailService.GetServiceStatus();
var stats = emailService.GetStatistics();
```
*/
