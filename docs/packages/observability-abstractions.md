# XiHan.Framework.Observability.Abstractions

可观测性的抽象包：两项能力的 ID（导出与运行信息），以及进程与主机运行信息的契约 `IHostMonitor` 与七份记录。实现见 [Observability.Host](./observability-host) 与 [Observability.OpenTelemetry](./observability-opentelemetry)。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Observability.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `observability.export@1` 与 `observability.host@1`（`ObservabilityCapabilities`），本包不提供 |
| 特性入口 | — |
| 配置节 | — |
| 依赖 | `XiHan.Framework.Primitives`；无第三方包 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

内核的追踪与指标直接基于 `System.Diagnostics`，不需要抽象；本包只承载两件事：能力 ID 常量，供两个实现包与应用清单共用；运行信息的契约，供管理界面的服务监控读取（RFC-0023，ADR-0056）。

`IHostMonitor` 的七个方法各返回一份不可变记录。契约约定读取不阻塞、不抛出、不启动外部命令；读不到的项为 `null`，不以 0 或空串顶替；使用率与速率取后台采样器最近一次的值，启动后第一个采样间隔内为空；CPU 型号与核心、GPU、主板首次读取后缓存。

## 何时使用

应用代码依赖 `IHostMonitor` 时只引用本包，装配时再选实现（[Observability.Host](./observability-host)）。在清单里按能力 ID 声明依赖时引用 `ObservabilityCapabilities`。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Observability.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。两个实现包都依赖本包，`Profiles.Full` 已包含。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `ObservabilityCapabilities` | `Export`（`observability.export`）、`Host`（`observability.host`）、`Version`（1） |
| `IHostMonitor` | `ReadRuntime`、`ReadProcessor`、`ReadMemory`、`ReadDisks`、`ReadNetworkInterfaces`、`ReadGpus`、`ReadBoard` |
| `RuntimeInfo` | `OsDescription`、`OsPlatform`（`windows` / `linux` / `macos` / `freebsd` / `other`）、`OsVersion`、`OsArchitecture`、`Framework`、`RuntimeIdentifier`、`ProcessArchitecture`、`MachineName`、`UserName`、`Interactive`、`ProcessId`、`ProcessName`、`ProcessStartedAt`、`ProcessUptime`、`SystemStartedAt`、`SystemUptime`、`WorkingSetBytes`、`PrivateBytes`、`ManagedHeapBytes`、`ThreadCount`、`HandleCount`、`Gen0Collections` / `Gen1Collections` / `Gen2Collections`、`ProcessCpuUsagePercent` |
| `ProcessorInfo` | `Name`、`Vendor`、`Architecture`、`PhysicalCores`、`LogicalCores`、`FrequencyMhz`、`CacheBytes`、`UsagePercent` |
| `MemoryInfo` | `TotalBytes`、`AvailableBytes`、`FreeBytes`、`CachedBytes`、`UsedBytes`、`UsagePercent` |
| `DiskInfo` | `Name`、`Device`、`Label`、`Format`、`Type`、`TotalBytes`、`FreeBytes`、`AvailableBytes`、`UsedBytes`、`UsagePercent` |
| `NetworkInterfaceInfo` | `Id`、`Name`、`Description`、`Type`、`Status`、`SpeedBitsPerSecond`、`PhysicalAddress`、`IPv4Addresses`、`IPv6Addresses`、`Gateways`、`DnsServers`、`BytesReceived` / `BytesSent`、`PacketsReceived` / `PacketsSent`、`ReceiveBytesPerSecond` / `SendBytesPerSecond` |
| `GpuInfo` | `Name`、`Vendor`、`VendorId`、`DeviceId`、`Driver`、`DriverVersion`、`MemoryBytes`、`TemperatureCelsius` |
| `BoardInfo` | `Manufacturer`、`Product`、`Version`、`SerialNumber`、`SystemManufacturer`、`SystemProduct`、`BiosVendor`、`BiosVersion`、`BiosDate` |

## 使用示例

在后台作业或服务里读取并判断阈值，空值表示读不到，不当作 0：

```csharp
using XiHan.Framework.Observability.Abstractions;

namespace MyApp.Diagnostics;

public sealed class CapacityCheck(IHostMonitor monitor)
{
    public IReadOnlyList<string> FindAlerts()
    {
        var alerts = new List<string>();
        if (monitor.ReadMemory().UsagePercent is > 90)
        {
            alerts.Add("memory");
        }

        // 启动后第一个采样间隔内 UsagePercent 为 null，这里自然跳过
        if (monitor.ReadProcessor().UsagePercent is > 95)
        {
            alerts.Add("cpu");
        }

        foreach (var disk in monitor.ReadDisks())
        {
            if (disk.UsagePercent is > 85)
            {
                alerts.Add($"disk:{disk.Name}");
            }
        }

        return alerts;
    }
}
```

## 行为与限制

- 契约只规定读取的语义；平台读取路径、采样与缓存由实现决定，内置实现的规则见 [Observability.Host](./observability-host#行为与限制)。
- 百分比在 0 到 100 之间、不取整；收发速率 `ReceiveBytesPerSecond` / `SendBytesPerSecond` 的单位是字节每秒，网卡链路速率 `SpeedBitsPerSecond` 的单位是 bit/s。
- 记录不包含命令行参数、环境变量与当前目录。

## 相关

- [可观测性](../guide/observability#运行信息)
- [Observability.Host](./observability-host)
- [Observability.OpenTelemetry](./observability-opentelemetry)
- 规范：RFC-0023《可观测性：导出与运行信息》
