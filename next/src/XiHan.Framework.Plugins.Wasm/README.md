# XiHan.Framework.Plugins.Wasm

Wasm 插件宿主（Ring 5，`isolation: wasm`，RFC-0018，**Experimental**）。插件是 WASI preview 1 的 reactor 核心模块，宿主用 wasmtime 在进程内沙箱里运行，消息与进程外插件相同（`xihan.plugin.v1`），编码为 proto3 JSON，经导出函数 + 线性内存传递（`spec/plugin/wasm.md`）。Rust 示例见 `samples/EchoWasmPlugin`。

```csharp
builder.AddXiHan(x => x.UseStandardProfile().UseFeature<WasmPluginsFeature>());
```

```xml
<!-- 宿主可执行项目（Windows）：wasmtime 的陷阱处理与 .NET 9 起默认开启的 CET 影子栈冲突 -->
<CETCompat>false</CETCompat>
```

```json
{
  "XiHan": {
    "Plugins": {
      "Wasm": {
        "Directories": ["plugins/echo-wasm"],
        "Grants": { "xihan.samples.echowasm": ["capability:kernel.pipeline"] },
        "Mounts": { "xihan.samples.echowasm": { "/data": "C:/plugins/echo-wasm/data" } },
        "MemoryLimitBytes": 67108864, "StackLimitBytes": 1048576, "EpochIntervalMilliseconds": 10,
        "CallTimeoutSeconds": 30, "MiddlewareTimeoutMilliseconds": 100, "HealthIntervalSeconds": 10, "HealthFailures": 3,
        "RestartLimit": 5, "RestartWindowSeconds": 600
      }
    }
  }
}
```

## 责任边界

- 配置阶段按 `Directories` 发现插件：读 `xihan.manifest.json`、校验权限（`capability:` 看 `Grants`，`fs.read:` / `fs.write:` 看 `Mounts`，`net.outbound:` 永不授予）、编译模块并校验导出 / 导入表、实例化并 `xihan_handshake`、核对清单。声明的操作登记为代理操作，消息目录接进本地化器，订阅登记事件处理器，清单 `contributes.pipeline` 的每一项按 `清单 ID:中间件名` 插进对应阶段（`handle` 阶段不允许）。
- 启动阶段启动 `onStartup` 的插件（`xihan_configure` → `xihan_start` → 健康轮询）；`onOperation:` / `onEvent:` 的插件在握手后丢弃实例，首次调用时再实例化。
- 运行期：每插件一个实例、调用串行；调用在上下文剩余时间（没有时取 `CallTimeout`）内经 epoch 中断强制停止；回调 `xihan.invoke` / `xihan.publish` 以插件身份执行并按权限判定，嵌套深度上限 8；回调期间管道再次进入同一插件（它自己的中间件、操作或订阅）时，在同一执行线程上、导入函数返回前同步再入，受外层调用的截止时间约束。
- 中间件：进入下游前后各调一次 `xihan_middleware`，before 可短路（必须给出失败）或改写已登记 Baggage，after 只观察；陷阱、超时或无响应都关闭调用（`PLUGINS_UNAVAILABLE_001`）。
- 陷阱、超时与无响应把插件记为 `Failed` 并按 1 秒起翻倍、上限 60 秒的退避重建实例，`RestartWindow` 内超过 `RestartLimit` 次后保持 `Failed`；每个插件是应用的健康条目 `plugins.<清单 ID>`。
- 沙箱：线性内存与栈上限、无网络、文件系统只有映射的预开放目录；WASI 的 stdout / stderr 写到工作目录的 `stdout.log` / `stderr.log`，日志应走 `xihan.log`。
- 宿主进程（Windows）：影子栈强制时第一次陷阱就让整个进程 fail-fast（退出码 `0xC0000409`），所以宿主、测试与一致性套件的可执行项目都要设 `<CETCompat>false</CETCompat>`；创建宿主时检测进程的影子栈策略，强制时以 `PLUGINS_UNAVAILABLE_003` 抛出 `FrameworkException`。
- `IPluginHost.LoadAsync` / `UnloadAsync` 可在运行期加载与卸载不贡献中间件的插件。

不负责：组件模型 / WIT（ABI v2）、流式操作、多实例并发、`net.outbound`。

## 依赖方向

依赖 `Plugins.Protocol`、`Kernel`、`Messaging.Abstractions`、`Localization.Abstractions` 与 NuGet `Wasmtime`（含各平台原生库）。非 AOT。

## 状态

P4-03：Experimental；`samples/EchoWasmPlugin` 通过宿主测试与协议一致性套件。
