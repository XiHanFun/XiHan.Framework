# 硬件信息模块 (HardwareInfos)

本模块提供了全面的系统硬件信息获取功能，支持跨平台（Windows、Linux、macOS），具备缓存机制、异步操作和错误处理等企业级特性。

## ✨ 主要特性

- 🔧 **全面的硬件支持**: CPU、内存、磁盘、网络、GPU、主板信息
- 🌍 **跨平台兼容**: 支持 Windows、Linux、macOS（自动处理平台差异）
- ⚡ **高性能**: 内置智能缓存机制，避免重复查询
- 🔄 **异步支持**: 提供异步 API，适合高并发场景
- 🛡️ **健壮性**: 完善的错误处理和平台兼容性检查
- 📊 **诊断功能**: 硬件状态监控和问题诊断
- 📤 **数据导出**: 支持 JSON 格式导出硬件信息
- 🔒 **类型安全**: 基于强类型接口设计，编译时类型检查

## 🏗️ 架构设计

### 核心接口

```csharp
// 硬件信息基础接口
public interface IHardwareInfo
{
    DateTime Timestamp { get; }
    bool IsAvailable { get; }
    string? ErrorMessage { get; }
}

// 硬件信息提供者接口
public interface IHardwareInfoProvider<T>
{
    T GetInfo();
    Task<T> GetInfoAsync();
    T GetCachedInfo(bool forceRefresh = false);
    Task<T> GetCachedInfoAsync(bool forceRefresh = false);
}
```

### 继承体系

```
BaseHardwareInfoProvider<T>
├── CpuInfoProvider
├── RamInfoProvider
├── NetworkInfoProvider
└── GpuInfoProvider
```

## 🚀 快速开始

### 1. 基础使用

```csharp
// 获取CPU信息
var cpuInfo = CpuHelper.GetCpuInfos();
Console.WriteLine($"CPU: {cpuInfo.ProcessorName}");
Console.WriteLine($"核心数: {cpuInfo.PhysicalCoreCount}C/{cpuInfo.LogicalCoreCount}T");
Console.WriteLine($"使用率: {cpuInfo.UsagePercentage:F1}%");

// 获取内存信息
var ramInfo = RamHelper.GetRamInfos();
Console.WriteLine($"总内存: {ramInfo.TotalSpace}");
Console.WriteLine($"已用: {ramInfo.UsedSpace} ({ramInfo.UsagePercentage:F1}%)");

// 获取网络信息
var networkInfos = NetworkHelper.GetNetworkInfos();
foreach (var network in networkInfos.Where(n => n.IsAvailable))
{
    Console.WriteLine($"网卡: {network.Name} - {network.OperationalStatus}");
}
```

### 2. 异步操作

```csharp
// 异步获取硬件信息
var cpuTask = CpuHelper.GetCpuInfosAsync();
var ramTask = RamHelper.GetRamInfosAsync();
var networkTask = NetworkHelper.GetNetworkInfosAsync();

await Task.WhenAll(cpuTask, ramTask, networkTask);

var cpuInfo = await cpuTask;
var ramInfo = await ramTask;
var networkInfos = await networkTask;
```

### 3. 统一管理器使用

```csharp
// 获取完整系统信息
var systemInfo = HardwareInfoManager.GetSystemHardwareInfo();

// 异步获取
var systemInfoAsync = await HardwareInfoManager.GetSystemHardwareInfoAsync();

// 获取摘要信息
var summary = HardwareInfoManager.GetSystemHardwareSummary();
Console.WriteLine($"CPU: {summary.CpuName} ({summary.CpuCores})");
Console.WriteLine($"内存: {summary.TotalMemory} (使用率: {summary.MemoryUsage})");

// 硬件诊断
var diagnostic = HardwareInfoManager.GetDiagnosticReport();
if (diagnostic.Status == "发现问题")
{
    foreach (var issue in diagnostic.Issues)
    {
        Console.WriteLine($"⚠️ {issue}");
    }
}
```

### 4. 缓存控制

```csharp
// 使用缓存（默认）
var cachedCpuInfo = CpuHelper.GetCachedCpuInfos();

// 强制刷新缓存
var freshCpuInfo = CpuHelper.GetCachedCpuInfos(forceRefresh: true);

// 清除所有缓存
HardwareInfoManager.ClearAllCache();
```

### 5. 数据导出

```csharp
// 导出为JSON
var exportPath = "hardware_info.json";
var success = await HardwareInfoManager.ExportHardwareInfoToJsonAsync(exportPath);
if (success)
{
    Console.WriteLine($"硬件信息已导出到: {exportPath}");
}
```

## 📊 详细功能

### CPU 信息 (CpuHelper)

获取处理器详细信息：

```csharp
var cpuInfo = CpuHelper.GetCpuInfos();
// 可获取:
// - 处理器名称和架构
// - 物理/逻辑核心数
// - 基础时钟频率
// - 缓存大小
// - 实时使用率
// - 温度（Linux支持）
```

### 内存信息 (RamHelper)

获取内存使用详情：

```csharp
var ramInfo = RamHelper.GetRamInfos();
// 可获取:
// - 总内存/已用/空闲/可用内存（字节级精度）
// - 缓冲区和缓存信息
// - 使用率和可用率
// - 格式化的容量显示
```

### 网络信息 (NetworkHelper)

获取网络接口详情：

```csharp
var networkInfos = NetworkHelper.GetNetworkInfos();
// 可获取:
// - 所有网络接口信息（包括状态）
// - IPv4/IPv6地址详情（地址、子网掩码、前缀长度）
// - 网络统计信息（流量、错误包统计）
// - DNS/网关/DHCP服务器（平台兼容性处理）
// - 接口属性（MAC地址、速度、多播支持等）

// 获取网络统计汇总
var networkStats = NetworkHelper.GetNetworkStatistics();
Console.WriteLine($"总接收: {networkStats.TotalBytesReceived} 字节");
Console.WriteLine($"总发送: {networkStats.TotalBytesSent} 字节");
```

### GPU 信息 (GpuHelper)

获取显卡信息：

```csharp
var gpuInfos = GpuHelper.GetGpuInfos();
// 可获取:
// - GPU名称和厂商
// - 显存大小
// - 驱动版本
// - 温度（NVIDIA支持）
// - 使用率（部分支持）
```

### 磁盘信息 (DiskHelper)

获取存储设备信息：

```csharp
var diskInfos = DiskHelper.GetDiskInfos();
// 可获取:
// - 所有驱动器信息
// - 总容量/已用/可用空间
// - 磁盘类型
// - 可用率
```

## ⚙️ 配置和优化

### 缓存配置

不同硬件信息的缓存时间：

- CPU 信息: 30 秒（变化快）
- 内存信息: 10 秒（变化快）
- 网络信息: 2 分钟（相对稳定）
- GPU 信息: 5 分钟（较稳定）
- 磁盘信息: 无缓存（实时）

### 性能优化建议

1. **使用缓存 API**: 优先使用 `GetCached*` 方法
2. **异步操作**: 在 UI 线程中使用异步版本
3. **批量获取**: 使用 `HardwareInfoManager` 批量获取
4. **定期清理**: 在长期运行的应用中定期清理缓存

### 错误处理

```csharp
var cpuInfo = CpuHelper.GetCpuInfos();
if (!cpuInfo.IsAvailable)
{
    Console.WriteLine($"获取CPU信息失败: {cpuInfo.ErrorMessage}");
    // 处理错误情况
}

// 网络信息的平台兼容性处理
var networkInfos = NetworkHelper.GetNetworkInfos();
foreach (var network in networkInfos)
{
    if (network.IsAvailable)
    {
        Console.WriteLine($"网卡: {network.Name}");
        // 某些属性在特定平台上可能不可用
        if (network.DhcpServerAddresses.Any())
        {
            Console.WriteLine($"DHCP服务器: {string.Join(", ", network.DhcpServerAddresses)}");
        }
    }
    else
    {
        Console.WriteLine($"网卡错误: {network.ErrorMessage}");
    }
}
```

## 🔧 扩展开发

### 添加新的硬件信息类型

1. 创建信息模型（实现 `IHardwareInfo`）
2. 创建提供者（继承 `BaseHardwareInfoProvider<T>`）
3. 创建静态助手类
4. 在 `HardwareInfoManager` 中集成

### 自定义缓存策略

```csharp
internal class CustomInfoProvider : BaseHardwareInfoProvider<CustomInfo>
{
    protected override TimeSpan CacheExpiry => TimeSpan.FromMinutes(10);

    protected override CustomInfo GetInfoCore()
    {
        // 实现具体获取逻辑
    }
}
```

## 🛠️ 依赖项

### 基础依赖

- .NET 6.0+
- System.Runtime.InteropServices（跨平台支持）
- System.Net.NetworkInformation（网络信息）
- System.Net.Sockets（网络套接字）
- System.Text.RegularExpressions（文本解析）
- System.Text.Json（用于导出功能）

### 框架内部依赖

- XiHan.Framework.Utils.CommandLine（命令行执行）
- XiHan.Framework.Utils.Logging（日志记录）
- XiHan.Framework.Utils.System（系统工具）
- XiHan.Framework.Utils.IO（文件处理）

## 📝 版本历史

### v2.0.0 (当前版本)

**架构优化**

- ✅ 重构为基于接口的架构设计
- ✅ 实现 `BaseHardwareInfoProvider<T>` 基础抽象类
- ✅ 添加 `IHardwareInfo` 和 `IHardwareInfoProvider<T>` 接口

**功能增强**

- ✅ 添加智能缓存机制和异步支持
- ✅ 新增 GPU 信息获取（支持 Windows、Linux、macOS）
- ✅ 添加硬件诊断功能和状态监控
- ✅ 支持 JSON 格式数据导出
- ✅ 新增网络统计信息和流量监控

**跨平台兼容性**

- ✅ 完善 macOS 平台兼容性处理
- ✅ 解决 DHCP 服务器地址在 macOS 上的兼容性问题
- ✅ 修复网络统计信息的平台差异
- ✅ 优化 Linux/Unix 系统下的信息获取

**稳定性提升**

- ✅ 完善错误处理机制和异常捕获
- ✅ 添加平台特定功能的检查和降级处理
- ✅ 优化内存使用和性能表现
- ✅ 修复编译警告和类型安全问题

### v1.x (原版本)

- 基础硬件信息获取功能
- 简单的跨平台支持

## ⚠️ 已知问题与解决方案

### 平台兼容性问题

1. **macOS 平台限制**

   - DHCP 服务器地址获取不受支持，自动跳过
   - 某些网络统计信息可能不可用
   - 解决方案：已添加平台检查，自动降级处理

2. **Linux 权限问题**

   - 温度信息需要访问 `/sys/class/thermal/` 目录
   - 某些硬件信息需要 root 权限
   - 解决方案：优雅降级，记录错误信息但不影响其他功能

3. **Windows WMI 依赖**
   - CPU 使用率依赖 WMI 服务
   - 解决方案：添加异常处理，WMI 失败时返回默认值

### 性能考虑

- 首次获取硬件信息可能较慢（特别是 GPU 信息）
- 建议在应用启动时预热缓存
- 长时间运行的应用建议定期清理缓存

## 🤝 贡献指南

欢迎提交 PR 来改进此模块！主要贡献方向：

1. **硬件支持扩展**

   - 支持更多硬件类型（声卡、蓝牙设备等）
   - 增强现有硬件信息的详细程度

2. **跨平台优化**

   - 改进 ARM 架构支持
   - 优化移动平台兼容性
   - 增强 Linux 发行版兼容性

3. **性能与稳定性**

   - 优化缓存策略
   - 减少系统调用开销
   - 提升错误恢复能力

4. **文档与示例**
   - 完善 API 文档
   - 添加更多使用示例
   - 翻译多语言文档

## 📄 许可证

MIT License - 详见项目根目录 LICENSE 文件
