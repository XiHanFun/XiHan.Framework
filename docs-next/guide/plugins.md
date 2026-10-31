# 插件

本页讲插件的三种隔离级别、清单与权限、发现与加载、握手与生命周期，以及怎样用 .NET、Rust 或 Go 写插件，适用于要在应用里运行第三方或跨语言代码的宿主开发者和插件作者。各宿主的完整 API 与配置见 [Plugins.InProcess](../packages/plugins-inprocess)、[Plugins.OutOfProcess](../packages/plugins-outofprocess)、[Plugins.Wasm](../packages/plugins-wasm) 与 [Plugins.Protocol](../packages/plugins-protocol)。

## 三种隔离级别

插件是在运行期装载、与宿主隔离的代码，清单写 `kind: plugin`，`isolation` 决定由哪个宿主包加载。它和[能力包](./modularity)的区别在于装配时机与信任：能力包在启动时进入能力图谱、与应用一起编译，插件按目录发现、由宿主授予权限，失败只影响自己。

| `isolation` | 宿主包 | 载体 | 插件可贡献 | 稳定性 |
| --- | --- | --- | --- | --- |
| `in-process` | `Plugins.InProcess` | 可收集的 `AssemblyLoadContext`，插件有自己的服务容器 | 插件自己的服务与启停逻辑 | Preview |
| `wasm` | `Plugins.Wasm` | wasmtime 进程内沙箱里的 WASI preview 1 reactor 模块 | 操作、事件处理器、管道中间件 | Experimental |
| `out-of-process` | `Plugins.OutOfProcess` | 子进程 + 本机 gRPC，协议 `xihan.plugin.v1` | 操作、事件处理器 | Preview |

三者的信任边界不同，选型主要看这一点。

进程内插件与宿主同进程，`XiHan.Framework.*`、`Microsoft.Extensions.*`、`System.*` 程序集与宿主共享（类型身份一致），私有依赖按插件自己的 `.deps.json` 隔离解析。它能访问进程里的一切，宿主只在加载前核对权限，运行期无从约束，所以只用于受信任的 .NET 扩展。加载要用运行期反射，Native AOT 应用不能使用。5.0 的进程内宿主只给插件独立的服务容器和宿主配置的只读视图，`PluginContext.Pipeline` 恒为 `null`，插件不能向宿主登记操作或中间件。

Wasm 插件在沙箱里运行：线性内存与栈有上限，按截止时间以 epoch 中断强制停下，没有网络，文件系统只有宿主映射的目录。它适合运行不可信代码，也是唯一能在管道阶段插入中间件的插件。ABI v1 只增不改，但 5.x 内不承诺不升版本（RFC-0018）。

进程外插件是独立进程，崩溃与依赖冲突都不会波及宿主，任意语言只要有 protobuf、gRPC 与 JSON 就能实现。宿主不做操作系统级沙箱，插件能做的事等同于运行它的系统账户，`fs.*:` 与 `net.outbound:` 权限只在加载时核对授予。

三种宿主都实现内核的 `IPluginHost`（`LoadAsync` / `UnloadAsync` / `Plugins`），失败以 `Result` 返回、不抛到宿主。

## 插件清单

每个插件目录放一份 `xihan.manifest.json`（RFC-0002，字段全集见[能力包与清单](./modularity)）。清单是装配的唯一事实源：宿主读它校验种类、隔离级别与权限；进程外与 Wasm 插件握手时还要把同一份清单原样发回，逐字段一致才算通过。下面是示例 `samples/EchoPlugin` 的清单：

```json
{
  "$schema": "https://spec.xihanfun.com/manifest/v1.json",
  "id": "xihan.samples.echo",
  "package": "EchoPlugin",
  "version": "5.0.0-preview.1",
  "primitives": "1",
  "stability": "experimental",
  "kind": "plugin",
  "isolation": "out-of-process",
  "requires": [ { "id": "kernel.plugins", "version": "1" } ],
  "contributes": {
    "operations": [ "echoes.create", "echoes.relay", "echoes.crash", "events.list" ],
    "config": { "section": "XiHan:Echo" },
    "health": [ "echo" ]
  },
  "permissions": [ "capability:kernel.pipeline" ],
  "activation": [ "onStartup" ],
  "entry": "EchoPlugin"
}
```

| 字段 | 在插件里的含义 |
| --- | --- |
| `kind` | 必须是 `plugin`；与宿主不匹配的种类或隔离级别以 `PLUGINS_VALIDATION_001` 拒绝 |
| `isolation` | `in-process` / `wasm` / `out-of-process` |
| `entry` | 进程内：入口类型全名，程序集按 `package` 取 `<package>.dll`；进程外：相对插件目录的可执行文件（Windows 上没有扩展名时补 `.exe`）；Wasm：相对插件目录的 `.wasm`（参考实现也接受 `.wat` 文本） |
| `contributes.operations` | 插件服务的全部操作 ID，握手声明的操作必须都在其中 |
| `contributes.config.section` | `Configure` 时宿主下发的配置节，按 `IConfiguration` 展开后序列化为 JSON 对象；没有声明为 `{}` |
| `contributes.health` | 插件自报的健康项名，未声明的健康项是握手不一致 |
| `contributes.pipeline` | 只用于 Wasm：中间件名与阶段 |
| `permissions` | 所需权限，见下一节 |
| `activation` | `onStartup`（缺省）、`onOperation:<模式>`、`onEvent:<模式>` |

## 权限：声明、授予与强制

权限是 `<种类>:<对象>` 形式的字符串，种类只有四个：`capability:`、`fs.read:`、`fs.write:`、`net.outbound:`。插件在清单里声明需要什么，宿主在配置里按清单 ID 授予；**声明了而未授予的权限一项也不能少**，宿主在运行任何插件代码之前核对，缺一项即以 `PLUGINS_DENIED_001` 拒绝加载。

```json
{
  "XiHan": {
    "Plugins": {
      "OutOfProcess": {
        "Directories": [ "plugins/echo" ],
        "Grants": { "xihan.samples.echo": [ "capability:kernel.pipeline" ] }
      },
      "Wasm": {
        "Directories": [ "plugins/echo-wasm" ],
        "Grants": { "xihan.samples.echowasm": [ "capability:kernel.pipeline" ] },
        "Mounts": { "xihan.samples.echowasm": { "/data": "/var/lib/app/echo-wasm" } }
      }
    }
  }
}
```

加载之后各宿主对权限的强制方式：

| 权限 | 进程外 | Wasm |
| --- | --- | --- |
| `capability:kernel.pipeline` | 回调 `Host.Invoke` 时判定，未授予为 gRPC `PermissionDenied`，尾部元数据 `xihan-error-code: PLUGINS_DENIED_002` | 导入 `xihan.invoke` 时判定，未授予返回 `PLUGINS_DENIED_002` 的失败状态，不陷阱 |
| `capability:messaging.queue` | 回调 `Host.Publish` 时判定，同上 | 导入 `xihan.publish` 时判定，同上 |
| `fs.read:<路径>` / `fs.write:<路径>` | 只在加载时核对授予 | 只有 `Mounts:<清单 ID>:<客体路径>` 映射了才算授予，映射的宿主目录以只读 / 可写预开放给模块 |
| `net.outbound:<目标>` | 只在加载时核对授予 | 永不授予，声明了即加载失败 |

进程内插件的权限只在加载时核对，插件可以在 `Configure` 里用 `PluginContext.Permissions.IsGranted(...)` 自查（`samples/SamplePlugin` 就这样做）。

回调以插件身份执行：主体 ID 是清单 ID、认证方案 `Plugin`（`OutOfProcessPluginHost.PluginScheme`）、没有声明；回调元数据里的 `xihan.principal` 被忽略，`xihan.tenant` 与其他已登记的 Baggage 照常采用。回调经宿主完整管道，Authorize 阶段照常按操作契约判定，所以插件主体还要在[授权](./authorization)里被授予目标操作要求的权限码。

## 发现与加载

### 进程外与 Wasm 插件按目录发现

1. 引用宿主包并启用特性：`UseFeature<OutOfProcessPluginsFeature>()` 或 `UseFeature<WasmPluginsFeature>()`。两者都依赖 `kernel.hosting@1`，分别提供 `plugins.out-of-process@1` 与 `plugins.wasm@1`。
2. 在 `XiHan:Plugins:OutOfProcess:Directories` 或 `XiHan:Plugins:Wasm:Directories` 列出插件目录（一个目录一个插件，相对路径相对当前目录），在 `Grants` 里授予权限。
3. 特性在配置阶段同步完成发现：读清单、核对权限，进程外插件启动进程、读握手行、调用 `Handshake`，Wasm 插件编译模块、校验导出与导入表、实例化并调用 `xihan_handshake`，然后核对握手声明与清单。
4. 握手声明的操作登记为代理操作，消息目录接进本地化器，订阅的主题登记事件处理器；Wasm 插件清单里的中间件按 `清单 ID:中间件名` 插进对应阶段。
5. 启动阶段启动 `onStartup` 的插件（`Configure` → `Start` → 健康监视）。懒激活（只写了 `onOperation:` / `onEvent:`）的插件在握手后停下，首次调用或投递时再启动。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Plugins.OutOfProcess;
using XiHan.Framework.Plugins.Wasm;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<OutOfProcessPluginsFeature>()
    .UseFeature<WasmPluginsFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();

// 插件声明的操作已登记为代理操作，与本地操作一样按 expose 发布路由
app.MapXiHanOperations();

app.Run();
```

发现阶段的失败分三种结果：

| 失败 | 结果 |
| --- | --- |
| 清单不合法、权限未授予、入口文件不存在 | 该插件被跳过，不进入 `IPluginHost.Plugins`，没有健康条目 |
| 握手超时、握手行不合法、声明与清单不一致、模块不合法 | 插件以 `Failed` 留在宿主里，健康条目 `plugins.<清单 ID>` 报 `Failed`，应用进入 `Degraded` |
| 插件声明的操作与应用已有操作重名 | 配置错误，应用启动失败 |

### 运行期加载与卸载

宿主同时以 `IPluginHost` 登记在容器里，可以在运行期加载与卸载：

```csharp
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;

// 运行期加载：授予由调用方给出，不读 Grants 配置
static async Task<bool> LoadEchoAsync(IPluginHost plugins, CancellationToken cancellationToken)
{
    var source = new PluginSource("plugins/echo", new PermissionGrant(["capability:kernel.pipeline"]));
    var loaded = await plugins.LoadAsync(source, cancellationToken);
    return loaded.TryGetValue(out var status) && status.State == LifecycleState.Ready;
}
```

运行期加载的代理操作只对 `IOperationInvoker` 可见：协议适配器的路由在启动时已经发布，不会为它新增 HTTP 路由或 MCP tool。卸载后代理操作仍在登记表里，调用返回 `PLUGINS_UNAVAILABLE_001`。Wasm 宿主拒绝在运行期加载贡献中间件的插件（`PLUGINS_VALIDATION_005`），因为中间件必须在管道构建前登记。同一清单 ID 重复加载为 `PLUGINS_CONFLICT_001`，卸载未加载的插件为 `PLUGINS_NOTFOUND_001`。

### 进程内宿主

`Plugins.InProcess` 没有特性入口，应用自己创建并持有 `InProcessPluginHost`：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Plugins.InProcess;
using XiHan.Framework.Profiles.Minimal;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseMinimalProfile());

var app = builder.Build();
app.UseXiHan();

// 插件目录：SamplePlugin.dll + xihan.manifest.json + 私有依赖
await using var plugins = new InProcessPluginHost(app.Configuration, app.Services.GetRequiredService<ILoggerFactory>());
var source = new PluginSource(Path.Combine(AppContext.BaseDirectory, "plugins", "sample"), new PermissionGrant(["capability:kernel.diagnostics"]));
var loaded = await plugins.LoadAsync(source, CancellationToken.None);
if (loaded.IsFailure)
{
    // 加载失败以结果返回，应用照常运行
    app.Logger.LogWarning("Plugin was not loaded: {Code}", loaded.Error!.Code);
}

await app.RunAsync();
```

宿主核对清单种类与隔离级别、权限、`package` 与 `entry`，在新的可收集加载上下文里加载 `<package>.dll`，实例化 `entry` 类型（必须实现 `IPlugin`，且自带清单的 ID 与文件一致），调用 `Configure` 后构建插件专属容器，再调用 `StartAsync`，成功即为 `Ready`。`UnloadAsync` 先调用 `StopAsync`，无论成败都释放容器并卸载加载上下文。构造函数标注了 `RequiresDynamicCode` 与 `RequiresUnreferencedCode`，项目开启裁剪或 AOT 分析时会报 IL2026 / IL3050。

写进程内插件：类库项目设 `<EnableDynamicLoading>true</EnableDynamicLoading>`，引用 `XiHan.Framework.Kernel`，实现 `IPlugin`，把 `xihan.manifest.json` 复制到输出目录。示例见 `samples/SamplePlugin`。

## 握手与生命周期

进程外插件的启动顺序（`spec/plugin/handshake.md`）：宿主只继承必要的环境变量（`PATH`、用户与临时目录、区域与时区、操作系统所需的系统变量、`DOTNET_*`、`GO*`），再追加下表的变量启动进程，工作目录是插件目录；插件启动 gRPC 服务端后向 stdout 写一行握手行 `xihan-plugin/1 <network> <address>`（`network` 为 `unix`、`tcp` 或 `npipe`，`tcp` 只能是回环地址），宿主连上后调用 `Handshake`。

| 环境变量 | 含义 |
| --- | --- |
| `XIHAN_PLUGIN_PROTOCOL_VERSIONS` | 宿主支持的协议版本，逗号分隔、升序，当前为 `1` |
| `XIHAN_PLUGIN_PRIMITIVES` | 宿主的原语层大版本 |
| `XIHAN_PLUGIN_SESSION` | 本次启动的会话令牌，回调时放在元数据 `xihan-plugin-session` |
| `XIHAN_PLUGIN_HOST_ADDRESS` | 宿主回调端点 `<network> <address>`：Windows 上是命名管道，其他系统是 Unix 域套接字 |
| `XIHAN_PLUGIN_INSTANCE` | 实例 ID |
| `XIHAN_PLUGIN_LOCALE` | 宿主默认区域 |

握手协商协议版本（取双方都支持的最高版本，没有交集时插件以 gRPC `FailedPrecondition` 拒绝）、核对原语层大版本、比对清单、交换操作声明（契约与 JSON Schema）、订阅与消息目录。stdin 在插件停止前保持打开，插件必须在 stdin 关闭时自行退出；stderr 是插件日志，宿主逐行转成来源 `plugins.<清单 ID>` 的日志，以 `{` 开头的 JSON 行按结构化日志处理。Wasm 插件没有进程与握手行，同一套消息按 proto3 JSON 经导出函数与线性内存传递（`spec/plugin/wasm.md`），环境变量只有上表里的协议版本、原语层、实例与区域四个。

生命周期状态与原语层的 `LifecycleState` 一致：

| 状态 | 进入时机 |
| --- | --- |
| `Declared` | 宿主读到清单 |
| `Resolved` | 握手成功，声明与清单一致 |
| `Configured` | `Configure` 成功 |
| `Ready` | `Start` 成功；之后按健康推送或轮询更新 |
| `Degraded` | 插件自报 `Degraded`，或任一健康项为 Degraded / Unhealthy |
| `Draining` | 宿主已发 `Stop`，等待插件完成在途调用 |
| `Stopped` | 正常停止 |
| `Failed` | 握手、配置、启动失败，进程异常退出或实例陷阱，健康自报 `Failed`，停止超时被终止 |

时限与重启的缺省值（进程外与 Wasm 相同的部分）：

| 环节 | 缺省 | 配置键 |
| --- | --- | --- |
| 握手（读到握手行并完成 `Handshake`） | 10 秒，超时 `PLUGINS_TIMEOUT_001` | `HandshakeTimeoutSeconds`（进程外） |
| `Configure` 与 `Start` 各自 | 30 秒 | `StartTimeoutSeconds`（进程外）；Wasm 的生命周期调用用 `CallTimeoutSeconds` |
| 健康 | 进程外先订阅 `WatchHealth` 推送，断开后每 10 秒一次 `Health`；Wasm 每 10 秒轮询 `xihan_health`；连续 3 次失败视为失效 | `HealthIntervalSeconds`、`HealthFailures` |
| 崩溃后重启 | 1 秒起翻倍、上限 60 秒的退避 | — |
| 重启上限 | 10 分钟窗口内 5 次，超过后保持 `Failed`，直到 `UnloadAsync` + `LoadAsync` | `RestartLimit`、`RestartWindowSeconds` |
| 停止宽限 | 进程外取 `XiHan:Kernel:StopTimeoutSeconds`（缺省 30 秒） | `StopGraceSeconds`（进程外） |

进程外插件停止时，宿主发 `Stop`（带宽限秒数与原因 `shutdown` / `unload` / `restart`），在宽限内等进程退出，超时则终止整个进程树并记为 `Failed`（`PLUGINS_TIMEOUT_001`）。宿主停止时按与登记相反的顺序停止插件。重启后是新进程、新会话令牌、重新握手，声明必须与首次一致，否则 `PLUGINS_VALIDATION_004`。

## 代理操作经完整管道

握手声明的每个操作在宿主的 `OperationRegistry` 里登记为代理操作，归属 ID 是插件清单 ID，契约（`stability`、`expose`、权限码或策略）与 Schema 取自声明。代理操作和本地[操作契约](./operations)没有区别：协议适配器按 `expose` 发布（HTTP 路由、gRPC 方法、MCP tool、命令行），调用经宿主十一个阶段的完整[管道](./pipeline)，Authorize 阶段按契约判定，Handle 阶段把 JSON 载荷与上下文发给插件。

上下文按 RFC-0003 平铺：`traceparent` / `tracestate`、截止时间（同时设为 gRPC 调用的截止时间）、只含已登记 `xihan.*` 键的 Baggage（租户、主体、关联 ID、幂等键、区域都在其中）；`Items` 与服务不传播。插件返回的业务失败原样成为调用结果，宿主用握手时拿到的消息目录翻译消息键（[国际化](./localization)）；传输层失败按下表映射：

| 插件侧 | 宿主错误码 |
| --- | --- |
| 插件未就绪、正在停止、进程已退出或不可达 | `PLUGINS_UNAVAILABLE_001` |
| 截止时间到 | `KERNEL_TIMEOUT_001` |
| 调用被取消 | `KERNEL_CANCELLED_001` |
| 插件未实现该操作（gRPC `Unimplemented`；Wasm 返回 `KERNEL_NOTFOUND_001` 状态） | `KERNEL_NOTFOUND_001` |
| 载荷或帧序不合法（gRPC `InvalidArgument`） | `KERNEL_VALIDATION_001` |
| 其他 gRPC 失败 | `PLUGINS_UNEXPECTED_001` |

协议定义了流式帧 `InvokeStream`，.NET 宿主暂不发布流式操作；Wasm 插件声明流式操作在握手时就被拒绝。

## 事件投递

握手里的每个订阅让宿主向消息代理订阅该主题，消息到达时派发器按 [消息与事件](./messaging) 的规则开新作用域、经完整管道，在 Handle 阶段把信封（CloudEvents 1.0 属性平铺，`attempt` 从 1 起）发给插件。订阅写了消息类型时只投递类型相同的消息，其余在宿主侧按成功处理。投递至少一次，插件处理器必须幂等，可用 Baggage 里的 `xihan.idempotency-key` 或信封 `id` 去重。插件返回失败或不可用时由代理按自己的重试与死信策略处理，宿主不缓冲消息。宿主要装配一个消息代理（Standard 组合已带内存默认实现）。

## 回调：Host.Invoke 与 Host.Publish

插件可以反过来调用宿主：

| 回调 | 进程外 RPC | Wasm 导入 | 权限 | 语义 |
| --- | --- | --- | --- | --- |
| 调用操作 | `Host.Invoke` | `xihan.invoke` | `capability:kernel.pipeline` | 以新的调用经宿主完整管道执行操作，未登记的操作为 `KERNEL_NOTFOUND_001` |
| 发布消息 | `Host.Publish` | `xihan.publish` | `capability:messaging.queue` | 按主题分组批量经宿主消息发布器发布，逐项返回结果；宿主没有发布器时每项为 `PLUGINS_UNAVAILABLE_001` |

进程外插件回调时必须带 `xihan-plugin-session` 元数据，令牌缺失或不匹配为 gRPC `Unauthenticated` + `PLUGINS_DENIED_002`；令牌只对本次进程有效，宿主端点只接受本机连接。Wasm 回调的嵌套深度上限 8，超过返回 `PLUGINS_UNAVAILABLE_002`；回调期间管道可能再次进入同一插件（它自己的中间件、操作或订阅），再入在同一线程上、导入函数返回前同步完成，受外层调用的截止时间约束，模块跨导入调用不得持有不可重入的锁。

## Wasm 插件贡献中间件

Wasm 插件清单 `contributes.pipeline` 的每一项在对应阶段插入一个中间件，名字为 `清单 ID:中间件名`、归属为清单 ID，阶段不能是 `handle`。每次调用宿主在进入下游前后各调一次 `xihan_middleware`：

| 阶段 | 插件应答 | 宿主行为 |
| --- | --- | --- |
| before | `CONTINUE` | 合并应答里的 Baggage（未登记的键丢弃，`xihan.principal` 不可改写）后进入下游 |
| before | `SHORT_CIRCUIT` | 把应答里的失败结果写为调用结果，不进入下游；结果缺失或不是失败按 `KERNEL_VALIDATION_001` 处理 |
| after | 任意 | 只观察下游结果，动作与 Baggage 忽略 |

中间件只看得到元数据、操作 ID 与结果，看不到也改不了载荷，需要载荷的逻辑写成操作。每次钩子调用的时限是 `MiddlewareTimeoutMilliseconds`（缺省 100 毫秒）。中间件陷阱、超时或无响应时调用以 `PLUGINS_UNAVAILABLE_001` 失败，插件按崩溃处理并重建：安全相关的阶段不能失败即放行。参考实现把每个中间件放在所在阶段的末尾，清单里的 `before` / `after` 目前不参与排序。示例 `samples/EchoWasmPlugin` 的 `echo-guard` 在 `observe` 阶段把租户 `blocked` 短路为 `ECHOWASM_DENIED_001`。

::: warning Windows 上 Wasm 宿主的可执行项目要关闭 CET 兼容
wasmtime 处理陷阱时改写线程上下文，与 Windows 用户态影子栈（CET）冲突；.NET 9 起 apphost 默认开启 CET 兼容，影子栈强制时第一次陷阱就让整个进程 fail-fast（退出码 `0xC0000409`）。宿主、测试与一致性套件的可执行项目都要设 `<CETCompat>false</CETCompat>`。`WasmPluginHost` 创建时检测进程的影子栈策略，强制时以 `PLUGINS_UNAVAILABLE_003` 抛出 `FrameworkException`。Linux 与 macOS 不受影响。
:::

## 失败降级，不让宿主崩溃

插件故障的结果是降级而不是退出：每个插件是一个健康条目 `plugins.<清单 ID>`，条目 `Failed` 或 `Degraded` 只把应用从 `Ready` 压成 `Degraded`，`/health/ready` 仍按宿主自己的就绪规则应答（[生命周期与健康](./lifecycle)）。

| 故障 | 插件状态 | 调用方看到 |
| --- | --- | --- |
| 进程异常退出 / Wasm 陷阱或导出返回 0 | `Failed`（`PLUGINS_UNEXPECTED_002`），按退避重启或重建 | 在途调用 `PLUGINS_UNAVAILABLE_001` |
| 调用超过截止时间 | Wasm 实例被 epoch 中断，记为 `Failed` 并重建；进程外插件不受影响 | `KERNEL_TIMEOUT_001` |
| 健康探测连续失败 | 进程被终止，按崩溃处理 | 重启前 `PLUGINS_UNAVAILABLE_001` |
| 重启次数超过上限 | 保持 `Failed` | `PLUGINS_UNAVAILABLE_001` |
| 沙箱资源不足（回调嵌套超限、客体内存分配失败） | 不变 | `PLUGINS_UNAVAILABLE_002` |

## 用 Go SDK 写插件

[framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) 是规范的 Go 实现，`plugin` 包提供进程外插件运行时 `plugin.Serve`：读环境变量、监听、写握手行、实现 `xihan.plugin.v1` 服务端、生命周期与健康推送、事件投递、宿主回调客户端和 stderr 结构化日志。SDK 当前为 `v0.1`，不提供流式操作，不覆盖 Wasm 载体。

1. 写清单 `xihan.manifest.json`：`kind: plugin`、`isolation: out-of-process`、`contributes.operations` 列出全部操作、`entry` 是可执行文件名。
2. 写入口，用 `operation.Add` 登记操作，交给 `plugin.Serve`：

   ```go
   package main

   import (
       "context"
       _ "embed"
       "os"

       "github.com/xihanfun/framework-sdk-go/invocation"
       "github.com/xihanfun/framework-sdk-go/operation"
       "github.com/xihanfun/framework-sdk-go/plugin"
       "github.com/xihanfun/framework-sdk-go/primitives"
   )

   //go:embed xihan.manifest.json
   var manifest []byte

   type CreateRequest struct{ Text string `json:"text"` }
   type CreateResponse struct{ Text, Tenant string }

   func main() {
       registry := operation.NewRegistry()
       _ = operation.Add(registry, operation.Contract{ID: "echoes.create", Stability: primitives.Experimental, Expose: []string{operation.ExposeHTTP}},
           operation.HandlerFunc[CreateRequest, CreateResponse](func(ctx *invocation.Context, request CreateRequest) primitives.Result[CreateResponse] {
               return primitives.Ok(CreateResponse{Text: request.Text, Tenant: ctx.Tenant()})
           }), nil, "")

       if err := plugin.Serve(context.Background(), plugin.Options{Manifest: manifest, Operations: registry}); err != nil {
           os.Exit(1)
       }
   }
   ```

3. 订阅、生命周期回调与健康项经 `plugin.Options` 的 `Subscriptions`、`Hooks`、`Catalogs` 接入；`Hooks.Configure` 里用 `configuration.Decode(&settings)` 读配置节；操作里用 `plugin.HostOf(ctx)` 回调宿主；业务失败返回 `primitives.Fail`，文案用消息键加 `plugin.LoadCatalog` 加载的 `Localization/<区域>.json`。完整写法见 SDK 的 `examples/echo`。
4. 构建并把清单放进产物目录：

   ```bash
   go build -o bin/echo-go .
   cp xihan.manifest.json bin/
   ```

5. 用规范仓库的黑盒运行器验证协议一致性（[测试与一致性套件](./testing#plugins-conformance-与-xihan-conformance)）：

   ```bash
   go install github.com/xihanfun/framework-spec/conformance/cmd/xihan-conformance@latest
   xihan-conformance plugin --dir bin --exclude echoes.crash --sample 'echoes.create={"text":"hi"}'
   ```

6. 在宿主的 `XiHan:Plugins:OutOfProcess:Directories` 里指向 `bin`，按清单在 `Grants` 里授予权限。

`samples/EchoGoPlugin` 是在 .NET 宿主里端到端运行的 Go 插件，`Plugins.OutOfProcess.Tests` 用本机的 `go` 构建它并跑一致性套件与宿主测试。.NET 写进程外插件不引用任何 XiHan 包，只需 protobuf 与 gRPC 从 `plugin.proto` 生成桩，见 `samples/EchoPlugin`；Rust 写 Wasm 插件见 `samples/EchoWasmPlugin`（`wasm32-wasip1`、`cdylib`，只依赖 `serde_json` 与 `base64`）。

## 远程 Provider

子系统能力也可以由进程外插件提供（RFC-0019）：宿主侧的 [`Caching.Remote`](../packages/caching-remote) 适配器声明 `caching.distributed@1`，插件只实现规范规定的操作集 `cache-entries.*`，示例是 Go 写的 `samples/CacheGoPlugin`。

## 相关

- [Plugins.Protocol](../packages/plugins-protocol)、[Plugins.InProcess](../packages/plugins-inprocess)、[Plugins.OutOfProcess](../packages/plugins-outofprocess)、[Plugins.Wasm](../packages/plugins-wasm)
- [Conformance 一致性套件](../packages/conformance#plugins)
- [操作契约](./operations)、[执行管道](./pipeline)、[授权](./authorization)、[消息与事件](./messaging)
- [扩展与二次开发](./extending)
- 规范：RFC-0017《进程外插件协议》、RFC-0018《Wasm 插件 ABI》、RFC-0019《远程 Provider》；ADR-0006、ADR-0009、ADR-0011
