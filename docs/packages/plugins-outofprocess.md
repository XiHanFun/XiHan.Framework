# XiHan.Framework.Plugins.OutOfProcess

进程外插件宿主（`isolation: out-of-process`）：按协议 `xihan.plugin.v1` 启动插件子进程、握手、配置、监视健康与崩溃重启，把插件声明的操作登记为经完整管道的代理操作、把订阅的消息投递给插件，并提供受权限约束的宿主回调。适用于任意语言写的插件与远程 Provider。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Plugins.OutOfProcess` |
| 环层 | Ring 5（协议与插件） |
| 能力 | 提供 `plugins.out-of-process@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `OutOfProcessPluginsFeature` |
| 配置节 | `XiHan:Plugins:OutOfProcess` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Plugins.Protocol`、`XiHan.Framework.Messaging.Abstractions`、`XiHan.Framework.Localization.Abstractions`；ASP.NET Core 共享框架；`Grpc.AspNetCore.Server`、`Grpc.Net.Client` |
| AOT | 不兼容（`Google.Protobuf` 的描述符反射未声明裁剪兼容） |
| 稳定性 | Preview |

## 概述

特性在配置阶段按 `Directories` 同步发现插件：读 `xihan.manifest.json`（要求 `kind: plugin`、`isolation: out-of-process`），核对 `Grants` 授予的权限，以插件目录为工作目录启动 `entry` 指向的可执行文件，只继承必要的环境变量并追加 `XIHAN_PLUGIN_*`，在 `HandshakeTimeout` 内读到 stdout 上的握手行、连上插件端点并调用 `Handshake`，再按 `spec/plugin/handshake.md` 核对清单与声明。声明的操作登记为代理操作，清单 `contributes.permissions` 声明的权限码连同它们一次登记进 `PermissionRegistry`（显示名取码本身，同组的操作可以引用，ADR-0080），消息目录接进本地化器，订阅的主题登记事件处理器。

启动阶段启动回调服务器（Windows 上是命名管道，其他系统是 Unix 域套接字）与 `onStartup` 的插件：`Configure`（下发配置节 JSON、权限、绑定与工作目录）→ `Start` → 健康监视。懒激活的插件在握手后停止进程，首次调用或投递时再启动。停止阶段按与登记相反的顺序停止全部插件，再关闭回调服务器。

运行期每次调用带 `traceparent`、截止时间与已登记的 Baggage；业务失败来自插件的 `Status.error`，用握手得到的目录翻译；传输失败映射为 `PLUGINS_UNAVAILABLE_001`、`KERNEL_TIMEOUT_001` 等。插件回调 `Host.Invoke` / `Host.Publish` 以插件身份执行（主体 ID 为清单 ID，方案 `Plugin`）。每个插件是应用的一个健康条目 `plugins.<清单 ID>`。

## 何时使用

- 插件用 Go、Rust 等其他语言写，或要独立进程、独立依赖：用本包。Go 有现成的 [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go)。
- 用插件提供子系统能力（远程 Provider，如 [Caching.Remote](./caching-remote)）：用本包发现插件，再装配对应的 `<领域>.Remote` 适配器。
- 要在管道阶段插入中间件：进程外插件做不到，用 [Plugins.Wasm](./plugins-wasm)。清单写了 `contributes.pipeline` 的进程外插件在发现时被拒（`PLUGINS_VALIDATION_001`），不会照常加载、把中间件静默忽略。
- 受信任的 .NET 扩展、不需要独立进程：[Plugins.InProcess](./plugins-inprocess) 更轻。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Plugins.OutOfProcess --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Plugins.OutOfProcess;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<OutOfProcessPluginsFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.Run();
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Directories` | 字符串数组 | 空 | 启动时装载的插件目录，一个目录一个插件；相对路径相对当前目录 |
| `Grants:<清单 ID>` | 字符串数组 | 空授予 | 授予该插件的权限；清单声明的权限必须全部授予才能加载 |
| `HandshakeTimeoutSeconds` | 正数 | 10 | 读到握手行并完成 `Handshake` 的时限 |
| `StartTimeoutSeconds` | 正数 | 30 | `Configure` 与 `Start` 各自的时限 |
| `StopGraceSeconds` | 正数 | 取 `XiHan:Kernel:StopTimeoutSeconds`（缺省 30） | 停止宽限；超时先发终止信号，5 秒后仍未退出再终止进程树 |
| `HealthIntervalSeconds` | 正数 | 10 | 健康推送断开后的轮询间隔 |
| `HealthFailures` | 正整数 | 3 | 连续多少次健康轮询失败视为进程失效 |
| `RestartLimit` | 非负整数 | 5 | `RestartWindowSeconds` 内最多自动重启多少次，之后保持 `Failed` |
| `RestartWindowSeconds` | 正数 | 600 | 统计重启次数的时间窗 |
| `WorkDirectory` | 字符串 | 临时目录下的 `xihan-plugins` | 插件工作目录的根，每个插件一个子目录，宿主停止时不删除 |

```json
{
  "XiHan": {
    "Plugins": {
      "OutOfProcess": {
        "Directories": [ "plugins/echo" ],
        "Grants": { "xihan.samples.echo": [ "capability:kernel.pipeline" ] },
        "HandshakeTimeoutSeconds": 10,
        "StartTimeoutSeconds": 30,
        "HealthIntervalSeconds": 10,
        "HealthFailures": 3,
        "RestartLimit": 5,
        "RestartWindowSeconds": 600
      }
    },
    "Echo": { "Greeting": "hello" }
  }
}
```

时长不是正数、`HealthFailures` 不是正整数、`RestartLimit` 不是非负整数时，`OutOfProcessPluginOptions.FromConfiguration` 以带配置键的 `ArgumentException` 拒绝启动。插件自己的配置节由插件清单 `contributes.config.section` 决定（上例 `XiHan:Echo`），宿主在 `Configure` 时把它序列化为 JSON 对象下发；绑定从 `XiHan:Bindings:<名字>` 取，只下发清单声明的绑定。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `OutOfProcessPluginsFeature` | 特性入口，能力常量 `Capability`（`plugins.out-of-process`） |
| `OutOfProcessPluginHost` | 宿主：实现 `IPluginHost`、`IHealthContributor`、`IAsyncDisposable`；`Describe(manifestId)` 返回诊断快照，`HostEndpoint` 是回调端点；常量 `PipelinePermission`、`MessagingPermission`、`PluginScheme`、`HealthPrefix` |
| `OutOfProcessPluginStatus` | 诊断快照：`Status`（`PluginStatus`）、`ProcessId`、`ProtocolVersion`、`Restarts`、`Lazy` |
| `OutOfProcessPluginOptions` | 选项与 `FromConfiguration`、`GrantsFor`、`ResolveWorkDirectory` |

特性把宿主同时登记为 `OutOfProcessPluginHost`、`IPluginHost` 与 `IHealthContributor` 单例。

## 使用示例

查看插件状态：

```csharp
using XiHan.Framework.Plugins.OutOfProcess;

static string Describe(IServiceProvider services, string manifestId)
{
    var host = services.GetRequiredService<OutOfProcessPluginHost>();
    // 未加载为 null；发现时被拒的插件也在宿主里，状态为 Failed、没有进程
    return host.Describe(manifestId) is { } status
        ? $"{status.Status.State} pid={status.ProcessId} restarts={status.Restarts} lazy={status.Lazy}"
        : "not loaded";
}
```

经内核调用器调用插件操作（与本地操作写法相同）：

```csharp
using System.Text;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;

static async Task<OperationResponse> EchoAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None, deadline: DateTimeOffset.UtcNow.AddSeconds(5));
    var invoker = scope.ServiceProvider.GetRequiredService<IOperationInvoker>();
    return await invoker.InvokeAsync(context, new OperationRequest("echoes.create", Encoding.UTF8.GetBytes("""{"text":"ping"}""")));
}
```

完整的插件示例：.NET 写的 `samples/EchoPlugin`（只依赖 protobuf 与 gRPC，不引用任何 XiHan 包）、Go 写的 `samples/EchoGoPlugin` 与 `samples/CacheGoPlugin`。

## 行为与限制

- 发现失败不阻止应用启动，也不会静默消失：清单不合法（含写了 `contributes.pipeline`：只有 Wasm 插件能贡献中间件，消息键 `plugins.process.pipeline_unsupported`，细节 `middleware` 列出声明的中间件名）、权限未授予、可执行文件不存在的插件不启动进程，以 `Failed` 登记（出现在 `Plugins` 里、可 `Describe`、健康条目 `plugins.<清单 ID>`，清单读不出来时只有健康条目 `plugins.<目录名>`），宿主启动时记错误日志，不重试；握手失败的插件同样以 `Failed` 留在宿主里。运行期 `LoadAsync` 的同类失败直接返回给调用方、不登记。
- 启动阶段的握手、`Configure`、`Start` 失败按重启策略重试（`spec/plugin/lifecycle.md` §6）：宿主附着到容器后按与崩溃相同的退避在后台重试，计入 `RestartLimit`；运行期 `LoadAsync` 的这类失败返回给调用方，插件保持 `Failed` 并同样重试，直到 `UnloadAsync`。首次握手就失败的插件重试成功后，代理操作按运行期加载补登记（只对 `IOperationInvoker` 可见，协议适配器不新增路由），握手里的订阅与消息目录要宿主重启才生效（记警告）；声明登记不进去（声明的权限码与已登记的重复 `KERNEL_SECURITY_001`、与已有操作重名，或操作的 `permission` 既不是宿主已登记的、也不在本插件清单里，`KERNEL_VALIDATION_005`，或授权能力的启动核对之后操作的 `policy` 不存在，`SECURITY_VALIDATION_004`）则按发现时被拒处理、不再重试，一个权限码与代理操作都不留下（一个插件的权限码与操作全进或全不进，ADR-0071、ADR-0080）。懒激活插件首次调用时启动失败，该次调用失败，下一次调用再试。宿主附着后改用容器里的 `TimeProvider` 计时退避、健康轮询与停止等待。插件操作与应用操作重名、插件声明的权限码与宿主或别的插件已登记的码重复，都是配置错误，启动失败。
- 崩溃（进程非 `Stop` 引起的退出，或健康轮询连续失败后被终止）进入 `Failed`（`PLUGINS_UNEXPECTED_002`），按 1 秒起翻倍、上限 60 秒退避重启；窗口内超过 `RestartLimit` 次后保持 `Failed`，直到 `UnloadAsync` + `LoadAsync`。重启后的握手声明必须与首次一致。
- 插件 `Failed` 或 `Degraded` 让应用进入 `Degraded`，不让应用退出。
- 停止：发 `Stop`（宽限秒数与原因 `shutdown` / `unload` / `restart`）→ 在宽限内等待退出 → 超时先发终止信号：关闭插件的 stdin，Unix 上同时发 SIGTERM（Windows 没有 SIGTERM，关闭 stdin 就是等价的信号）→ 5 秒内仍未退出才终止进程树；两种结局都记为 `Failed`（`PLUGINS_TIMEOUT_001`）。插件应把 SIGTERM 与 stdin 关闭都当作立即收尾退出的请求。
- 运行期 `LoadAsync` 的代理操作只对 `IOperationInvoker` 可见，协议适配器不会为它新增路由。卸载先按归属撤回插件的代理操作与声明的权限码、再停止进程（ADR-0085）：之后调用返回 `KERNEL_NOTFOUND_001`，在途调用照常完成或以 `PLUGINS_UNAVAILABLE_001` 结束；同一插件可以直接再加载，不用重启宿主。卸载之后才完成的重试握手不再登记（只登记仍是宿主当前运行时的插件）。订阅与消息目录不随卸载撤回。
- 回调需要会话令牌（元数据 `xihan-plugin-session`），缺失或不符为 `Unauthenticated` + `PLUGINS_DENIED_002`；`Host.Invoke` 要 `capability:kernel.pipeline`，`Host.Publish` 要 `capability:messaging.queue`，权限须在清单声明且已授予（授予多于声明的部分不生效），未声明或未授予为 `PermissionDenied` + `PLUGINS_DENIED_002`。
- 不负责：流式操作在 .NET 侧的发布（协议已定义 `InvokeStream` 帧）、操作系统沙箱（`net.outbound:` / `fs.*:` 只核对授予）、Wasm。
- 事件订阅：握手的每个 `Subscription` 按它的 `mode` 订阅主题（`spec/plugin/invocation.md` §4）：空或 `competing` 是竞争消费（订阅了主题的宿主实例里只有一个把消息交给插件），`broadcast` 是广播（每个在线的宿主实例都交给自己的插件进程，尽力而为、不进死信，RFC-0009「订阅方式」）。宿主每个（主题，方式）登记一次转交处理器，只把投递交给以同一方式订阅了主题的插件；`mode` 不合法或同一（主题，类型）以两种方式各声明一次是 `PLUGINS_VALIDATION_004`。
- 清单的 `contributes.config` 声明 `XiHan:Plugins:OutOfProcess`。宿主另读别的领域的两项共享设置，不在清单里（RFC-0002「配置节」）：`XiHan:Kernel:StopTimeoutSeconds`（插件停止宽限的缺省）与 `XiHan:Localization:DefaultLocale`（握手时告诉插件的缺省区域）；每个插件收到的是它自己清单声明的配置节。

## 相关

- [插件](/plugins/overview)
- [Plugins.Protocol](./plugins-protocol)、[Plugins.Wasm](./plugins-wasm)、[Caching.Remote](./caching-remote)
- [Conformance 一致性套件](./conformance#plugins)
- 规范：RFC-0017《进程外插件协议》、RFC-0019《远程 Provider》
