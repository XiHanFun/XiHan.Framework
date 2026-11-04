# XiHan.Framework.Observability.Host

## 关于

`XiHan.Framework.Observability.Host` 运行信息的默认实现：提供单例 `IHostMonitor`，读取进程与所在主机的 CPU、内存、磁盘、网卡、GPU、主板与运行时长，供管理界面的服务监控使用。

## 主要特性

* Linux 与 Windows 读全部项，其他系统读 BCL 能读到的部分
* 不启动任何外部命令，单项读取失败不影响其他项
* 后台采样 CPU 使用率与网卡速率，间隔可配置

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Observability.Host --prerelease
```

### 使用

注册特性后即可注入 `IHostMonitor`。

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HostObservabilityFeature>());
```

可选配置采样间隔（秒，缺省 5，取值 (0, 3600]）：

```json
{ "XiHan": { "Observability": { "Host": { "SampleIntervalSeconds": 5 } } } }
```

## 主要类型

此库提供的主要类型：

* `HostObservabilityFeature`：能力包入口，启动时采第一次样并起后台采样
* `HostObservabilityOptions`：采样间隔选项
* `IHostMonitor`：读取接口，来自 `Observability.Abstractions`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/observability-host)

## 相关包

* 抽象与记录类型：[XiHan.Framework.Observability.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Observability.Abstractions)
* 指标与链路导出：[XiHan.Framework.Observability.OpenTelemetry](https://www.nuget.org/packages/XiHan.Framework.Observability.OpenTelemetry)

## 反馈与贡献

`XiHan.Framework.Observability.Host` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
