# XiHan.Framework.Observability.Host

运行信息（Ring 3，`role: default`，RFC-0023）。提供 `observability.host@1`：单例 `IHostMonitor` 读进程与所在主机的 CPU、内存、磁盘、网卡、GPU、主板与运行时长，供管理界面的服务监控使用。

```csharp
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<HostObservabilityFeature>());
```

```json
{ "XiHan": { "Observability": { "Host": { "SampleIntervalSeconds": 5 } } } }
```

## 规则

- 不启动外部命令：只读 BCL、`/proc` 与 `/sys`、`pci.ids`、注册表与 kernel32（`GetSystemTimes`、`GetLogicalProcessorInformation`、`K32GetPerformanceInfo`），不用 WMI、PowerShell、`dmidecode`、`lspci`、`nvidia-smi`、`top`。
- 读不到的项为 `null`；单项失败不影响其他项，读取不抛出。
- CPU 型号与核心、GPU、主板首次读取后缓存；内存、磁盘、网卡、运行时每次当场读。
- 使用率与速率由后台采样器求两次采样之差：启动时同步采一次，之后每 `SampleIntervalSeconds`（默认 5，取值 (0, 3600]）采一次；读取只返回最近的值，不睡眠、不等待，启动后第一个间隔内为 `null`。
- 平台：Linux 与 Windows 读全部项（各自的读取路径见 RFC-0023）；macOS 等只有 BCL 读得到的部分，CPU 型号与使用率、内存、GPU、主板为 `null`。
- 与 4.x `HardwareInfos` 的已知差异（NVIDIA 显存与温度、GPU 使用率与显示模式、Windows 主板序列号等）列在 RFC-0023。

## 测试

Linux 的读取路径以根目录为参数，夹具用例在任何系统上运行；真实机器上的用例分 Windows 与 Linux，只在对应系统上运行。

## 依赖方向

依赖 `XiHan.Framework.Kernel` 与 `XiHan.Framework.Observability.Abstractions`，没有第三方包。

## 状态

P5：可用。
