# Runtime 运行时模块

## 概述

Runtime 模块提供了全面的 .NET 应用程序运行时信息获取、性能监控和生命周期管理功能。该模块包含三个核心组件：

- **OSPlatformHelper**: 操作系统平台信息获取
- **RuntimeMonitor**: 应用程序性能监控
- **ApplicationLifecycleManager**: 应用程序生命周期管理

## 主要特性

### 🖥️ 跨平台兼容

- 支持 Windows、Linux、macOS、FreeBSD
- 智能平台检测和适配
- 平台特定功能自动处理

### 📊 运行时信息

- 操作系统详细信息
- .NET 框架版本检测
- 硬件架构信息
- 权限级别检测

### 📈 性能监控

- 实时性能指标收集
- 历史数据趋势分析
- 内存和 CPU 使用率监控
- GC 收集统计

### 🔄 生命周期管理

- 应用程序状态管理
- 优雅启动和关闭
- 自动重启机制
- 异常处理和恢复

## 快速开始

### 1. 获取系统信息

```csharp
using XiHan.Framework.Utils.Runtime;

// 获取基本系统信息
Console.WriteLine($"操作系统: {OSPlatformHelper.OperatingSystem}");
Console.WriteLine($"系统架构: {OSPlatformHelper.OsArchitecture}");
Console.WriteLine($"是否64位: {OSPlatformHelper.Is64BitOperatingSystem}");
Console.WriteLine($"处理器数量: {OSPlatformHelper.ProcessorCount}");

// 获取详细运行时信息
var runtimeInfo = OSPlatformHelper.GetRuntimeInfo();
Console.WriteLine($"框架类型: {runtimeInfo.FrameworkType}");
Console.WriteLine($"CLR版本: {runtimeInfo.ClrVersion}");
Console.WriteLine($"进程ID: {runtimeInfo.ProcessId}");

// 导出为JSON
var json = OSPlatformHelper.ToJson(indented: true);
Console.WriteLine(json);
```

### 2. 性能监控

```csharp
using XiHan.Framework.Utils.Runtime;

// 创建监控器
using var monitor = new RuntimeMonitor(
    monitorInterval: TimeSpan.FromSeconds(5),
    maxSnapshotCount: 500
);

// 开始监控
monitor.StartMonitoring();

// 获取当前性能快照
var snapshot = monitor.GetCurrentSnapshot();
Console.WriteLine($"CPU使用率: {snapshot.CpuUsage:F1}%");
Console.WriteLine($"内存使用: {snapshot.MemoryUsage / 1024 / 1024} MB");

// 等待一段时间收集数据
await Task.Delay(TimeSpan.FromMinutes(1));

// 分析趋势
var cpuTrend = monitor.AnalyzeTrend(
    PerformanceCounterType.CpuUsage,
    TimeSpan.FromMinutes(5)
);

Console.WriteLine($"CPU平均使用率: {cpuTrend.AverageValue:F1}%");
Console.WriteLine($"CPU使用趋势: {(cpuTrend.Trend > 0 ? "上升" : "下降")}");

// 导出监控数据
var reportJson = monitor.ExportToJson(includeAllSnapshots: false);
await File.WriteAllTextAsync("performance_report.json", reportJson);
```

### 3. 生命周期管理

```csharp
using XiHan.Framework.Utils.Runtime;

// 创建生命周期管理器
using var lifecycle = new ApplicationLifecycleManager();

// 配置自动重启
lifecycle.AutoRestartEnabled = true;
lifecycle.MaxRestartAttempts = 3;
lifecycle.RestartDelay = TimeSpan.FromSeconds(10);

// 注册事件处理
lifecycle.StateChanged += (sender, args) =>
{
    Console.WriteLine($"状态变更: {args.PreviousState} -> {args.State}");
    Console.WriteLine($"消息: {args.Message}");
};

lifecycle.ApplicationExiting += (sender, args) =>
{
    Console.WriteLine($"应用程序正在退出，原因: {args.Reason}");
    // 可以在这里执行清理工作

    // 如果需要，可以取消退出
    if (args.CanCancel && ShouldCancelExit())
    {
        args.Cancel = true;
    }
};

// 添加初始化任务
lifecycle.AddInitializeTask(async () =>
{
    Console.WriteLine("执行数据库连接初始化...");
    await InitializeDatabaseAsync();
});

lifecycle.AddInitializeTask(async () =>
{
    Console.WriteLine("加载配置文件...");
    await LoadConfigurationAsync();
});

// 添加关闭任务
lifecycle.AddShutdownTask(async () =>
{
    Console.WriteLine("保存应用程序状态...");
    await SaveApplicationStateAsync();
});

lifecycle.AddShutdownTask(async () =>
{
    Console.WriteLine("关闭数据库连接...");
    await CloseDatabaseConnectionsAsync();
});

// 初始化应用程序
try
{
    await lifecycle.InitializeAsync();

    // 应用程序主逻辑
    await RunApplicationLogicAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"应用程序启动失败: {ex.Message}");
    await lifecycle.ShutdownAsync(ApplicationExitReason.Exception, -1);
}
```

## 详细使用指南

### OSPlatformHelper 详细功能

#### 平台检测

```csharp
// 检测特定平台
if (OSPlatformHelper.IsWindows)
{
    Console.WriteLine("运行在 Windows 平台");
}
else if (OSPlatformHelper.IsLinux)
{
    Console.WriteLine("运行在 Linux 平台");
}
else if (OSPlatformHelper.IsMacOS)
{
    Console.WriteLine("运行在 macOS 平台");
}

// 检测 Unix 系统
if (OSPlatformHelper.OsIsUnix)
{
    Console.WriteLine("运行在 Unix 系统");
}
```

#### 权限检测

```csharp
// 检测管理员权限
if (OSPlatformHelper.IsElevated())
{
    Console.WriteLine("当前进程具有管理员权限");
}
else
{
    Console.WriteLine("当前进程为普通用户权限");
}
```

#### 环境变量操作

```csharp
// 获取单个环境变量
var path = OSPlatformHelper.GetEnvironmentVariable("PATH");
Console.WriteLine($"PATH: {path}");

// 获取所有环境变量
var allVars = OSPlatformHelper.GetAllEnvironmentVariables();
foreach (var (key, value) in allVars)
{
    Console.WriteLine($"{key} = {value}");
}
```

#### 版本检查

```csharp
// 检查操作系统版本
var minVersion = new Version(10, 0); // Windows 10
if (OSPlatformHelper.CheckOSVersion(minVersion))
{
    Console.WriteLine("操作系统版本满足要求");
}
```

### RuntimeMonitor 高级用法

#### 自定义监控

```csharp
// 创建自定义监控器
using var monitor = new RuntimeMonitor(
    monitorInterval: TimeSpan.FromSeconds(1), // 1秒监控间隔
    maxSnapshotCount: 1000 // 保持1000个快照
);

// 监控特定时间段
monitor.StartMonitoring();
await Task.Delay(TimeSpan.FromMinutes(10));
monitor.StopMonitoring();

// 获取指定时间范围的数据
var recentSnapshots = monitor.GetSnapshotsInTimeRange(TimeSpan.FromMinutes(5));
Console.WriteLine($"最近5分钟收集了 {recentSnapshots.Count} 个快照");
```

#### 性能趋势分析

```csharp
// 分析不同指标的趋势
var trends = new[]
{
    monitor.AnalyzeTrend(PerformanceCounterType.CpuUsage),
    monitor.AnalyzeTrend(PerformanceCounterType.MemoryUsage),
    monitor.AnalyzeTrend(PerformanceCounterType.ThreadCount),
    monitor.AnalyzeTrend(PerformanceCounterType.GCCollections)
};

foreach (var trend in trends)
{
    Console.WriteLine($"{trend.CounterType}:");
    Console.WriteLine($"  当前值: {trend.CurrentValue:F2}");
    Console.WriteLine($"  平均值: {trend.AverageValue:F2}");
    Console.WriteLine($"  最大值: {trend.MaxValue:F2}");
    Console.WriteLine($"  最小值: {trend.MinValue:F2}");
    Console.WriteLine($"  趋势: {(trend.Trend > 0 ? "上升" : "下降")} ({trend.Trend:F4})");
    Console.WriteLine($"  标准差: {trend.StandardDeviation:F2}");
    Console.WriteLine();
}
```

#### 内存管理

```csharp
// 获取内存压力信息
var memoryInfo = RuntimeMonitor.GetMemoryPressureInfo();
Console.WriteLine(memoryInfo);

// 强制垃圾回收
RuntimeMonitor.ForceGarbageCollection();

// 强制特定代的垃圾回收
RuntimeMonitor.ForceGarbageCollection(generation: 2, mode: GCCollectionMode.Forced);
```

### ApplicationLifecycleManager 高级场景

#### 复杂初始化流程

```csharp
var lifecycle = new ApplicationLifecycleManager();

// 按顺序添加初始化任务
lifecycle.AddInitializeTask(async () =>
{
    Console.WriteLine("第1步：检查系统要求");
    await CheckSystemRequirementsAsync();
});

lifecycle.AddInitializeTask(async () =>
{
    Console.WriteLine("第2步：加载核心配置");
    await LoadCoreConfigurationAsync();
});

lifecycle.AddInitializeTask(async () =>
{
    Console.WriteLine("第3步：初始化数据访问层");
    await InitializeDataAccessLayerAsync();
});

lifecycle.AddInitializeTask(async () =>
{
    Console.WriteLine("第4步：启动后台服务");
    await StartBackgroundServicesAsync();
});

lifecycle.AddInitializeTask(async () =>
{
    Console.WriteLine("第5步：预热缓存");
    await WarmupCacheAsync();
});
```

#### 优雅关闭处理

```csharp
// 按相反顺序添加关闭任务
lifecycle.AddShutdownTask(async () =>
{
    Console.WriteLine("清理缓存");
    await ClearCacheAsync();
});

lifecycle.AddShutdownTask(async () =>
{
    Console.WriteLine("停止后台服务");
    await StopBackgroundServicesAsync();
});

lifecycle.AddShutdownTask(async () =>
{
    Console.WriteLine("关闭数据库连接");
    await CloseDataConnectionsAsync();
});

lifecycle.AddShutdownTask(async () =>
{
    Console.WriteLine("保存配置更改");
    await SaveConfigurationChangesAsync();
});

lifecycle.AddShutdownTask(async () =>
{
    Console.WriteLine("清理临时文件");
    await CleanupTemporaryFilesAsync();
});
```

#### 自动故障恢复

```csharp
var lifecycle = new ApplicationLifecycleManager
{
    AutoRestartEnabled = true,
    MaxRestartAttempts = 5,
    RestartDelay = TimeSpan.FromSeconds(30)
};

lifecycle.ApplicationError += async (sender, args) =>
{
    Console.WriteLine($"应用程序发生错误: {args.Exception?.Message}");

    // 记录错误日志
    await LogErrorAsync(args.Exception);

    // 尝试自动恢复
    if (args.Exception is OutOfMemoryException)
    {
        Console.WriteLine("检测到内存不足，执行内存清理");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
};
```

## 集成示例

### Web 应用程序集成

```csharp
public class Program
{
    private static RuntimeMonitor? _monitor;
    private static ApplicationLifecycleManager? _lifecycle;

    public static async Task Main(string[] args)
    {
        // 创建生命周期管理器
        _lifecycle = new ApplicationLifecycleManager();
        _lifecycle.AutoRestartEnabled = true;

        // 创建性能监控器
        _monitor = new RuntimeMonitor(TimeSpan.FromSeconds(10));

        try
        {
            // 添加初始化任务
            _lifecycle.AddInitializeTask(async () =>
            {
                Console.WriteLine("启动性能监控");
                _monitor.StartMonitoring();
            });

            _lifecycle.AddInitializeTask(async () =>
            {
                Console.WriteLine("创建 Web 主机");
                await CreateWebHostAsync(args);
            });

            // 添加关闭任务
            _lifecycle.AddShutdownTask(async () =>
            {
                Console.WriteLine("停止性能监控");
                _monitor?.StopMonitoring();
            });

            // 初始化应用程序
            await _lifecycle.InitializeAsync();

            // 保持应用程序运行
            Console.WriteLine("应用程序正在运行，按 Ctrl+C 停止");
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用程序启动失败: {ex.Message}");
        }
        finally
        {
            _monitor?.Dispose();
            _lifecycle?.Dispose();
        }
    }

    private static async Task CreateWebHostAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 配置服务
        builder.Services.AddControllers();

        var app = builder.Build();

        // 配置管道
        app.UseRouting();
        app.MapControllers();

        // 添加运行时信息端点
        app.MapGet("/runtime", () =>
        {
            var runtimeInfo = OSPlatformHelper.GetRuntimeInfo();
            var performanceSnapshot = _monitor?.GetCurrentSnapshot();

            return new
            {
                Runtime = runtimeInfo,
                Performance = performanceSnapshot,
                Lifecycle = _lifecycle?.GetLifecycleSummary()
            };
        });

        await app.RunAsync();
    }
}
```

### 控制台应用程序集成

```csharp
public class ConsoleApplication
{
    private readonly ApplicationLifecycleManager _lifecycle;
    private readonly RuntimeMonitor _monitor;

    public ConsoleApplication()
    {
        _lifecycle = new ApplicationLifecycleManager();
        _monitor = new RuntimeMonitor();

        SetupLifecycle();
    }

    private void SetupLifecycle()
    {
        // 监听状态变化
        _lifecycle.StateChanged += OnStateChanged;
        _lifecycle.ApplicationExiting += OnApplicationExiting;

        // 配置初始化
        _lifecycle.AddInitializeTask(InitializeLoggingAsync);
        _lifecycle.AddInitializeTask(InitializeConfigurationAsync);
        _lifecycle.AddInitializeTask(InitializeServicesAsync);
        _lifecycle.AddInitializeTask(StartMonitoringAsync);

        // 配置关闭
        _lifecycle.AddShutdownTask(StopMonitoringAsync);
        _lifecycle.AddShutdownTask(CleanupServicesAsync);
        _lifecycle.AddShutdownTask(SaveStateAsync);
    }

    public async Task RunAsync()
    {
        try
        {
            await _lifecycle.InitializeAsync();

            Console.WriteLine("=== 应用程序已启动 ===");
            Console.WriteLine(OSPlatformHelper.GetPerformanceSummary());
            Console.WriteLine();

            // 主程序逻辑
            await RunMainLogicAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用程序运行失败: {ex.Message}");
            await _lifecycle.ShutdownAsync(ApplicationExitReason.Exception, -1);
        }
    }

    private async Task RunMainLogicAsync()
    {
        var cancellationToken = new CancellationTokenSource();

        // 启动后台任务
        var tasks = new[]
        {
            MonitorPerformanceAsync(cancellationToken.Token),
            ProcessDataAsync(cancellationToken.Token),
            HandleUserInputAsync(cancellationToken.Token)
        };

        await Task.WhenAny(tasks);
        cancellationToken.Cancel();
    }

    private async Task MonitorPerformanceAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var summary = _monitor.GetPerformanceSummary();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {summary}");

            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        }
    }

    // ... 其他方法实现
}
```

## 性能建议

### 监控频率优化

```csharp
// 根据应用程序类型选择合适的监控间隔
var monitor = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production"
    ? new RuntimeMonitor(TimeSpan.FromSeconds(30)) // 生产环境较长间隔
    : new RuntimeMonitor(TimeSpan.FromSeconds(5));  // 开发环境较短间隔
```

### 内存管理

```csharp
// 定期清理监控数据以避免内存泄漏
var monitor = new RuntimeMonitor(maxSnapshotCount: 100); // 限制快照数量

// 定期强制垃圾回收（谨慎使用）
if (DateTime.Now.Minute % 10 == 0) // 每10分钟
{
    RuntimeMonitor.ForceGarbageCollection();
}
```

## 故障排除

### 常见问题

1. **权限不足**: 某些系统信息需要管理员权限

   ```csharp
   if (!OSPlatformHelper.IsElevated())
   {
       Console.WriteLine("警告：当前权限不足，某些功能可能无法使用");
   }
   ```

2. **平台兼容性**: 某些功能在特定平台上可能不可用

   ```csharp
   try
   {
       var info = OSPlatformHelper.GetRuntimeInfo();
   }
   catch (PlatformNotSupportedException ex)
   {
       Console.WriteLine($"平台不支持: {ex.Message}");
   }
   ```

3. **监控性能影响**: 过于频繁的监控可能影响性能
   ```csharp
   // 使用合适的监控间隔
   var monitor = new RuntimeMonitor(TimeSpan.FromSeconds(10)); // 不要太频繁
   ```

### 调试技巧

```csharp
// 启用详细日志
var lifecycle = new ApplicationLifecycleManager();
lifecycle.StateChanged += (sender, args) =>
{
    Debug.WriteLine($"状态变更: {args.PreviousState} -> {args.State}: {args.Message}");
};

// 导出诊断信息
var diagnostics = new
{
    Platform = OSPlatformHelper.GetRuntimeInfo(),
    Performance = monitor.GetCurrentSnapshot(),
    Lifecycle = lifecycle.GetLifecycleSummary()
};

var json = JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions
{
    WriteIndented = true
});

await File.WriteAllTextAsync("diagnostics.json", json);
```

## 最佳实践

1. **及早初始化**: 在应用程序启动早期创建生命周期管理器
2. **优雅关闭**: 始终使用生命周期管理器的关闭功能
3. **监控适度**: 根据需要选择合适的监控频率
4. **异常处理**: 妥善处理平台特定的异常
5. **资源清理**: 确保正确释放监控器和生命周期管理器

## 扩展开发

如果需要扩展 Runtime 模块功能，可以：

1. 继承现有类添加自定义功能
2. 实现自定义监控指标
3. 添加平台特定的功能支持
4. 集成外部监控系统

---

## 更新日志

### v2.0.0

- ✨ 全面重构架构，提升性能和可扩展性
- 🆕 添加 RuntimeMonitor 性能监控功能
- 🆕 添加 ApplicationLifecycleManager 生命周期管理
- 🔧 优化跨平台兼容性
- 📚 完善文档和示例

### v1.0.0

- 🎉 初始版本发布
- ✅ 基础操作系统信息获取功能
