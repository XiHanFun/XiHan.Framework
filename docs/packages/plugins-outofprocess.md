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

特性在配置阶段按 `Directories` 同步发现插件：读 `xihan.manifest.json`（要求 `kind: plugin`、`isolation: out-of-process`），核对 `Grants` 授予的权限，以插件目录为工作目录启动 `entry` 指向的可执行文件，只继承必要的环境变量并追加 `XIHAN_PLUGIN_*`，在 `HandshakeTimeout` 内读到 stdout 上的握手行、连上插件端点并调用 `Handshake`，再按 `spec/plugin/handshake.md` 核对清单与声明。声明的操作登记为代理操作，清单 `contributes.permissions` 声明的权限码连同它们一次登记进 `PermissionRegistry`（显示名取码本身，同组的操作可以引用，ADR-0080），消息目录以清单 ID 为归属注册进本地化器（卸载时撤回，ADR-0252），订阅的主题登记事件处理器。

回调服务器的端点在创建宿主时选定：Windows 上是只允许当前用户的命名管道加回环 TCP，其他系统是私有目录里的 Unix 域套接字加回环 TCP（临时目录的路径含空白或 `;` 时只有回环 TCP），回环 TCP 的端口由系统分配并在此刻占住；端点按这个偏好以 `;` 分隔写进插件的 `XIHAN_PLUGIN_HOST_ADDRESS`，插件取第一个自己能连的（ADR-0237）。启动阶段让回调服务器在这些端点上监听，再启动 `onStartup` 的插件：`Configure`（下发配置节 JSON、权限、绑定与工作目录）→ `Start` → 健康监视。懒激活的插件在握手后停止进程，首次调用或投递时再启动。停止阶段按与登记相反的顺序停止全部插件，再关闭回调服务器。

运行期每次调用带 `traceparent`、截止时间与已登记的 Baggage；每次调用插件的操作、向插件投递事件各记一条 `xihan.plugin.invoke` Span（内核 `xihan.invoke` 的子 Span，带 `xihan.plugin.id`，操作调用另带 `xihan.operation`），写给插件的 `traceparent` 以它为父级，没有监听器时不开 Span（ADR-0269）；业务失败来自插件的 `Status.error`，用握手得到的目录翻译；传输失败映射为 `PLUGINS_UNAVAILABLE_001`、`KERNEL_TIMEOUT_001` 等。插件回调 `Host.Invoke` / `Host.Publish` 以插件身份执行（主体 ID 为清单 ID，方案 `Plugin`），只能带在途调用的租户（ADR-0195）；两者都按元数据建调用上下文，截止时间取元数据与 gRPC 调用自身截止时间中较早的，`Host.Publish` 在这个上下文里发布，信封缺的租户、主体、关联 ID、区域与追踪由发布器补齐（ADR-0237）。每个插件是应用的一个健康条目 `plugins.<清单 ID>`。

## 何时使用

- 插件用 Go、Rust 等其他语言写，或要独立进程、独立依赖：用本包。Go 有现成的 [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go)。
- 用插件提供子系统能力（远程 Provider，如 [Caching.Remote](./caching-remote)）：用本包发现插件，再装配对应的 `<领域>.Remote` 适配器。
- 要在管道阶段插入中间件：进程外插件做不到，用 [Plugins.Wasm](./plugins-wasm)。清单写了 `contributes.pipeline` 的进程外插件在发现时被拒（`PLUGINS_VALIDATION_001`），不会照常加载、把中间件静默忽略。
- 受信任的 .NET 扩展、不需要独立进程：[Plugins.InProcess](./plugins-inprocess) 更轻。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Plugins.OutOfProcess --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[环境与安装](/getting-started/installation)。

```csharp
using XiHan.Framework.Plugins.OutOfProcess;

builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<OutOfProcessPluginsFeature>());

// app.UseXiHan() 之后
app.MapXiHanHealth();
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Directories` | 字符串数组 | 空 | 启动时装载的插件目录，一个目录一个插件；相对路径相对当前目录 |
| `Grants:<清单 ID>` | 字符串数组 | 空授予 | 授予该插件的权限；清单声明的权限必须全部授予才能加载。清单要读的宿主配置节与绑定也在这里授予：`config:<配置节>`、`binding:<绑定名>`，没有授予即拒绝加载 |
| `HandshakeTimeoutSeconds` | 正数 | 10 | 读到握手行并完成 `Handshake` 的时限 |
| `StartTimeoutSeconds` | 正数 | 30 | `Configure` 与 `Start` 各自的时限 |
| `StopGraceSeconds` | 正数 | 取 `XiHan:Kernel:StopTimeoutSeconds`（缺省 30） | 停止宽限；超时先发终止信号，5 秒后仍未退出再终止进程树 |
| `HealthIntervalSeconds` | 正数 | 10 | 健康推送断开后的轮询间隔 |
| `HealthFailures` | 正整数 | 3 | 连续多少次健康轮询失败视为进程失效 |
| `RestartLimit` | 非负整数 | 5 | `RestartWindowSeconds` 内最多自动重启多少次，之后保持 `Failed` |
| `RestartWindowSeconds` | 正数 | 600 | 统计重启次数的时间窗 |
| `WorkDirectory` | 字符串 | 临时目录下的 `xihan-plugins-<用户名>` | 插件工作目录的根，每个插件一个子目录（Unix 上 0700），宿主停止时不删除；缺省的根在 Unix 上必须是本用户私有的真实目录（0700、不是链接），否则启动插件失败（ADR-0207） |

```json
{
  "XiHan": {
    "Plugins": {
      "OutOfProcess": {
        "Directories": [ "plugins/echo" ],
        "Grants": { "xihan.samples.echo": [ "capability:kernel.pipeline", "config:XiHan:Echo" ] },
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

时长不是正数、`HealthFailures` 不是正整数、`RestartLimit` 不是非负整数时，`OutOfProcessPluginOptions.FromConfiguration` 以带配置键的 `ArgumentException` 拒绝启动。插件自己的配置节由插件清单 `contributes.config.section` 决定（上例 `XiHan:Echo`），宿主在 `Configure` 时把它序列化为 JSON 对象下发。清单声明并授予（`binding:<绑定名>`）的资源绑定随同一个请求下发，与内核按同一套来源解析（ADR-0268）：先读 Service Binding 目录（`SERVICE_BINDING_ROOT`，或应用经 `UseServiceBindingRoot` 指定的目录），再读 `XiHan:Bindings:<名字>`，同名时配置整体覆盖目录；绑定名与 `type` / `value` 子键按字面比。每次配置插件时解析一遍，重启或运行期加载的插件拿到的是那一刻的值；只读清单声明的那几个绑定，别的绑定读不出来不影响它。下发名字、清单声明的类型、主值与全部属性（`Binding.properties`，ADR-0285）：属性名区分大小写、原样下发，只有属性、没有主值的绑定照常下发、主值为空。下发之前按清单的顺序逐个绑定核对，依次是有没有、类型、属性（ADR-0292）：清单标为必需（`required` 缺省 `true`）的绑定两种来源都没给或只给了类型，或者来源声明的类型与清单不同（按序数比较，必需与可选都核对），这个插件配置失败；可选的绑定没给时不下发，只给了类型的不下发并记 Warning。属性名须是 1 到 253 个 ASCII 字母、数字、`.`、`_`、`-` 且不以 `.` 开头，一个绑定最多 64 个属性、属性名与属性值合计不超过 262144 个 UTF-8 字节；逐个绑定都合格之后，整个 `Configure` 请求按 protobuf 编码不超过 2097152 字节，不合时不少发、不截断。属性随绑定受 `binding:<绑定名>` 约束、不单独授权，属性值不进日志与错误。声明的绑定在目录里读不出来时返回 `PLUGINS_UNEXPECTED_001`（`phase` 为 `configure`，`reason` 只写异常类型），绑定目录超过内核的大小或文件数上限（一个绑定多于 128 个文件时 `reason` 为 `binding '<绑定名>' has more than 128 files`）或上述核对不合时同样返回它（`reason` 是只带绑定名、类型名与上限的固定文本，全文见[资源绑定](/configuration/bindings#下发给插件的绑定)），宿主不发 `Configure`，记一条 Error（`The configure request of plugin <清单 ID> is not sent: <reason>; the plugin is not configured.`），插件进入 Failed 并按重启策略重试。插件一侧 gRPC 服务端的接收上限不得小于 4194304 字节（各 gRPC 库的缺省值）。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `OutOfProcessPluginsFeature` | 特性入口，能力常量 `Capability`（`plugins.out-of-process`） |
| `OutOfProcessPluginHost` | 宿主：实现 `IPluginHost`、`IHealthContributor`、`IAsyncDisposable`；`FindStatus(manifestId)` 查找诊断快照（未加载时无值），`HostEndpoints` 是按偏好排序的回调端点（回环 TCP 在最后）；构造的可选参数 `serviceBindingRoot` 是下发给插件的绑定的 Service Binding 根目录（经特性装配时取 `FeatureContext.ServiceBindingRoot`，自己构造且应用用了 `UseServiceBindingRoot` 的传同一个目录，不传取环境变量）；常量 `PipelinePermission`、`MessagingPermission`、`PluginScheme`、`HealthPrefix` |
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
    // 未加载时无值；发现时被拒的插件也在宿主里，状态为 Failed、没有进程
    return host.FindStatus(manifestId).TryGetValue(out var status)
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
    var context = App.CreateContext(scope.ServiceProvider, new InvocationContextOptions { Deadline = DateTimeOffset.UtcNow.AddSeconds(5) }, CancellationToken.None);
    var invoker = scope.ServiceProvider.GetRequiredService<IOperationInvoker>();
    return await invoker.InvokeAsync(context, new OperationRequest("echoes.create", Encoding.UTF8.GetBytes("""{"text":"ping"}""")));
}
```

完整的插件示例：.NET 写的 `samples/EchoPlugin`（只依赖 protobuf 与 gRPC，不引用任何 XiHan 包）、Go 写的 `samples/EchoGoPlugin` 与 `samples/CacheGoPlugin`。

## 行为与限制

- 发现失败不阻止应用启动，也不会静默消失：清单不合法（含写了 `contributes.pipeline`：只有 Wasm 插件能贡献中间件，消息键 `plugins.process.pipeline_unsupported`，细节 `middleware` 列出声明的中间件名）、权限未授予、可执行文件不存在的插件不启动进程，以 `Failed` 登记（出现在 `Plugins` 里、可 `FindStatus`、健康条目 `plugins.<清单 ID>`，清单读不出来时只有健康条目 `plugins.<目录名>`），宿主启动时记错误日志，不重试；握手失败的插件同样以 `Failed` 留在宿主里。运行期 `LoadAsync` 的同类失败直接返回给调用方、不登记。
- 启动阶段的握手、`Configure`、`Start` 失败按重启策略重试（`spec/plugin/lifecycle.md` §6）：宿主附着到容器后按与崩溃相同的退避在后台重试，计入 `RestartLimit`；运行期 `LoadAsync` 的这类失败返回给调用方，插件保持 `Failed` 并同样重试，直到 `UnloadAsync`。首次握手就失败的插件重试成功后，消息目录与代理操作按运行期加载补登记（HTTP 路由、OpenAPI 文档与 MCP tool 随之出现，gRPC 方法不补，ADR-0251、ADR-0252），握手里的订阅要宿主重启才生效（记警告）；声明登记不进去（消息目录与同名目录的模板冲突 `LOCALIZATION_CONFLICT_001`、声明的权限码与已登记的重复 `KERNEL_SECURITY_001`、与已有操作重名，或操作的 `permission` 既不是宿主已登记的、也不在本插件清单里，`KERNEL_VALIDATION_005`，或授权能力的启动核对之后操作的 `policy` 不存在，`SECURITY_VALIDATION_004`）则按发现时被拒处理、不再重试，一个目录、权限码与代理操作都不留下（一个插件的目录、权限码与操作全进或全不进，ADR-0071、ADR-0080）。懒激活插件首次调用时启动失败，该次调用失败，下一次调用再试。宿主附着后改用容器里的 `TimeProvider` 计时退避、健康轮询与停止等待。插件操作与应用操作重名、插件声明的权限码与宿主或别的插件已登记的码重复，都是配置错误，启动失败。
- 崩溃（进程非 `Stop` 引起的退出，或健康轮询连续失败后被终止）进入 `Failed`（`PLUGINS_UNEXPECTED_002`），按 1 秒起翻倍、上限 60 秒退避重启；窗口内超过 `RestartLimit` 次后保持 `Failed`，直到 `UnloadAsync` + `LoadAsync`。重启后的握手声明必须与首次一致。
- 插件 `Failed` 或 `Degraded` 让应用进入 `Degraded`，不让应用退出。
- 停止：发 `Stop`（宽限秒数与原因 `shutdown` / `unload` / `restart`）→ 在宽限内等待退出 → 超时先发终止信号：关闭插件的 stdin，Unix 上同时发 SIGTERM（Windows 没有 SIGTERM，关闭 stdin 就是等价的信号）→ 5 秒内仍未退出才终止进程树；两种结局都记为 `Failed`（`PLUGINS_TIMEOUT_001`）。插件应把 SIGTERM 与 stdin 关闭都当作立即收尾退出的请求。
- 运行期 `LoadAsync` 的插件先以清单 ID 为归属登记消息目录，再登记代理操作（ADR-0252）：操作登记之后立即有 HTTP 路由、进 OpenAPI 文档、是 MCP tool（ADR-0251），文档里的文案这时已经能解析；目录与同名目录的模板冲突（`LOCALIZATION_CONFLICT_001`）、推导出的路由或 tool 名冲突时整个插件登记失败；gRPC 方法与 AI 工具目录不随之增加。卸载先按归属撤回插件的代理操作与声明的权限码、再撤回消息目录、最后停止进程（ADR-0085、ADR-0252）：路由与 tool 随之消失，之后经调用器按 ID 调用返回 `KERNEL_NOTFOUND_001`，在途调用照常完成或以 `PLUGINS_UNAVAILABLE_001` 结束；同一插件可以直接再加载，不用重启宿主。卸载之后才完成的重试握手不再登记（只登记仍是宿主当前运行时的插件）。配置阶段发现的插件的目录同样随卸载撤回、随再加载重新登记；订阅不随卸载撤回。
- 宿主调用插件的每一次 RPC 都带会话令牌（元数据 `xihan-plugin-session`），插件必须校验、不符即 `Unauthenticated`（ADR-0202）；宿主的回调套接字在新建的私有目录里（`0700`，文件 `0600`），命名管道只允许当前用户，回环 TCP 同机的其他用户连得上，隔离靠令牌。
- 回调需要会话令牌（元数据 `xihan-plugin-session`，恰好一个值：带两个即使其中一个正确也不认），缺失或不符为 `Unauthenticated` + `PLUGINS_DENIED_002`，两个端点认同一套令牌；`Host.Invoke` 要 `capability:kernel.pipeline`，`Host.Publish` 要 `capability:messaging.queue`，权限须在清单声明且已授予（授予多于声明的部分不生效），未声明或未授予为 `PermissionDenied` + `PLUGINS_DENIED_002`。
- 回调元数据的 `xihan.tenant` 必须是宿主此刻交给该插件处理的某个调用的租户，否则 `Status.error` 为 `PLUGINS_DENIED_002`（gRPC 状态 `OK`）；`Host.Publish` 的元数据越界即整批不发布、每一项都是 `PLUGINS_DENIED_002` + `plugins.callback.tenant_denied`，信封带 `xihantenant` 时逐项判定（ADR-0195、ADR-0237）。
- `Host.Publish` 的信封 `time` 是空串（没有时间）或带秒与偏移的 RFC 3339 时刻，文法与消息信封、截止时间相同，读成 UTC；不合文法的那一项以 `MESSAGING_VALIDATION_002` + `messaging.envelope.invalid_time`（参数 `time` 是原文）不发布，其余各项照常（ADR-0293）。宿主写给插件的信封一律是 UTC、七位小数、`+00:00`（`2026-11-25T03:25:00.0000000+00:00`），没有时间写空串。
- 不负责：流式操作在 .NET 侧的发布（协议已定义 `InvokeStream` 帧）、操作系统沙箱（`net.outbound:` / `fs.*:` 只核对授予）、Wasm。
- 事件订阅：握手的每个 `Subscription` 按它的 `mode` 订阅主题（`spec/plugin/invocation.md` §4）：空或 `competing` 是竞争消费（订阅了主题的宿主实例里只有一个把消息交给插件），`broadcast` 是广播（每个在线的宿主实例都交给自己的插件进程，尽力而为、不进死信，RFC-0009「订阅方式」）。宿主每个（主题，方式）登记一次转交处理器，只把投递交给以同一方式订阅了主题的插件；`mode` 不合法或同一（主题，类型）以两种方式各声明一次是 `PLUGINS_VALIDATION_004`。
- 清单的 `contributes.config` 声明 `XiHan:Plugins:OutOfProcess`。宿主另读别的领域的两项共享设置，不在清单里（RFC-0002「配置节」）：`XiHan:Kernel:StopTimeoutSeconds`（插件停止宽限的缺省）与 `XiHan:Localization:DefaultLocale`（握手时告诉插件的缺省区域）；每个插件收到的是它自己清单声明的配置节。

## 相关

- [插件](/plugins/overview)
- [Plugins.Protocol](./plugins-protocol)、[Plugins.Wasm](./plugins-wasm)、[Caching.Remote](./caching-remote)
- [Conformance 一致性套件](./conformance#plugins)
- 规范：RFC-0017《进程外插件协议》、RFC-0019《远程 Provider》
