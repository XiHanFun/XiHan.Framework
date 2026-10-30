# XiHan.Framework.Observability.Abstractions

可观测性契约（Ring 2，RFC-0023）。

| 契约 | 用途 |
| --- | --- |
| `ObservabilityCapabilities` | 能力 ID：`observability.export`（导出器）与 `observability.host`（运行信息），接口版本 1 |
| `IHostMonitor` | 进程与所在主机的运行信息：`ReadRuntime` / `ReadProcessor` / `ReadMemory` / `ReadDisks` / `ReadNetworkInterfaces` / `ReadGpus` / `ReadBoard` |
| `RuntimeInfo` | 操作系统、.NET、机器名与用户名、进程 ID / 名称 / 启动时刻 / 运行时长、系统启动时刻 / 运行时长、工作集 / 私有字节 / 托管堆、线程数 / 句柄数、三代 GC 次数、进程 CPU 使用率 |
| `ProcessorInfo` | 型号、厂商、架构、物理 / 逻辑核心、标称频率（MHz）、最后一级缓存、系统 CPU 使用率 |
| `MemoryInfo` | 总量、可用、空闲、缓存，算出已用与使用率 |
| `DiskInfo` | 挂载点或盘符、设备、卷标、文件系统、类型、总量 / 空闲 / 当前用户可用，算出已用与使用率 |
| `NetworkInterfaceInfo` | ID、名称、描述、类型、状态、速率、物理地址、IPv4 / IPv6 地址、网关、DNS、累计收发字节与包数、收发速率 |
| `GpuInfo` | 名称、厂商、PCI 厂商 / 设备 ID、驱动与版本、显存、温度 |
| `BoardInfo` | 主板厂商 / 型号 / 版本 / 序列号、整机厂商 / 型号、BIOS 厂商 / 版本 / 日期 |

```csharp
var monitor = services.GetRequiredService<IHostMonitor>();
var cpu = monitor.ReadProcessor();   // cpu.UsagePercent 在启动后第一个采样间隔内为 null
```

读不到的项为 `null`，不以 0 或空串顶替，界面据此区分「没有」与「为零」。读取不阻塞、不抛出、不启动外部命令。

## 依赖方向

只依赖 `XiHan.Framework.Primitives`。实现见 `Observability.Host`。

## 状态

P5：可用。
