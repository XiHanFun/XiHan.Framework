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

## 责任边界

- 配置阶段按 `Directories` 发现插件：读 `xihan.manifest.json`、核对 `Grants` 授予的权限、启动进程、读 stdout 握手行、调用 `Handshake` 并核对清单（`spec/plugin/handshake.md`）。声明的操作登记为代理操作（与本地操作一样经完整管道，由协议适配器按 `expose` 发布），消息目录接进本地化器，订阅的主题登记事件处理器。发现失败的插件以 `Failed` 留在宿主里，不阻止应用启动。
- 启动阶段启动回调服务器（Windows 命名管道 / Unix 域套接字）与 `onStartup` 的插件（`Configure` → `Start` → 健康推送）；`onOperation:` / `onEvent:` 的插件在握手后停止，首次调用或投递时再启动。
- 运行期：`Invoke` 带 traceparent、截止时间与已登记 Baggage；业务失败来自 `Status.error` 并用握手得到的目录翻译；传输失败映射为 `PLUGINS_UNAVAILABLE_001` / `KERNEL_TIMEOUT_001` 等（`spec/plugin/invocation.md`）。插件回调 `Host.Invoke` / `Host.Publish` 需要清单权限 `capability:kernel.pipeline` / `capability:messaging.queue`，以插件身份（主体 ID = 清单 ID，方案 `Plugin`）执行。
- 崩溃进入 `Failed`，1 秒起翻倍、上限 60 秒退避重启，`RestartWindow` 内超过 `RestartLimit` 次后保持 `Failed`；每个插件是应用的一个健康条目 `plugins.<清单 ID>`，`Failed` / `Degraded` 让应用进入 `Degraded` 但不退出。
- 停止：`Stop`（宽限取 `StopGraceSeconds`，默认内核停止超时）→ 等待退出 → 超时终止进程树。
- `IPluginHost.LoadAsync` / `UnloadAsync` 可在运行期加载与卸载；运行期加载的代理操作只对 `IOperationInvoker` 可见，协议适配器的路由在启动时已经发布。

不负责：流式操作在 .NET 侧的契约（协议已定义 `InvokeStream` 帧，宿主暂不发布流式操作）、操作系统沙箱（`net.outbound:` / `fs.*:` 权限）、Wasm。

## 依赖方向

依赖 `Plugins.Protocol`（协议类型、握手核对、映射与代理操作）、`Kernel`、`Messaging.Abstractions`、`Localization.Abstractions`，以及 `Grpc.AspNetCore.Server`、`Grpc.Net.Client`。非 AOT。

## 状态

P4-02：发现、握手、配置、启动、调用、事件投递、回调、健康、重启与停止可用。
