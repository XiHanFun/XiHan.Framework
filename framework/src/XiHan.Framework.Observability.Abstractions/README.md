# XiHan.Framework.Observability.Abstractions

## 关于

`XiHan.Framework.Observability.Abstractions` 定义可观测性的契约：`IHostMonitor` 读取进程与所在主机的运行信息（CPU、内存、磁盘、网卡、GPU、主板、运行时长），以及对应的记录类型。不包含实现，一般由实现包带入，业务代码或写自定义监控实现时才直接引用。

## 主要特性

* 读不到的项为 `null`，不以 0 或空串顶替，界面可区分「没有」与「为零」
* 读取不阻塞、不抛出、不启动外部命令
* 使用率与速率取后台采样器最近一次的值，启动后第一个采样间隔内为 `null`

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Observability.Abstractions --prerelease
```

### 使用

注入 `IHostMonitor` 读取运行信息，通常配合 `Observability.Host` 提供的默认实现使用。

```csharp
using XiHan.Framework.Observability.Abstractions;

public sealed class CapacityCheck(IHostMonitor monitor)
{
    public bool MemoryTooHigh() => monitor.ReadMemory().UsagePercent is > 90;
}
```

## 主要类型

此库提供的主要类型：

* `IHostMonitor`：`ReadRuntime`、`ReadProcessor`、`ReadMemory`、`ReadDisks`、`ReadNetworkInterfaces`、`ReadGpus`、`ReadBoard`
* `RuntimeInfo`：操作系统、.NET、进程与系统运行时长、内存与 GC、CPU 使用率
* `ProcessorInfo`、`MemoryInfo`、`DiskInfo`：处理器、内存、磁盘信息
* `NetworkInterfaceInfo`、`GpuInfo`、`BoardInfo`：网卡、显卡、主板信息

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/observability-abstractions)

## 相关包

* 运行信息默认实现：[XiHan.Framework.Observability.Host](https://www.nuget.org/packages/XiHan.Framework.Observability.Host)
* 指标与链路导出：[XiHan.Framework.Observability.OpenTelemetry](https://www.nuget.org/packages/XiHan.Framework.Observability.OpenTelemetry)

## 反馈与贡献

`XiHan.Framework.Observability.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
