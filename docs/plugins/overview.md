# 隔离级别与清单

插件是在运行期装载、与宿主隔离的代码。这一页讲三种隔离级别怎么选、插件清单 `xihan.manifest.json` 写什么、权限如何声明与授予、宿主怎样按目录发现插件和在运行期加载与卸载，以及进程内宿主的用法。读完你能在应用里装配一个插件宿主、授予插件权限并看懂它的失败结果。前置知识是[特性与清单](/startup/features)与[能力图谱与 Provider](/startup/capabilities)：插件与能力包共用同一份清单格式，但装配时机和信任边界不同。

## 为什么需要

能力包随应用一起编译、在启动时进入能力图谱，适合自己写的业务代码与 Provider。但第三方扩展不能随应用重新编译，不可信代码不能与宿主共享进程，别的语言写的实现没有 .NET 程序集。这些场景需要运行期按目录发现、由宿主授予权限、失败只影响自己的隔离单元。框架用 `kind: plugin` 的清单统一描述它们，三个宿主包各负责一种隔离级别，都实现内核的 `IPluginHost`（`LoadAsync` / `UnloadAsync` / `Plugins`），加载失败以 `Result` 返回而不是抛到宿主：一个插件坏了，应用最多降级为 `Degraded`，不会退出。

## 三种隔离级别

清单的 `isolation` 决定由哪个宿主包加载：

| `isolation` | 宿主包 | 载体 | 插件可贡献 | 稳定性 |
| --- | --- | --- | --- | --- |
| `in-process` | `Plugins.InProcess` | 可收集的 `AssemblyLoadContext`，插件有自己的服务容器 | 插件自己的服务与启停逻辑 | Preview |
| `wasm` | `Plugins.Wasm` | wasmtime 进程内沙箱里的 WASI preview 1 reactor 模块 | 操作、事件处理器、管道中间件 | Experimental |
| `out-of-process` | `Plugins.OutOfProcess` | 子进程 + 本机 gRPC，协议 `xihan.plugin.v1` | 操作、事件处理器 | Preview |

三者的信任边界不同，选型主要看这一点：

- 进程内插件与宿主同进程，框架与 `Microsoft.Extensions.*`、`System.*` 程序集共享，私有依赖按插件自己的 `.deps.json` 隔离解析。它能访问进程里的一切，宿主只在加载前核对权限，所以只用于受信任的 .NET 扩展；加载要用反射，Native AOT 应用不能使用。进程内宿主只给插件独立的服务容器和宿主配置的只读视图，`PluginContext.Pipeline` 恒为 `null`，插件不能向宿主登记操作或中间件。
- Wasm 插件在沙箱里运行：线性内存与栈有上限，按截止时间强制停下，没有网络，文件系统只有宿主映射的目录。它适合运行不可信代码，也是唯一能在管道阶段插入中间件的插件（[Wasm 插件](/plugins/wasm)）。
- 进程外插件是独立进程，崩溃与依赖冲突都不会波及宿主，任意语言只要有 protobuf、gRPC 与 JSON 就能实现；宿主不做操作系统级沙箱，插件能做的事等同于运行它的系统账户（[进程外插件](/plugins/out-of-process)）。

## 插件清单

每个插件目录放一份 `xihan.manifest.json`（规范 RFC-0002，字段全集见[特性与清单](/startup/features)）。清单是装配的唯一事实源：宿主读它校验种类、隔离级别与权限；进程外与 Wasm 插件握手时还要把同一份清单原样发回，逐字段一致才算通过。仓库示例 `samples/EchoPlugin` 的清单：

```json
{
  "$schema": "https://spec.xihanfun.com/manifest/v1.json",
  "id": "xihan.samples.echo",
  "package": "EchoPlugin",
  "version": "1.0.0",
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
| `entry` | 进程内：入口类型全名，程序集按 `package` 取 `<package>.dll`；进程外：相对插件目录的可执行文件（Windows 上没有扩展名时补 `.exe`）；Wasm：相对插件目录的 `.wasm` |
| `contributes.operations` | 插件服务的全部操作 ID，握手声明的操作必须都在其中 |
| `contributes.config.section` | `Configure` 时宿主下发的配置节（如 `XiHan:Echo`），序列化为 JSON 对象，运营方要在 `Grants` 里授予 `config:<配置节>` 才下发（进程内插件除外）；没有声明为 `{}` |
| `contributes.health` | 插件自报的健康项名，未声明的健康项是握手不一致 |
| `contributes.pipeline` | 只用于 Wasm：中间件名与阶段。进程外与进程内插件写了非空值，在发现或加载时以 `PLUGINS_VALIDATION_001` 拒绝，不静默忽略 |
| `contributes.permissions` | 插件定义的权限码，见下文 |
| `permissions` | 插件向宿主要的权限，见下一节 |
| `activation` | `onStartup`（缺省）、`onOperation:<模式>`、`onEvent:<模式>`；只写后两种的插件懒激活，首次调用或投递时再启动 |

## 权限：声明、授予与强制

权限是 `<种类>:<对象>` 形式的字符串，种类只有四个：`capability:`、`fs.read:`、`fs.write:`、`net.outbound:`。插件在清单里声明需要什么，宿主在配置里按清单 ID 授予；宿主在运行任何插件代码之前核对，声明了而未授予的缺一项即以 `PLUGINS_DENIED_001` 拒绝加载。

```json
{
  "XiHan": {
    "Plugins": {
      "Wasm": {
        "Directories": [ "plugins/echo-wasm" ],
        "Grants": { "xihan.samples.echowasm": [ "capability:kernel.pipeline", "config:XiHan:Echo" ] },
        "Mounts": { "xihan.samples.echowasm": { "/data": "/var/lib/app/echo-wasm" } }
      }
    }
  }
}
```

进程外宿主的 `XiHan:Plugins:OutOfProcess` 节同样有 `Directories` 与 `Grants`。加载之后，各宿主对权限的强制方式不同。回调权限按「清单声明且已被授予」判定：宿主授予了而清单没有声明的权限不生效，不能靠多授予让插件获得清单里没写的回调能力。

| 权限 | 进程外 | Wasm |
| --- | --- | --- |
| `capability:kernel.pipeline` | 回调 `Host.Invoke` 时判定，未声明或未授予为 gRPC `PermissionDenied`（`PLUGINS_DENIED_002`） | 导入 `xihan.invoke` 时判定，未声明或未授予返回 `PLUGINS_DENIED_002` 的失败状态，不陷阱 |
| `capability:messaging.queue` | 回调 `Host.Publish` 时判定，同上 | 导入 `xihan.publish` 时判定，同上 |
| `fs.read:<路径>` / `fs.write:<路径>` | 只在加载时核对授予 | 只有 `Mounts:<清单 ID>:<客体路径>` 映射了才算授予，映射的宿主目录以只读 / 可写预开放给模块 |
| `net.outbound:<目标>` | 只在加载时核对授予 | 永不授予，声明了即加载失败 |

进程内插件的权限只在加载时核对，插件可以在 `Configure` 里用 `PluginContext.Permissions.IsGranted(...)` 自查。

### 插件定义的权限码

上面的权限是插件向宿主要的能力；插件自己的操作要求调用方持有的权限码是另一回事。清单 `contributes.permissions` 写插件定义的权限码（文法同[授权与权限](/security/authorization)），进程外与 Wasm 宿主登记代理操作时替它把这些码一并登记进 `PermissionRegistry`，显示名取码本身，译文可在握手的消息目录里以 `permissions.<码>.name` / `.description` 给出：

```json
"contributes": {
  "operations": [ "echoes.create", "echoes.export" ],
  "permissions": [ "echoes.export" ]
}
```

- 只登记清单声明了的。操作的 `permission` 既不是宿主特性登记的码、也不在本插件清单里，按未登记拒绝（`KERNEL_VALIDATION_005`）；要引用宿主或别的插件定义的码，只在操作里写，不在清单里声明。操作的 `policy` 同样要存在，不存在即 `SECURITY_VALIDATION_004`。
- 声明的码与已登记的重复是冲突（`KERNEL_SECURITY_001`）：两个插件声明同一个码，后登记的整体失败；宿主特性定义的码插件又声明，不论谁先登记都失败。配置阶段的冲突是启动失败。
- 权限码与代理操作一次登记、全进或全不进；卸载时按归属一次撤回，之后可以直接再加载。
- 进程内插件有自己的容器，不向应用的登记表贡献操作，也不登记权限码。

回调以插件身份执行（主体 ID 是清单 ID、认证方案 `Plugin`）并经宿主完整管道，Authorize 阶段照常按操作契约判定，所以插件主体还要在[授权与权限](/security/authorization)里被授予目标操作要求的权限码。插件身份没有租户声明，回调与发布只能带宿主交给插件处理的调用的租户，带别的租户为 `PLUGINS_DENIED_002`（ADR-0195）。

## 最小示例

装配进程外与 Wasm 两个宿主，在配置里列出插件目录并授予权限：

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

```json
{
  "XiHan": {
    "Plugins": {
      "OutOfProcess": {
        "Directories": [ "plugins/echo" ],
        "Grants": { "xihan.samples.echo": [ "capability:kernel.pipeline", "config:XiHan:Echo" ] }
      }
    },
    "Echo": { "Greeting": "hello" }
  }
}
```

把 `samples/EchoPlugin` 的构建输出（可执行文件加 `xihan.manifest.json`）放到 `plugins/echo`，启动应用：`echoes.create` 按 HTTP 路由约定成为 `POST /echoes`，请求体 `{"text":"hi"}` 得到插件的回声；`/health/ready` 里多出健康条目 `plugins.xihan.samples.echo`；插件进程的 stderr 以同名来源进入宿主日志。两个特性分别提供 `plugins.out-of-process@1` 与 `plugins.wasm@1`。

## 发现与加载

### 进程外与 Wasm 插件按目录发现

1. 启用特性：`UseFeature<OutOfProcessPluginsFeature>()` 或 `UseFeature<WasmPluginsFeature>()`。
2. 在 `XiHan:Plugins:OutOfProcess:Directories` 或 `XiHan:Plugins:Wasm:Directories` 列出插件目录（一个目录一个插件，相对路径相对当前目录），在 `Grants` 里授予权限。
3. 特性在配置阶段同步完成发现：读清单、核对权限、启动进程或实例化模块、握手，然后核对握手声明与清单。
4. 握手声明的操作登记为代理操作，清单声明的权限码与它们一次登记，消息目录接进本地化器，订阅登记事件处理器；Wasm 插件的中间件按 `清单 ID:中间件名` 插进对应阶段。
5. 启动阶段启动 `onStartup` 的插件（`Configure` → `Start` → 健康监视）。懒激活的插件在握手后停下，首次调用或投递时再启动。

发现阶段的失败分三种结果：

| 失败 | 结果 |
| --- | --- |
| 清单不合法（含进程外插件写了 `contributes.pipeline`）、权限未授予、入口文件不存在 | 不启动插件代码，插件以 `Failed` 登记（`PLUGINS_VALIDATION_001` / `PLUGINS_DENIED_001` / `PLUGINS_NOTFOUND_001`）：进入 `IPluginHost.Plugins`、健康条目 `plugins.<清单 ID>` 报 `Failed`，应用进入 `Degraded`，宿主记错误日志；清单读不出来时健康条目为 `plugins.<目录名>`。属配置错误，不重试，可 `UnloadAsync` 后改正再 `LoadAsync` |
| 握手超时、握手行不合法、声明与清单不一致、模块不合法 | 插件以 `Failed` 留在宿主里，应用进入 `Degraded`。进程外插件按重启策略重试（1 秒起翻倍，计入 `RestartLimit`），重试成功后代理操作按运行期加载补登记；Wasm 插件的这类失败不重试 |
| 声明的权限码与已登记的重复、声明的操作与应用已有操作重名，或操作的 `permission` / `policy` 不存在 | 配置错误，应用启动失败（`KERNEL_SECURITY_001` / `KERNEL_OPERATION_001` / `KERNEL_VALIDATION_005` / `SECURITY_VALIDATION_004`）；装配完成之后才登记的插件整体登记失败 |

### 运行期加载与卸载

宿主同时以 `IPluginHost` 登记在容器里，可以在运行期加载与卸载。运行期加载的授予由调用方给出，不读 `Grants` 配置：

```csharp
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;

static async Task<bool> LoadEchoAsync(IPluginHost plugins, CancellationToken cancellationToken)
{
    var source = new PluginSource("plugins/echo", new PermissionGrant(["capability:kernel.pipeline"]));
    var loaded = await plugins.LoadAsync(source, cancellationToken);
    return loaded.TryGetValue(out var status) && status.State == LifecycleState.Ready;
}
```

- 运行期加载的代理操作只对 `IOperationInvoker` 可见：协议适配器的路由在启动时已经发布，不会为它新增 HTTP 路由或 MCP tool。
- `UnloadAsync` 先按归属（清单 ID）一次撤回代理操作与权限码，再停止插件：之后按 ID 调用这些操作返回 `KERNEL_NOTFOUND_001`，已在执行的调用照常完成或以 `PLUGINS_UNAVAILABLE_001` 结束；同一插件可以直接再 `LoadAsync`，不用重启宿主。不随卸载撤回的是配置阶段接进容器的订阅与消息目录，以及协议适配器启动时发布的路由（再加载后同一操作 ID 照常可用）。
- 同一清单 ID 重复加载为 `PLUGINS_CONFLICT_001`，卸载未加载的插件为 `PLUGINS_NOTFOUND_001`。进程外插件在 `LoadAsync` 里握手或启动失败时，错误返回给调用方，插件保持 `Failed` 并在后台按重启策略重试，不想再试就 `UnloadAsync`。

## 进程内宿主

`Plugins.InProcess` 没有特性入口，应用注册它的消息目录后自己创建并持有宿主：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Plugins.InProcess;
using XiHan.Framework.Profiles.Minimal;

var builder = WebApplication.CreateBuilder(args);
// 本包没有特性入口，失败结果的文案由宿主应用注册消息目录
builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("plugins", typeof(PluginMessages)));
builder.AddXiHan(xihan => xihan.UseMinimalProfile());

var app = builder.Build();
app.UseXiHan();

// 插件目录：SamplePlugin.dll + xihan.manifest.json + 私有依赖；加载失败以结果返回，应用照常运行
await using var plugins = new InProcessPluginHost(app.Configuration, app.Services.GetRequiredService<ILoggerFactory>());
var source = new PluginSource(Path.Combine(AppContext.BaseDirectory, "plugins", "sample"), new PermissionGrant(["capability:kernel.diagnostics"]));
var loaded = await plugins.LoadAsync(source, CancellationToken.None);
if (loaded.IsFailure)
{
    app.Logger.LogWarning("Plugin was not loaded: {Code}", loaded.Error!.Code);
}

await app.RunAsync();
```

宿主核对清单种类、隔离级别、权限、`package` 与 `entry`，在新的可收集加载上下文里加载 `<package>.dll`，实例化 `entry` 类型（必须实现 `IPlugin`，自带清单的 ID 与文件一致），调用 `Configure` 后构建插件专属容器，再调用 `StartAsync`，成功即为 `Ready`。`UnloadAsync` 先调用 `StopAsync`，无论成败都释放容器并卸载加载上下文。构造函数标注了 `RequiresDynamicCode` 与 `RequiresUnreferencedCode`，项目开启裁剪或 AOT 分析时会报 IL2026 / IL3050：进程内插件不能进 Native AOT 应用。

写进程内插件：类库项目设 `<EnableDynamicLoading>true</EnableDynamicLoading>`，引用 `XiHan.Framework.Kernel`，把 `xihan.manifest.json` 复制到输出目录，入口类型实现 `IPlugin`：`Manifest` 与文件一致（`Kind = ManifestKind.Plugin`、`Isolation = PluginIsolation.InProcess`、`Entry` 为类型全名），`Configure(PluginContext)` 里可以用 `context.Permissions.IsGranted(...)` 自检并向 `context.Services` 注册自己的服务，`StartAsync` 从插件私有容器解析它们。完整代码见 `samples/SamplePlugin` 与 [Plugins.InProcess](/packages/plugins-inprocess)。

## 常见问题

### 能力包与插件怎么选

自己写、随应用一起编译、要进入能力图谱的，写能力包（[能力包与中间件](/extending/feature)）。第三方交付、运行期才知道有没有、需要权限边界或别的语言实现的，写插件；插件不进入能力图谱，靠握手声明的操作、订阅与（Wasm 的）中间件接进宿主。

### 插件加载失败，应用会退出吗

不会。清单、权限、入口错误和握手失败都让插件以 `Failed` 登记，应用进入 `Degraded`，`/health/ready` 仍按宿主自己的就绪规则应答。只有登记冲突才让应用启动失败：权限码重复、操作重名、引用了不存在的权限码或策略。

### 授予了权限，插件回调仍被拒绝

回调权限按「清单声明且已授予」判定：先看插件清单的 `permissions` 有没有 `capability:kernel.pipeline`（或 `capability:messaging.queue`），再看宿主 `Grants:<清单 ID>` 是否授予；两边都有，再检查插件主体是否被授予了目标操作要求的权限码，因为回调经完整管道，Authorize 阶段照常判定。

### Native AOT 应用能用插件吗

不能：`Plugins.*` 包都不兼容 AOT，进程内宿主必须用反射。要在 AOT 应用里扩展能力，用能力包（[序列化与 Native AOT](/common/serialization)）。

## 相关包与下一步

- 包参考：[Plugins.Protocol](/packages/plugins-protocol)、[Plugins.InProcess](/packages/plugins-inprocess)、[Plugins.OutOfProcess](/packages/plugins-outofprocess)、[Plugins.Wasm](/packages/plugins-wasm)、[Kernel](/packages/kernel)
- 相关章节：[特性与清单](/startup/features)、[操作契约](/web/operations)、[执行管道](/startup/pipeline)、[授权与权限](/security/authorization)、[生命周期与健康](/startup/lifecycle)
- 规范：RFC-0002《清单》、RFC-0017《进程外插件协议》、RFC-0018《Wasm 插件 ABI》
- 下一步：[进程外插件](/plugins/out-of-process)
