# XiHan.Framework.Plugins.Conformance

插件协议 `xihan.plugin.v1` 的黑盒一致性套件：进程外载体继承 `PluginProtocolConformance`（RFC-0017，`spec/plugin/invocation.md` §7），Wasm 载体继承 `WasmPluginConformance`（RFC-0018，`spec/plugin/wasm.md` §7）。

## 进程外载体

任何语言写的插件都以其构建产物目录继承 `PluginProtocolConformance` 运行，套件以宿主身份启动进程并核对：

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

## Wasm 载体

以含清单与模块文件的目录继承 `WasmPluginConformance`，套件在进程内实例化模块并核对（Windows 上测试项目须设 `<CETCompat>false</CETCompat>`：套件会故意触发陷阱，影子栈强制时整个进程会 fail-fast，套件先以 `PLUGINS_UNAVAILABLE_003` 拒绝）：

1. 模块是 reactor、导出表与导入表符合 ABI v1、`xihan_abi_version` 返回 1。
2. 握手协商出版本 1、清单与文件一致、操作与订阅在清单声明范围内、没有流式操作；声明了订阅 / 中间件就导出了对应函数。
3. `xihan_configure` / `xihan_start` 成功，`xihan_health` 报可服务状态且健康项已在清单声明，`xihan_stop` 成功。
4. 未声明的操作以 `KERNEL_NOTFOUND_001` 的失败状态应答。
5. 每个声明的操作都返回带 `status` 的应答。
6. 向声明的订阅投递消息得到确认。
7. 每个贡献的中间件在 before 阶段返回合法动作（短路时带失败结果），在 after 阶段也应答。

```csharp
public sealed class EchoWasmPluginConformanceTests : WasmPluginConformance
{
    protected override string PluginDirectory => "<EchoWasmPlugin 的 plugin 目录>";
    protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string> { "echoes.trap", "echoes.spin" };
}
```

套件提供的宿主导入只有 `xihan.log`；`xihan.invoke` / `xihan.publish` 在没有宿主容器时返回不可用状态。

## 依赖方向

依赖 `XiHan.Framework.Plugins.Protocol`（协议桩、握手核对）、`XiHan.Framework.Plugins.OutOfProcess`（进程启动）、`XiHan.Framework.Plugins.Wasm`（模块校验与实例）与 xunit 的断言与扩展核心包。

## 状态

P4-04：可用；`samples/EchoPlugin` 通过进程外用例，`samples/EchoWasmPlugin` 通过 Wasm 用例。
