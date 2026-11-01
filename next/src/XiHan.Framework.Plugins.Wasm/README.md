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

清单的 `contributes.config` 声明 `XiHan:Plugins:Wasm`。宿主另读 `XiHan:Localization:DefaultLocale`（握手时告诉插件的缺省区域），属于本地化领域，不在清单里（RFC-0002「配置节」）；每个插件收到的是它自己清单声明的配置节。

## 责任边界

- 配置阶段按 `Directories` 发现插件：读 `xihan.manifest.json`、校验权限（`capability:` 看 `Grants`，`fs.read:` / `fs.write:` 看 `Mounts`，`net.outbound:` 永不授予）、编译模块并校验导出 / 导入表、实例化并 `xihan_handshake`、核对清单。声明的操作登记为代理操作，清单 `contributes.permissions` 声明的权限码连同代理操作一次登记（ADR-0080）（权限码进 `PermissionRegistry`、同组操作可以引用；声明的码与已登记的重复 `KERNEL_SECURITY_001`、操作重名或引用了未登记的权限码，全组不进，配置阶段即启动失败），消息目录接进本地化器，订阅登记事件处理器，清单 `contributes.pipeline` 的每一项按 `清单 ID:中间件名` 插进对应阶段（`handle` 阶段不允许），`before` / `after` 按 RFC-0004 参与排序（先按本插件的中间件名解析，否则作为管道里的中间件名，`spec/plugin/wasm.md` §6）。发现失败的插件以 `Failed` 留在宿主里：清单、权限或模块文件不合格的不实例化，同样可描述、是健康条目（清单读不出来时为 `plugins.<目录名>`），宿主附着到容器时逐个记错误日志（`spec/plugin/lifecycle.md` §1）。
- 启动阶段启动 `onStartup` 的插件（`xihan_configure` → `xihan_start` → 健康轮询）；`onOperation:` / `onEvent:` 的插件在握手后丢弃实例，首次调用时再实例化。
- 运行期：每插件一个实例、调用串行；调用在上下文剩余时间（没有时取 `CallTimeout`）内经 epoch 中断强制停止；回调 `xihan.invoke` / `xihan.publish` 以插件身份执行，所需权限须在清单声明且已授予，受当前调用的截止时间与取消约束（新调用的截止时间取请求元数据与当前调用中较早者，发布随当前调用取消，到期返回 `KERNEL_TIMEOUT_001`），嵌套深度上限 8；回调期间管道再次进入同一插件（它自己的中间件、操作或订阅）时，在同一执行线程上、导入函数返回前同步再入，受外层调用的截止时间约束。
- 中间件：进入下游前后各调一次 `xihan_middleware`，before 可短路（必须给出失败）或改写已登记 Baggage，after 只观察；陷阱、超时或无响应都关闭调用（`PLUGINS_UNAVAILABLE_001`）。
- 陷阱、超时与无响应把插件记为 `Failed` 并按 1 秒起翻倍、上限 60 秒的退避重建实例，`RestartWindow` 内超过 `RestartLimit` 次后保持 `Failed`；每个插件是应用的健康条目 `plugins.<清单 ID>`。
- 沙箱：线性内存与栈上限、无网络、文件系统只有映射的预开放目录；WASI 的 stdout / stderr 写到工作目录的 `stdout.log` / `stderr.log`，日志应走 `xihan.log`。
- 宿主进程（Windows）：影子栈强制时第一次陷阱就让整个进程 fail-fast（退出码 `0xC0000409`），所以宿主、测试与一致性套件的可执行项目都要设 `<CETCompat>false</CETCompat>`；创建宿主时检测进程的影子栈策略，强制时以 `PLUGINS_UNAVAILABLE_003` 抛出 `FrameworkException`。
- `IPluginHost.LoadAsync` / `UnloadAsync` 可在运行期加载与卸载不贡献中间件的插件。

不负责：组件模型 / WIT（ABI v2）、流式操作、多实例并发、`net.outbound`。

## 事件订阅

与进程外插件相同：握手的 `Subscription.mode` 为空或 `competing` 是竞争消费，`broadcast` 是广播（每个宿主实例都交给自己的插件实例，RFC-0009「订阅方式」）；宿主只把投递交给以同一方式订阅了主题的插件，`mode` 不合法或同一（主题，类型）以两种方式各声明一次是 `PLUGINS_VALIDATION_004`。

## 依赖方向

依赖 `Plugins.Protocol`、`Kernel`、`Messaging.Abstractions`、`Localization.Abstractions` 与 NuGet `Wasmtime`（含各平台原生库）。非 AOT。

## 状态

P4-03：Experimental；`samples/EchoWasmPlugin` 通过宿主测试与协议一致性套件。
