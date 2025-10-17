# 日志系统使用示例

本文档展示了优化后日志系统的各种使用方式。

## 1. 基本使用

```csharp
using XiHan.Framework.Utils.Logging;

// 基本日志输出
LogHelper.Info("这是一条信息日志");
LogHelper.Success("操作成功");
LogHelper.Warn("这是一条警告");
LogHelper.Error("发生错误");
LogHelper.Handle("处理中...");
```

## 2. 配置日志系统

### 2.1 使用 Configure 方法统一配置

```csharp
// 统一配置日志系统
LogHelper.Configure(options =>
{
    // 基本配置
    options.MinimumLevel = LogLevel.Info;
    options.EnableConsoleOutput = true;
    options.EnableFileOutput = true;
    options.DisplayHeader = true;

    // 文件输出配置
    options.LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    options.MaxFileSize = 10 * 1024 * 1024; // 10MB

    // 性能配置
    options.QueueCapacity = 10000;
    options.BatchSize = 50;
    options.EnableAsyncWrite = true;

    // 高级功能
    options.EnableStatistics = true;
    options.LogFormat = LogFormat.Text; // 或 LogFormat.Json, LogFormat.Structured
});
```

### 2.2 单独配置各项

```csharp
// 设置最小日志级别
LogHelper.SetMinimumLevel(LogLevel.Warn);

// 启用/禁用控制台输出
LogHelper.SetConsoleOutputEnabled(true);

// 启用/禁用文件输出
LogHelper.SetFileOutputEnabled(true);

// 设置日志目录
LogHelper.SetLogDirectory(@"C:\MyApp\Logs");

// 设置最大文件大小
LogHelper.SetMaxFileSize(20 * 1024 * 1024); // 20MB

// 设置是否显示日志头
LogHelper.SetIsDisplayHeader(false);
```

## 3. 日志格式化

### 3.1 文本格式 (默认)

```csharp
LogHelper.Configure(options =>
{
    options.LogFormat = LogFormat.Text;
});

// 输出: [2024-01-15 10:30:45.123 INFO] 用户登录成功
LogHelper.Info("用户登录成功");
```

### 3.2 JSON 格式

```csharp
LogHelper.Configure(options =>
{
    options.LogFormat = LogFormat.Json;
});

// 输出: {"timestamp":"2024-01-15T10:30:45.123Z","level":"INFO","message":"用户登录成功"}
LogHelper.Info("用户登录成功");
```

### 3.3 结构化格式

```csharp
LogHelper.Configure(options =>
{
    options.LogFormat = LogFormat.Structured;
});

// 输出: Timestamp=2024-01-15T10:30:45.123Z | Level=INFO | Message=用户登录成功
LogHelper.Info("用户登录成功");
```

## 4. 日志统计

### 4.1 启用统计并查看统计信息

```csharp
// 启用统计
LogHelper.Configure(options =>
{
    options.EnableStatistics = true;
});

// 写入一些日志
LogHelper.Info("信息1");
LogHelper.Info("信息2");
LogHelper.Warn("警告1");
LogHelper.Error("错误1");

// 获取统计信息
var stats = LogHelper.GetStatistics();
Console.WriteLine($"总日志数: {stats.TotalLogsWritten}");
Console.WriteLine($"信息日志: {stats.InfoLogsWritten}");
Console.WriteLine($"警告日志: {stats.WarnLogsWritten}");
Console.WriteLine($"错误日志: {stats.ErrorLogsWritten}");
Console.WriteLine($"控制台写入: {stats.ConsoleWrites}");
Console.WriteLine($"文件写入: {stats.FileWrites}");
Console.WriteLine($"丢弃日志: {stats.LogsDropped}");
Console.WriteLine($"写入错误: {stats.WriteErrors}");

// 获取统计摘要
var summary = stats.GetSummary();
Console.WriteLine(summary);

// 重置统计
LogHelper.ResetStatistics();
```

### 4.2 文件输出统计

```csharp
// 启用文件输出统计
LogFileHelper.SetStatisticsEnabled(true);

// 写入日志...

// 获取文件输出统计
var fileStats = LogFileHelper.GetStatistics();
Console.WriteLine($"文件日志总数: {fileStats.TotalLogsWritten}");
Console.WriteLine($"队列丢弃: {fileStats.LogsDropped}");

// 获取配置信息
var config = LogFileHelper.GetConfiguration();
Console.WriteLine($"队列容量: {config.QueueCapacity}");
Console.WriteLine($"批次大小: {config.BatchSize}");
Console.WriteLine($"最大文件: {config.MaxFileSize} bytes");

// 获取通道状态
var status = LogFileHelper.GetQueueStatus();
Console.WriteLine(status.GetSummary());
Console.WriteLine($"使用率: {status.UsageRate:P0}");
if (status.IsNearFull)
{
    Console.WriteLine("警告：通道接近满载！");
}
```

## 5. 高级配置示例

### 5.1 高并发场景配置

```csharp
LogHelper.Configure(options =>
{
    options.EnableFileOutput = true;
    options.EnableAsyncWrite = true;
    options.QueueCapacity = 50000;  // 大容量队列
    options.BatchSize = 100;        // 大批次写入
    options.MaxFileSize = 50 * 1024 * 1024; // 50MB
});
```

### 5.2 调试模式配置

```csharp
LogHelper.Configure(options =>
{
    options.MinimumLevel = LogLevel.Info;  // 记录所有级别
    options.EnableConsoleOutput = true;
    options.EnableFileOutput = true;
    options.EnableStatistics = true;       // 启用统计便于分析
    options.LogFormat = LogFormat.Structured; // 结构化便于解析
});
```

### 5.3 生产环境配置

```csharp
LogHelper.Configure(options =>
{
    options.MinimumLevel = LogLevel.Warn;  // 只记录警告和错误
    options.EnableConsoleOutput = false;   // 禁用控制台输出
    options.EnableFileOutput = true;
    options.EnableAsyncWrite = true;
    options.LogFormat = LogFormat.Json;    // JSON 便于日志分析工具处理
    options.MaxFileSize = 100 * 1024 * 1024; // 100MB
});
```

## 6. 实用功能

### 6.1 格式化日志

```csharp
// 使用参数格式化
LogHelper.Info("用户 {0} 于 {1} 登录", username, DateTime.Now);
LogHelper.Error("处理订单 {0} 时发生错误: {1}", orderId, errorMessage);
```

### 6.2 异常日志

```csharp
try
{
    // 某些操作...
}
catch (Exception ex)
{
    LogHelper.Error("操作失败", ex);
}
```

### 6.3 表格日志

```csharp
using XiHan.Framework.Utils.ConsoleTools;

var table = new ConsoleTable("ID", "名称", "状态");
table.AddRow("1", "任务A", "完成");
table.AddRow("2", "任务B", "进行中");

// 以不同级别输出表格
LogHelper.InfoTable(table);
LogHelper.SuccessTable(table);
LogHelper.WarnTable(table);
LogHelper.ErrorTable(table);
```

### 6.4 彩虹渐变输出

```csharp
// 用于展示横幅或 Logo
string logo = @"
╔══════════════════════════════╗
║     My Awesome Application    ║
╚══════════════════════════════╝
";

LogHelper.Rainbow(logo);
```

### 6.5 清理和刷新

```csharp
// 清空控制台
LogHelper.Clear();

// 立即刷新待处理日志到文件
LogHelper.FlushToFile();

// 清除所有日志文件
LogHelper.ClearLogFiles();

// 清除指定日期的日志
LogHelper.ClearLogFiles(DateTime.Now.AddDays(-7));

// 优雅关闭日志系统
LogHelper.Shutdown();
```

## 7. 性能监控示例

```csharp
// 启用统计
LogHelper.Configure(options =>
{
    options.EnableStatistics = true;
    options.EnableFileOutput = true;
});

LogFileHelper.SetStatisticsEnabled(true);

// 模拟高并发写入
var tasks = new List<Task>();
for (int i = 0; i < 1000; i++)
{
    int index = i;
    tasks.Add(Task.Run(() =>
    {
        LogHelper.Info($"并发日志 {index}");
    }));
}

await Task.WhenAll(tasks);

// 等待队列处理完成
LogHelper.FlushToFile();

// 查看性能统计
var stats = LogHelper.GetStatistics();
var fileStats = LogFileHelper.GetStatistics();
var channelStatus = LogFileHelper.GetQueueStatus();

Console.WriteLine("=== 性能统计 ===");
Console.WriteLine($"总日志数: {stats.TotalLogsWritten}");
Console.WriteLine($"丢弃日志: {fileStats.LogsDropped}");
Console.WriteLine($"写入错误: {stats.WriteErrors}");
Console.WriteLine($"通道状态: {channelStatus.GetSummary()}");
Console.WriteLine($"使用率: {channelStatus.UsageRate:P0}");
Console.WriteLine($"工作任务: {(channelStatus.IsWorkerActive ? "活跃" : "停止")}");
```

## 8. 完整应用示例

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        // 初始化日志系统
        InitializeLogging();

        // 启动应用
        LogHelper.Info("应用程序启动...");

        try
        {
            // 应用逻辑...
            RunApplication();

            LogHelper.Success("应用程序运行成功");
        }
        catch (Exception ex)
        {
            LogHelper.Error("应用程序发生严重错误", ex);
        }
        finally
        {
            // 输出统计信息
            PrintStatistics();

            // 优雅关闭
            LogHelper.Info("应用程序关闭中...");
            LogHelper.Shutdown();
        }
    }

    private static void InitializeLogging()
    {
        LogHelper.Configure(options =>
        {
            options.MinimumLevel = LogLevel.Info;
            options.EnableConsoleOutput = true;
            options.EnableFileOutput = true;
            options.DisplayHeader = true;

            options.LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            options.MaxFileSize = 10 * 1024 * 1024;

            options.QueueCapacity = 10000;
            options.BatchSize = 50;
            options.EnableAsyncWrite = true;

            options.EnableStatistics = true;
            options.LogFormat = LogFormat.Text;
        });

        LogFileHelper.SetStatisticsEnabled(true);
    }

    private static void RunApplication()
    {
        LogHelper.Info("开始处理任务...");
        LogHelper.Handle("正在连接数据库...");
        LogHelper.Success("数据库连接成功");

        // 更多业务逻辑...
    }

    private static void PrintStatistics()
    {
        var stats = LogHelper.GetStatistics();
        var summary = stats.GetSummary();

        LogHelper.Info("=== 运行统计 ===");
        LogHelper.Info(summary);
    }
}
```

## 9. 最佳实践

### 9.1 合理设置日志级别

- **开发环境**: 使用 `LogLevel.Info` 以查看所有日志
- **测试环境**: 使用 `LogLevel.Info` 或 `LogLevel.Warn`
- **生产环境**: 使用 `LogLevel.Warn` 或 `LogLevel.Error` 以减少日志量

### 9.2 使用合适的日志格式

- **控制台输出**: 使用 `LogFormat.Text` 便于阅读
- **文件存储**: 使用 `LogFormat.Json` 或 `LogFormat.Structured` 便于后期分析
- **日志聚合**: 使用 `LogFormat.Json` 便于与日志分析工具集成

### 9.3 性能优化建议

- 高并发场景增大 `QueueCapacity` 和 `BatchSize`
- 启用 `EnableAsyncWrite` 避免阻塞主线程
- 定期清理旧日志文件
- 使用统计功能监控日志系统健康状况

### 9.4 资源管理

```csharp
// 应用程序退出时确保优雅关闭
AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
{
    LogHelper.Shutdown();
};
```

## 10. 故障排查

### 10.1 日志丢失

```csharp
// 检查队列状态
var status = LogFileHelper.GetQueueStatus();
Console.WriteLine($"队列计数: {status.QueueCount}");
Console.WriteLine($"是否关闭: {status.IsShutdown}");

// 检查统计信息
var stats = LogFileHelper.GetStatistics();
Console.WriteLine($"丢弃日志: {stats.LogsDropped}");

// 解决方案：增大队列容量
LogHelper.Configure(options =>
{
    options.QueueCapacity = 50000;
});
```

### 10.2 性能问题

```csharp
// 调整批处理大小
LogHelper.Configure(options =>
{
    options.BatchSize = 100; // 增大批次
    options.EnableAsyncWrite = true; // 确保异步写入
});
```

### 10.3 文件大小问题

```csharp
// 调整文件大小限制
LogHelper.Configure(options =>
{
    options.MaxFileSize = 50 * 1024 * 1024; // 50MB
});
```

## 总结

优化后的日志系统提供了：

- ✅ 统一的配置接口 (`LogOptions`)
- ✅ 详细的性能统计 (`LogStatistics`)
- ✅ 灵活的格式化器 (`ILogFormatter`)
- ✅ 高性能异步队列写入
- ✅ 完善的错误处理和资源管理

通过合理配置和使用这些功能，可以满足从开发到生产环境的各种日志需求。
