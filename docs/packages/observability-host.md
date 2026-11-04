# XiHan.Framework.Observability.Host

运行信息的默认实现：单例 `IHostMonitor` 读进程与所在主机的 CPU、内存、磁盘、网卡、GPU、主板与运行时长，后台采样 CPU 使用率与网卡速率。提供 `observability.host@1`，供管理界面的服务监控使用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Observability.Host` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `observability.host@1`（`role: default`）；依赖 `kernel.hosting@1` |
| 特性入口 | `HostObservabilityFeature`（清单 `xihan.observability.host`，`IHostedFeature`） |
| 配置节 | `XiHan:Observability:Host` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Observability.Abstractions`；`Microsoft.Extensions.Logging.Abstractions` 10.0.13；没有其他第三方包 |
| AOT | 兼容（系统调用经 `LibraryImport` 源生成） |
| 稳定性 | Preview |

## 概述

特性被选中时注册三个单例：按当前操作系统创建的平台读取器、后台采样器与 `IHostMonitor`。`StartAsync` 同步采第一次样再起采样循环，`StopAsync` 结束循环，最近算出的值保留（RFC-0023，ADR-0056）。

读取分两类。静态项（CPU 型号与核心、GPU、主板）首次读取后缓存；动态项（内存、磁盘、网卡、运行时）每次当场读。使用率与速率要两次采样求差，由采样器按间隔计算：

| 量 | 算法 |
| --- | --- |
| 系统 CPU 使用率 | 1 − 空闲增量 / 总增量 |
| 进程 CPU 使用率 | 进程处理器时间增量 /（经过时间 × 逻辑核心数） |
| 网卡收发速率 | 累计字节增量 / 经过时间 |

计数变小（回绕、网卡重建）或总增量为零时该项为空；百分比限制在 0 到 100，不取整。一次采样失败记一条警告，保留上一次的值，循环继续。

## 何时使用

管理界面要显示服务器状态、运维脚本要按阈值告警时。指标与链路的导出用 [Observability.OpenTelemetry](./observability-opentelemetry)，两者互不依赖。`Profiles.Full` 已装配本包；不需要运行信息又想省下后台采样时，改用 `Profiles.Standard` 自行组合。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Observability.Host" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Observability.Host;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HostObservabilityFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Observability:Host:SampleIntervalSeconds` | 数值，(0, 3600] | 5 | 采样间隔（秒），CPU 使用率与网卡速率是相邻两次采样之差 |

```json
{ "XiHan": { "Observability": { "Host": { "SampleIntervalSeconds": 5 } } } }
```

取值越界或不是数值时 `HostObservabilityOptions.FromConfiguration` 抛出 `ArgumentException`；上限是 `HostObservabilityOptions.MaxSampleInterval`（1 小时）。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `HostObservabilityFeature` | 能力包入口；启动时采第一次样并起采样循环 |
| `HostObservabilityOptions` | `SampleInterval`（缺省 5 秒）；`ConfigurationSection` 为 `XiHan:Observability:Host` |
| `IHostMonitor`（抽象包） | 七个读取方法，见 [Observability.Abstractions](./observability-abstractions#主要类型) |

## 使用示例

服务监控页的端点见[可观测性](../guide/observability#运行信息)。在测试里等第一次使用率出来再断言：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Observability.Abstractions;
using XiHan.Framework.Observability.Host;
using XiHan.Framework.Testing;

public sealed class MonitorTests
{
    [Fact]
    public async Task CpuUsage_AppearsAfterOneInterval()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<HostObservabilityFeature>(),
            [new("XiHan:Observability:Host:SampleIntervalSeconds", "0.1")]);
        var monitor = host.Services.GetRequiredService<IHostMonitor>();

        // 只有 Linux 与 Windows 读得到系统 CPU 时间
        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (monitor.ReadProcessor().UsagePercent is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
            }

            Assert.InRange(monitor.ReadProcessor().UsagePercent!.Value, 0d, 100d);
        }
    }
}
```

## 行为与限制

- 不启动外部命令：只读 BCL、`/proc` 与 `/sys`、`pci.ids`、注册表与 kernel32 的 `GetSystemTimes`、`GetLogicalProcessorInformation`、`K32GetPerformanceInfo`；不用 WMI、PowerShell、`dmidecode`、`lspci`、`nvidia-smi`、`top`。
- 读不到的项为 `null`，列表读不到为空列表；字符串去掉首尾空白后为空也视为读不到。单项失败（权限不足、文件缺失、平台不支持）不影响其他项，读取不抛出、不睡眠、不等待采样。
- 不暴露命令行参数、环境变量与当前目录。

各平台的读取来源：

| 项 | Linux | Windows | 其他（macOS 等） |
| --- | --- | --- | --- |
| CPU 型号、厂商 | `/proc/cpuinfo` 的 `model name`（没有时取 `Hardware`）、`vendor_id` | 注册表 `HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0` 的 `ProcessorNameString`、`VendorIdentifier` | 空 |
| 物理核心、最后一级缓存 | `/sys/devices/system/cpu/cpu<N>/topology/` 与 `cpu0/cache/index<N>/` | `GetLogicalProcessorInformation` | 空 |
| 标称频率 | `cpu0/cpufreq/base_frequency`，没有时取 `cpuinfo_max_freq` | 注册表 `~MHz` | 空 |
| CPU 使用率 | `/proc/stat` 首行 | `GetSystemTimes` | 空 |
| 内存 | `/proc/meminfo` 的 `MemTotal`、`MemAvailable`、`MemFree`、`Buffers` + `Cached` | `K32GetPerformanceInfo`（空闲为空） | 空 |
| 系统运行时长 | `/proc/uptime` | `Environment.TickCount64` | `Environment.TickCount64` |
| 磁盘 | `/proc/self/mounts` 里以 `/dev/` 开头、不是 `/dev/loop` 的设备，每个设备取第一个挂载点，容量取 `DriveInfo` | `DriveInfo` 里就绪的固定、可移动与网络盘 | 同 Windows |
| 网卡 | `NetworkInterface`，平台不支持的属性为空 | 同左 | 同左 |
| GPU | `/sys/class/drm/card<N>/device`，名称与厂商名查 `pci.ids`（`/usr/share/hwdata`、`/usr/share/misc`、`/usr/share`） | 注册表显示适配器类 `{4d36e968-e325-11ce-bfc1-08002be10318}` 下 `MatchingDeviceId` 以 `pci` 开头的子键 | 空列表 |
| 主板 | `/sys/class/dmi/id/` 的 `board_*`、`sys_vendor`、`product_name`、`bios_*` | 注册表 `HKLM\HARDWARE\DESCRIPTION\System\BIOS`（没有序列号） | 空 |

已知的取舍：没有 NVIDIA 专有驱动的显存与温度、GPU 与显存使用率；Windows 没有 GPU 温度与主板序列号、没有空闲内存；Linux 的 `board_serial` 只有 root 读得到；容器里的内存与 CPU 使用率是宿主机的数值，不是 cgroup 限额，逻辑核心数是 .NET 看到的可用核心数。完整的 4.x 差异列在 RFC-0023。

## 相关

- [可观测性](../guide/observability#运行信息)
- [Observability.Abstractions](./observability-abstractions)
- [Observability.OpenTelemetry](./observability-opentelemetry)
- [Profiles.Full](./profiles-full)
- 规范：RFC-0023《可观测性：导出与运行信息》
