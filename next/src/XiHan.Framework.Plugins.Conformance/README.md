# XiHan.Framework.Plugins.Conformance

进程外插件协议 `xihan.plugin.v1` 的黑盒一致性套件（RFC-0017，`spec/plugin/invocation.md` §7）。任何语言写的插件都以其构建产物目录继承 `PluginProtocolConformance` 运行，套件以宿主身份启动进程并核对：

1. 握手行合法、协商出宿主支持的版本、原语层版本相同、清单与目录里的文件一致、操作与订阅在清单声明范围内。
2. 没有共同协议版本时以 `FailedPrecondition` 拒绝。
3. `Configure` 与 `Start` 成功；`Health` 与 `WatchHealth` 报可服务状态且健康项都已在清单声明；`Stop` 成功后进程在宽限内退出。
4. 未声明的操作以 gRPC `Unimplemented` 结束。
5. 每个声明的一元操作都返回带 `status` 的应答：成功时载荷是 JSON，失败时错误码合法。
6. 向声明的订阅投递消息得到确认。
7. stdin 关闭后进程自行退出。

```csharp
public sealed class EchoPluginConformanceTests : PluginProtocolConformance
{
    protected override string PluginDirectory => "<EchoPlugin 的输出目录>";
    protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string> { "echoes.crash" };
    protected override IReadOnlyDictionary<string, string> SampleRequests => new Dictionary<string, string> { ["echoes.create"] = """{"text":"conformance"}""" };
}
```

可覆写 `Timeout`、`ConfigurationJson`、`SampleRequests`（没有示例的操作以 `{}` 调用）与 `ExcludedOperations`（故意崩溃或依赖宿主回调的操作）。套件不提供宿主回调服务，`Host.Invoke` / `Host.Publish` 由 `Plugins.OutOfProcess` 的测试覆盖。

## 依赖方向

依赖 `XiHan.Framework.Plugins.Protocol`（协议桩、握手核对）、`XiHan.Framework.Plugins.OutOfProcess`（进程启动）与 xunit 的断言与扩展核心包。

## 状态

P4-04：可用；`samples/EchoPlugin` 通过全部用例。
