# Wasm 插件

Wasm 插件是在宿主进程内、由 wasmtime 沙箱运行的 WebAssembly 模块。这一页讲 ABI v1 的模块形态、线性内存与 JSON 消息约定、用 Rust 写并构建一个插件、插件如何向宿主管道贡献中间件、内存与 CPU 上限和崩溃重建，以及 Windows 上宿主项目的 `CETCompat` 要求与当前限制。读完你能把一个不可信的插件放进应用，并在管道阶段插入它的逻辑。前置知识是[隔离级别与清单](/plugins/overview)与[进程外插件](/plugins/out-of-process)：Wasm 载体复用同一套 `xihan.plugin.v1` 消息。

::: warning Experimental
`Plugins.Wasm` 与 ABI v1 处于实验阶段：ABI 只增不改，但当前大版本内不承诺不升版本（规范 RFC-0018）。生产环境需要稳定 ABI 时先用进程外插件。
:::

## 为什么需要

进程外插件隔离了崩溃与依赖，但它能做的事等同于运行它的系统账户，宿主无法限制它读哪些文件、连哪些网络、吃多少内存与 CPU。要运行真正不可信的代码，需要一个有边界的沙箱：线性内存与栈有上限，CPU 按截止时间强制中断，没有网络，文件系统只有宿主映射的目录。Wasm 模块天然具备这些边界，宿主在进程内用 wasmtime 运行它，调用没有进程间通信的开销，这也让插件可以在管道阶段插入中间件，这是另外两种隔离级别做不到的。

## ABI v1：核心模块、线性内存与 JSON

插件是 WASI preview 1（`wasi_snapshot_preview1`）的 reactor 模块：导出线性内存 `memory`，可选导出 `_initialize`，导出 `_start` 的命令式模块不被接受。消息与进程外插件相同，按 proto3 JSON 映射编码为 UTF-8 文本（字段名 lowerCamelCase，`bytes` 为 base64，枚举为名字），经导出函数与线性内存传递：

- 传入：宿主调用 `xihan_alloc(len)` 取得偏移，写入请求，再调用导出函数 `(ptr, len)`；写入的内存由插件处理完请求后自行回收。
- 传出：导出函数返回 `i64`，`(ptr << 32) | len` 指向客体内存里的响应；宿主复制后调用 `xihan_free(ptr, len)`。返回 `0` 表示插件无法给出响应，宿主按崩溃处理。
- `xihan_alloc` 失败返回 `0`，宿主按 `PLUGINS_UNAVAILABLE_002` 处理。

插件实现的导出：

| 导出 | 签名 | 必需 | 消息 |
| --- | --- | --- | --- |
| `memory` | 线性内存 | 是 | — |
| `xihan_abi_version` | `() -> i32`，返回 `1` | 是 | — |
| `xihan_alloc` / `xihan_free` | `(size) -> ptr` / `(ptr, size)` | 是 | — |
| `xihan_handshake`、`xihan_configure`、`xihan_start`、`xihan_stop`、`xihan_health`、`xihan_invoke` | `(ptr: i32, len: i32) -> i64` | 是 | 与进程外插件同名 RPC 的请求与响应 |
| `xihan_handle_event` | 同上 | 声明了订阅时 | `HandleEventRequest` → `HandleEventResponse` |
| `xihan_middleware` | 同上 | 清单 `contributes.pipeline` 非空时 | `MiddlewareRequest` → `MiddlewareResponse` |

宿主提供模块名 `xihan` 下的三个导入：

| 导入 | 签名 | 权限 | 语义 |
| --- | --- | --- | --- |
| `log` | `(level: i32, ptr: i32, len: i32)` | 无 | 一行日志，`level` 0–5 为 trace 到 critical，来源 `plugins.<清单 ID>` |
| `invoke` | `(ptr: i32, len: i32) -> i64` | `capability:kernel.pipeline` | 同 `Host.Invoke`：以插件身份经宿主完整管道调用操作 |
| `publish` | `(ptr: i32, len: i32) -> i64` | `capability:messaging.queue` | 同 `Host.Publish`：按主题分组批量发布 |

模块不是 reactor、`xihan_abi_version` 不是 1、缺必需导出、签名不符或导入了宿主不提供的函数，加载失败 `PLUGINS_VALIDATION_005`。没有 `WatchHealth` 与 `InvokeStream`：健康只轮询，声明流式操作在握手时按 `PLUGINS_VALIDATION_004` 拒绝；未声明的操作由 `xihan_invoke` 返回 `KERNEL_NOTFOUND_001` 的失败状态。回调权限须在清单声明且已授予，未声明或未授予不陷阱，返回 `PLUGINS_DENIED_002`。回调受当前调用（宿主进入模块的那次导出调用）的截止时间与取消约束，到期返回 `KERNEL_TIMEOUT_001`，嵌套深度上限 8；回调期间管道可能再次进入同一插件，再入在同一线程上、导入函数返回前同步完成，模块跨导入调用不得持有不可重入的锁。WASI 的 stdout / stderr 写到插件工作目录下的 `stdout.log` / `stderr.log`，日志应走 `xihan.log`；环境变量只有 `XIHAN_PLUGIN_PRIMITIVES`、`XIHAN_PLUGIN_PROTOCOL_VERSIONS`、`XIHAN_PLUGIN_INSTANCE`、`XIHAN_PLUGIN_LOCALE`。

## 最小示例

仓库示例 `samples/EchoWasmPlugin` 用 Rust 实现（目标 `wasm32-wasip1`、`cdylib`，只依赖 `serde_json` 与 `base64`，不依赖任何 XiHan 包）。它的清单：

```json
{
  "$schema": "https://spec.xihanfun.com/manifest/v1.json",
  "id": "xihan.samples.echowasm",
  "package": "EchoWasmPlugin",
  "version": "1.0.0",
  "primitives": "1",
  "stability": "experimental",
  "kind": "plugin",
  "isolation": "wasm",
  "requires": [ { "id": "kernel.plugins", "version": "1" } ],
  "contributes": {
    "pipeline": [ { "middleware": "echo-guard", "stage": "observe" } ],
    "operations": [ "echoes.create", "echoes.relay", "echoes.trap", "echoes.spin", "events.list" ],
    "config": { "section": "XiHan:Echo" },
    "health": [ "echo" ]
  },
  "permissions": [ "capability:kernel.pipeline" ],
  "activation": [ "onStartup" ],
  "entry": "echo.wasm"
}
```

模块一侧的骨架（`src/lib.rs` 的导入与导出声明，完整实现见示例）：

```rust
#[link(wasm_import_module = "xihan")]
extern "C" {
    fn log(level: i32, ptr: i32, len: i32);
    fn invoke(ptr: i32, len: i32) -> i64;
}

#[no_mangle]
pub extern "C" fn xihan_abi_version() -> i32 {
    1
}

#[no_mangle]
pub extern "C" fn xihan_alloc(size: i32) -> i32 {
    // 用标准分配器给宿主一块可写内存，失败返回 0
    let layout = std::alloc::Layout::from_size_align(size as usize, 1).unwrap();
    unsafe { std::alloc::alloc(layout) as i32 }
}

#[no_mangle]
pub extern "C" fn xihan_free(ptr: i32, size: i32) {
    let layout = std::alloc::Layout::from_size_align(size as usize, 1).unwrap();
    unsafe { std::alloc::dealloc(ptr as *mut u8, layout) }
}

// 其余导出都是 (ptr, len) -> i64：读请求 JSON，写响应 JSON，返回 (ptr << 32) | len；
// 下面的 0 只是占位，返回 0 会被宿主按崩溃处理，实际实现要返回响应在客体内存里的位置
#[no_mangle]
pub extern "C" fn xihan_handshake(ptr: i32, len: i32) -> i64 { /* ... */ 0 }
#[no_mangle]
pub extern "C" fn xihan_configure(ptr: i32, len: i32) -> i64 { /* ... */ 0 }
#[no_mangle]
pub extern "C" fn xihan_start(ptr: i32, len: i32) -> i64 { /* ... */ 0 }
#[no_mangle]
pub extern "C" fn xihan_stop(ptr: i32, len: i32) -> i64 { /* ... */ 0 }
#[no_mangle]
pub extern "C" fn xihan_health(ptr: i32, len: i32) -> i64 { /* ... */ 0 }
#[no_mangle]
pub extern "C" fn xihan_invoke(ptr: i32, len: i32) -> i64 { /* ... */ 0 }
#[no_mangle]
pub extern "C" fn xihan_handle_event(ptr: i32, len: i32) -> i64 { /* ... */ 0 }
#[no_mangle]
pub extern "C" fn xihan_middleware(ptr: i32, len: i32) -> i64 { /* ... */ 0 }
```

构建并把产物放进插件目录（在仓库根执行）：

```bash
rustup target add wasm32-wasip1
cd framework/samples/EchoWasmPlugin
cargo build --release --target wasm32-wasip1
cp target/wasm32-wasip1/release/echo_wasm_plugin.wasm plugin/echo.wasm
```

宿主一侧装配特性、指向目录并授予权限：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Plugins.Wasm;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<WasmPluginsFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.MapXiHanOperations();
app.Run();
```

```json
{
  "XiHan": {
    "Plugins": {
      "Wasm": {
        "Directories": [ "plugins/echo-wasm" ],
        "Grants": { "xihan.samples.echowasm": [ "capability:kernel.pipeline", "config:XiHan:Echo" ] }
      }
    },
    "Echo": { "Greeting": "hello" }
  }
}
```

把 `plugin/echo.wasm` 与 `plugin/xihan.manifest.json` 放到 `plugins/echo-wasm`，启动应用：`POST /echoes` 带 `{"text":"hi"}` 得到带问候语与租户的回声；带 Baggage 租户 `blocked` 的任何调用在 Observe 阶段被中间件 `echo-guard` 短路为 `ECHOWASM_DENIED_001`；`/health/ready` 里出现 `plugins.xihan.samples.echowasm`。Windows 上的宿主项目还要先按下文关闭 CET 兼容。

## 贡献中间件

Wasm 插件清单 `contributes.pipeline` 的每一项在对应阶段插入一个中间件，名字为 `清单 ID:中间件名`、归属为清单 ID，阶段不能是 `handle`。每次调用宿主在进入下游前后各调一次 `xihan_middleware`：

| 阶段 | 插件应答 | 宿主行为 |
| --- | --- | --- |
| before | `CONTINUE` | 合并应答里的 Baggage（未登记的键丢弃，`xihan.principal` 不可改写）后进入下游 |
| before | `SHORT_CIRCUIT` | 把应答里的失败结果写为调用结果，不进入下游；结果缺失或不是失败按 `KERNEL_VALIDATION_001` 处理 |
| after | 任意 | 只观察下游结果，动作与 Baggage 忽略 |

中间件只看得到元数据、操作 ID 与结果，看不到也改不了载荷，需要载荷的逻辑写成操作。每次钩子调用的时限是 `MiddlewareTimeoutMilliseconds`（缺省 100 毫秒）。中间件陷阱、超时或无响应时调用以 `PLUGINS_UNAVAILABLE_001` 失败，插件按崩溃处理并重建：安全相关的阶段不能失败即放行。

排序按[执行管道](/startup/pipeline)的规则：每个中间件以 `<清单 ID>:<middleware>` 为名登记在所在阶段（基准位置是阶段末尾），清单项的 `before` / `after` 每一项先按本插件 `contributes.pipeline` 里的中间件名解析，否则原样作为管道里的中间件名，即另一个插件的 `<清单 ID>:<middleware>` 或只登记一次的宿主中间件类型全名。目标未登记、有歧义或跨阶段为 `KERNEL_PIPELINE_003`，成环为 `KERNEL_PIPELINE_001`：配置阶段发现的插件在宿主构建或打印管道时报出，运行期 `LoadAsync` 的插件在加载时报出、整个插件不登记。

中间件随插件的运行期加载与卸载进出管道：

- 运行期 `LoadAsync`：中间件与代理操作、声明的权限码一次登记，全进或全不进；次序约束在加载时立即核对。加载成功之后开始的调用经过它的中间件。
- `UnloadAsync`：先把中间件撤出管道，再撤回操作与权限码、停止插件。卸载之后开始的调用不再经过它，已经进入管道的调用按进入时的管道走完。
- 宿主或别的插件的中间件以次序约束引用了它（比如宿主中间件声明 `After(类型全名, "xihan.samples.echowasm:echo-guard")`）时，卸载被拒（`KERNEL_PIPELINE_003`）、什么都不撤，先撤回引用方。
- 卸载之后可以直接再加载同一插件，中间件按排序规则回到原来的位置，不用重启宿主。

## 沙箱边界：内存、CPU 上限与崩溃重建

每个插件一个实例，调用串行，并发到达的调用排队，排队时间计入截止时间。配置节 `XiHan:Plugins:Wasm`：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Directories` | 字符串数组 | 空 | 启动时装载的插件目录（含清单与模块文件） |
| `Grants:<清单 ID>` | 字符串数组 | 空授予 | 授予的 `capability:` 权限，以及清单要读的宿主配置节与绑定：`config:<配置节>`、`binding:<绑定名>`，没有授予即拒绝加载 |
| `Mounts:<清单 ID>:<客体路径>` | 字符串 | 空 | 客体路径映射到的宿主目录；清单的 `fs.read:` / `fs.write:` 只有映射了才算授予 |
| `MemoryLimitBytes` | 正整数 | 67108864（64 MiB） | 线性内存上限 |
| `StackLimitBytes` | 正整数 | 1048576（1 MiB） | Wasm 栈上限 |
| `EpochIntervalMilliseconds` | 正数 | 10 | epoch 递增间隔，即 CPU 截止的粒度 |
| `CallTimeoutSeconds` | 正数 | 30 | 生命周期调用与没有截止时间的调用的时限 |
| `MiddlewareTimeoutMilliseconds` | 正数 | 100 | 中间件钩子每次调用的时限 |
| `HealthIntervalSeconds` | 正数 | 10 | 健康轮询间隔 |
| `HealthFailures` | 正整数 | 3 | 连续多少次健康探测失败视为实例失效 |
| `RestartLimit` | 非负整数 | 5 | `RestartWindowSeconds` 内最多自动重建多少次，之后保持 `Failed` |
| `RestartWindowSeconds` | 正数 | 600 | 统计重建次数的时间窗 |
| `WorkDirectory` | 字符串 | 临时目录下的 `xihan-plugins-wasm` | 插件工作目录的根，存放 `stdout.log` / `stderr.log` |

取值不合类型与范围时，`WasmPluginOptions.FromConfiguration` 以带配置键的 `ArgumentException` 拒绝启动。CPU 按 epoch 中断强制截止：操作与事件用调用上下文的剩余时间（没有时取 `CallTimeout`），中间件用 `MiddlewareTimeout`，生命周期调用用 `CallTimeout`。

| 情形 | 结果 |
| --- | --- |
| 陷阱、导出返回 `0` | 插件 `Failed`（`PLUGINS_UNEXPECTED_002`，细节只写陷阱种类，原文进日志），按 1 秒起翻倍、上限 60 秒退避重建实例，重建后的握手声明必须与首次一致 |
| 调用超过截止时间 | 调用方得到 `KERNEL_TIMEOUT_001`，实例按崩溃重建 |
| 内存超过上限 | `memory.grow` 失败或陷阱，按崩溃重建 |
| 回调嵌套超过 8 层、客体内存分配失败 | `PLUGINS_UNAVAILABLE_002`，实例不变 |
| 中间件陷阱、超时或无响应 | 调用以 `PLUGINS_UNAVAILABLE_001` 失败，插件按崩溃重建 |
| 重建次数在窗口内超过上限 | 保持 `Failed`，直到 `UnloadAsync` + `LoadAsync` |
| 发现时清单不合法、权限未授予或未映射、模块文件不存在 | 不实例化，插件以 `Failed` 登记，不重试 |

懒激活（只写 `onOperation:` / `onEvent:`）的插件在握手后丢弃实例，首次调用时再实例化。`WasmPluginHost.FindStatus(清单 ID)` 返回 `Maybe<WasmPluginStatus>`（未加载时无值）：状态、ABI 版本、协议版本、重建次数、是否懒激活。

## Windows 上关闭 CET 兼容

::: warning 宿主的可执行项目必须设 `CETCompat=false`
wasmtime 处理陷阱时改写线程上下文，与 Windows 用户态影子栈（CET）冲突；当前 .NET 的 apphost 默认开启 CET 兼容，影子栈强制时第一次陷阱就让整个进程 fail-fast（退出码 `0xC0000409`），到不了宿主的崩溃处理。宿主应用、测试项目与跑一致性套件的项目都要设：

```xml
<PropertyGroup>
  <CETCompat>false</CETCompat>
</PropertyGroup>
```

`WasmPluginHost` 创建时检测进程的影子栈策略，强制时以 `PLUGINS_UNAVAILABLE_003` 抛出 `FrameworkException`。Linux 与 macOS 不受影响。
:::

## 限制

- ABI v1 只有核心模块：不支持组件模型 / WIT，不支持流式操作，每插件一个实例、不并发。
- 没有网络：`net.outbound:` 永不授予，声明了即加载失败。需要访问外部服务的逻辑经 `xihan.invoke` 回调宿主操作完成。
- 文件系统只有 `Mounts` 映射的预开放目录，对应清单的 `fs.read:` / `fs.write:`。
- 事件订阅与进程外插件相同（竞争或广播，至少一次投递，处理器须幂等）。
- 包不兼容 Native AOT（wasmtime 原生绑定与 protobuf 未声明 AOT 兼容）。

## 常见问题

### 中间件能改请求体或响应体吗

不能。中间件只收到元数据、操作 ID 与（after 阶段的）结果，能做的是补充或改写已登记的 Baggage、短路为失败。需要载荷的校验或改写写成操作，或在宿主侧用 .NET 中间件（[能力包与中间件](/extending/feature)）。

### 中间件超时了会放行吗

不会。钩子陷阱、超时或无响应时调用以 `PLUGINS_UNAVAILABLE_001` 失败，插件按崩溃重建。把中间件逻辑控制在 `MiddlewareTimeoutMilliseconds` 之内，重活放进操作。

### 怎么给插件一个可读写的目录

清单里声明 `fs.write:/data`，宿主配置 `Mounts:<清单 ID>:/data` 映射到宿主目录。没有映射视为未授予，加载以 `PLUGINS_DENIED_001` 拒绝。

### 测试进程在 Windows 上直接退出，退出码 0xC0000409

测试项目没有设 `<CETCompat>false</CETCompat>`。一致性套件会故意触发陷阱，影子栈强制时整个进程 fail-fast。宿主正常启动时会先以 `PLUGINS_UNAVAILABLE_003` 拒绝，但套件里的进程一样要设。

### 怎样验证插件符合 ABI

测试项目继承 `Plugins.Conformance` 的 `WasmPluginConformance`，指向插件目录，给出 `TrapOperation` 让套件验证陷阱后重建，见[一致性套件与门禁](/testing/conformance)。

## 相关包与下一步

- 包参考：[Plugins.Wasm](/packages/plugins-wasm)、[Plugins.Protocol](/packages/plugins-protocol)、[Plugins.OutOfProcess](/packages/plugins-outofprocess)、[Conformance](/packages/conformance)
- 相关章节：[隔离级别与清单](/plugins/overview)、[进程外插件](/plugins/out-of-process)、[执行管道](/startup/pipeline)、[一致性套件与门禁](/testing/conformance)
- 规范：RFC-0018《Wasm 插件 ABI》
- 下一步：[应用清单与 xihan pack](/deployment/pack)讲怎样把应用打成镜像与部署产物
