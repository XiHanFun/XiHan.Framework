# XiHan.Framework.Plugins.OutOfProcess

进程外插件宿主（Ring 5，`isolation: out-of-process`，协议 `xihan.plugin.v1`，RFC-0017）。插件是任意语言写的子进程，只需 protobuf + gRPC + JSON；.NET 示例见 `samples/EchoPlugin`。

```csharp
builder.AddXiHan(x => x.UseStandardProfile().UseFeature<OutOfProcessPluginsFeature>());
```

```json
{
  "XiHan": {
    "Plugins": {
      "OutOfProcess": {
        "Directories": ["plugins/echo"],
        "Grants": { "xihan.samples.echo": ["capability:kernel.pipeline"] },
        "HandshakeTimeoutSeconds": 10, "StartTimeoutSeconds": 30, "HealthIntervalSeconds": 10, "HealthFailures": 3,
        "RestartLimit": 5, "RestartWindowSeconds": 600
      }
    }
  }
}
```

清单的 `contributes.config` 声明 `XiHan:Plugins:OutOfProcess`。宿主另读别的领域的两项共享设置，不在清单里（RFC-0002「配置节」）：`XiHan:Kernel:StopTimeoutSeconds`（插件停止宽限的缺省）与 `XiHan:Localization:DefaultLocale`（握手时告诉插件的缺省区域）；每个插件收到的是它自己清单声明的配置节。

## 责任边界

- 配置阶段按 `Directories` 发现插件：读 `xihan.manifest.json`、核对 `Grants` 授予的权限、启动进程、读 stdout 握手行、调用 `Handshake` 并核对清单（`spec/plugin/handshake.md`）。声明的操作登记为代理操作（与本地操作一样经完整管道，由协议适配器按 `expose` 发布），清单 `contributes.permissions` 声明的权限码连同代理操作一次登记（ADR-0080）：权限码进 `PermissionRegistry`（显示名取码本身），同组的操作可以引用它们；声明的码与已登记的重复（`KERNEL_SECURITY_001`）、操作重名或引用了未登记的权限码，一个权限码与代理操作都不留下，配置阶段即启动失败；操作的 `policy` 在授权能力启动时核对、之后登记的在登记时核对（`SECURITY_VALIDATION_004`）；消息目录接进本地化器，订阅的主题登记事件处理器。发现失败的插件以 `Failed` 留在宿主里，不阻止应用启动：清单、权限或入口不合格的不启动进程——包括清单写了 `contributes.pipeline` 的（进程外插件不能贡献中间件，只有 Wasm 插件能，`PLUGINS_VALIDATION_001`，不静默忽略，ADR-0092）——同样可描述、是健康条目 `plugins.<清单 ID>`（清单读不出来时为 `plugins.<目录名>`），宿主附着到容器时逐个记错误日志（`spec/plugin/lifecycle.md` §1）。
- 启动阶段启动回调服务器（Windows 命名管道 / Unix 域套接字）与 `onStartup` 的插件（`Configure` → `Start` → 健康推送）；`onOperation:` / `onEvent:` 的插件在握手后停止，首次调用或投递时再启动。
- 运行期：`Invoke` 带 traceparent、截止时间与已登记 Baggage；业务失败来自 `Status.error` 并用握手得到的目录翻译；传输失败映射为 `PLUGINS_UNAVAILABLE_001` / `KERNEL_TIMEOUT_001` 等（`spec/plugin/invocation.md`）。插件回调 `Host.Invoke` / `Host.Publish` 需要清单权限 `capability:kernel.pipeline` / `capability:messaging.queue`（须在清单声明且已授予，授予多于声明的部分不生效），以插件身份（主体 ID = 清单 ID，方案 `Plugin`）执行。
- 崩溃与启动阶段的握手、`Configure`、`Start` 失败都进入 `Failed`，1 秒起翻倍、上限 60 秒退避重启（启动阶段的失败在附着到容器后开始重试；首次握手就失败的插件重试成功后按运行期加载补登记代理操作，订阅与消息目录要宿主重启才生效；运行期 `LoadAsync` 的这类失败返回给调用方并同样重试），`RestartWindow` 内超过 `RestartLimit` 次后保持 `Failed`；每个插件是应用的一个健康条目 `plugins.<清单 ID>`，`Failed` / `Degraded` 让应用进入 `Degraded` 但不退出。
- 停止：`Stop`（宽限取 `StopGraceSeconds`，默认内核停止超时）→ 等待退出 → 超时先发终止信号（关闭 stdin，Unix 上同时发 SIGTERM；Windows 没有 SIGTERM，关闭 stdin 即等价信号）→ 5 秒内仍未退出再终止进程树；超时都记为 `Failed`（`PLUGINS_TIMEOUT_001`）。
- `IPluginHost.LoadAsync` / `UnloadAsync` 可在运行期加载与卸载；运行期加载的代理操作只对 `IOperationInvoker` 可见，协议适配器的路由在启动时已经发布。卸载先按归属（清单 ID）撤回插件的代理操作与声明的权限码、再停止进程（`plugin/invocation.md` §1，ADR-0085）：之后调用返回 `KERNEL_NOTFOUND_001`，在途调用照常完成或以 `PLUGINS_UNAVAILABLE_001` 结束，同一插件可以直接再加载、不用重启宿主；卸载之后才完成的重试握手不再登记。订阅与消息目录不随卸载撤回。

不负责：插件中间件（RFC-0017「范围」：要在管道阶段插入逻辑用 `Plugins.Wasm`）、流式操作在 .NET 侧的契约（协议已定义 `InvokeStream` 帧，宿主暂不发布流式操作）、操作系统沙箱（`net.outbound:` / `fs.*:` 权限）、Wasm。

## 事件订阅

握手的每个 `Subscription` 按它的 `mode` 订阅主题（`spec/plugin/invocation.md` §4）：空或 `competing` 是竞争消费（订阅了主题的宿主实例里只有一个把消息交给插件），`broadcast` 是广播（每个在线的宿主实例都交给自己的插件进程，尽力而为、不进死信，RFC-0009「订阅方式」）。宿主每个（主题，方式）登记一次转交处理器，只把投递交给以同一方式订阅了主题的插件；`mode` 不合法或同一（主题，类型）以两种方式各声明一次是 `PLUGINS_VALIDATION_004`。

## 依赖方向

依赖 `Plugins.Protocol`（协议类型、握手核对、映射与代理操作）、`Kernel`、`Messaging.Abstractions`、`Localization.Abstractions`，以及 `Grpc.AspNetCore.Server`、`Grpc.Net.Client`。非 AOT。

## 状态

P4-02：发现、握手、配置、启动、调用、事件投递、回调、健康、重启与停止可用。
