# XiHan.Framework.Plugins.Wasm

Wasm 插件宿主（`isolation: wasm`，Experimental）：用 wasmtime 在进程内沙箱里运行 WASI preview 1 reactor 模块，消息与进程外插件同为 `xihan.plugin.v1`，按 proto3 JSON 经导出函数与线性内存传递。适用于运行不可信代码，以及需要在管道阶段插入中间件的插件。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Plugins.Wasm` |
| 环层 | Ring 5（协议与插件） |
| 能力 | 提供 `plugins.wasm@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `WasmPluginsFeature` |
| 配置节 | `XiHan:Plugins:Wasm` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Plugins.Protocol`、`XiHan.Framework.Messaging.Abstractions`、`XiHan.Framework.Localization.Abstractions`；`Wasmtime`（含各平台原生库） |
| AOT | 不兼容（wasmtime 原生绑定与 `Google.Protobuf` 都未声明 AOT 兼容） |
| 稳定性 | Experimental |

## 概述

特性在配置阶段按 `Directories` 发现插件：读 `xihan.manifest.json`，校验权限（`capability:` 看 `Grants`，`fs.read:` / `fs.write:` 看 `Mounts`，`net.outbound:` 永不授予），编译 `entry` 指向的模块（`.wasm`，参考实现也接受 `.wat` 文本），校验导出与导入表（ABI v1，`spec/plugin/wasm.md`），实例化并调用 `xihan_handshake`，核对清单。声明的操作登记为代理操作，清单 `contributes.permissions` 声明的权限码连同它们一次登记进 `PermissionRegistry`（ADR-0080），消息目录接进本地化器，订阅登记事件处理器，清单 `contributes.pipeline` 的每一项按 `清单 ID:中间件名` 插进对应阶段（不允许 `handle` 阶段）。启动阶段对 `onStartup` 的插件依次调用 `xihan_configure`、`xihan_start` 并开始健康轮询；懒激活的插件在握手后丢弃实例，首次调用时再实例化。

每个插件一个实例，调用串行，并发到达的调用排队，排队时间计入截止时间。沙箱的边界：线性内存上限 `MemoryLimitBytes`、栈上限 `StackLimitBytes`；CPU 按 epoch 中断强制截止（操作与事件用调用上下文的剩余时间，没有截止时间时取 `CallTimeout`，中间件用 `MiddlewareTimeout`，生命周期调用用 `CallTimeout`）；没有网络；文件系统只有映射的预开放目录；WASI 的 stdout / stderr 写入即丢弃（ADR-0186），日志走导入 `xihan.log`。

回调 `xihan.invoke` / `xihan.publish` 以插件身份经宿主完整管道执行，所需权限须在清单声明且已授予，只能带当前调用的租户（ADR-0195），受当前调用的截止时间与取消约束（`xihan.invoke` 新调用的截止时间取请求元数据与当前调用中较早者，`xihan.publish` 随当前调用取消，到期返回 `KERNEL_TIMEOUT_001`），嵌套深度上限 8；回调期间管道再次进入同一插件（它自己的中间件、操作或订阅）时，在同一线程上、导入函数返回前同步再入，受外层调用的截止时间约束。

## 何时使用

- 插件代码不受信任，需要内存、CPU 与文件系统边界：用本包。
- 插件要在 Authenticate、Tenant、Observe 等阶段插入逻辑（审计、租户拦截、补充 Baggage）：只有本包支持插件贡献中间件。
- 插件需要网络访问、流式操作或多实例并发：ABI v1 不支持，用 [Plugins.OutOfProcess](./plugins-outofprocess)。
- 生产环境需要稳定的 ABI：本包是 Experimental，ABI 只增不改但当前大版本内不承诺不升版本。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Plugins.Wasm --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Plugins.Wasm;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<WasmPluginsFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.Run();
```

Windows 上宿主可执行项目要关闭 CET 兼容：

```xml
<PropertyGroup>
  <!-- wasmtime 的陷阱处理与 .NET 9 起默认开启的 CET 影子栈冲突 -->
  <CETCompat>false</CETCompat>
</PropertyGroup>
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Directories` | 字符串数组 | 空 | 启动时装载的插件目录（含清单与模块文件）；相对路径相对当前目录 |
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
| `WorkDirectory` | 字符串 | 临时目录下的 `xihan-plugins-wasm-<用户名>` | 插件工作目录的根，每个插件一个子目录（Unix 上 0700），经 `Configure` 交给插件；缺省的根在 Unix 上必须是本用户私有的真实目录，否则启动插件失败（ADR-0207） |

```json
{
  "XiHan": {
    "Plugins": {
      "Wasm": {
        "Directories": [ "plugins/echo-wasm" ],
        "Grants": { "xihan.samples.echowasm": [ "capability:kernel.pipeline", "config:XiHan:Echo" ] },
        "Mounts": { "xihan.samples.echowasm": { "/data": "/var/lib/app/echo-wasm" } },
        "MemoryLimitBytes": 67108864,
        "StackLimitBytes": 1048576,
        "EpochIntervalMilliseconds": 10,
        "CallTimeoutSeconds": 30,
        "MiddlewareTimeoutMilliseconds": 100,
        "HealthIntervalSeconds": 10,
        "HealthFailures": 3,
        "RestartLimit": 5,
        "RestartWindowSeconds": 600
      }
    }
  }
}
```

取值不合表中的类型与范围（时长不是正数、内存与栈上限或 `HealthFailures` 不是正整数、`RestartLimit` 不是非负整数，含小数）时，`WasmPluginOptions.FromConfiguration` 以带配置键的 `ArgumentException` 拒绝启动；配置与属性的范围一致，`0` 不会漏到属性赋值时才失败。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `WasmPluginsFeature` | 特性入口，能力常量 `Capability`（`plugins.wasm`） |
| `WasmPluginHost` | 宿主：实现 `IPluginHost`、`IHealthContributor`、`IAsyncDisposable`；`FindStatus(manifestId)` 查找诊断快照（未加载时无值）；常量 `HealthPrefix`（`plugins.`） |
| `WasmPluginStatus` | 诊断快照：`Status`、`AbiVersion`、`ProtocolVersion`、`Restarts`、`Lazy` |
| `WasmPluginOptions` | 选项与 `FromConfiguration`、`GrantsFor`、`MountsFor`、`ResolveWorkDirectory` |

## 使用示例

插件模块的导出与导入（ABI v1）：

| 导出 | 签名 | 必需 |
| --- | --- | --- |
| `memory` | 线性内存 | 是 |
| `xihan_abi_version` | `() -> i32`，返回 `1` | 是 |
| `xihan_alloc` / `xihan_free` | `(size) -> ptr` / `(ptr, size)` | 是 |
| `xihan_handshake`、`xihan_configure`、`xihan_start`、`xihan_stop`、`xihan_health`、`xihan_invoke` | `(ptr: i32, len: i32) -> i64`，返回值高 32 位是响应在客体内存里的偏移、低 32 位是长度 | 是 |
| `xihan_handle_event` | 同上 | 声明了订阅时 |
| `xihan_middleware` | 同上 | 清单 `contributes.pipeline` 非空时 |

宿主提供模块名 `xihan` 下的三个导入：`log(level, ptr, len)`（无需权限）、`invoke(ptr, len) -> i64`（`capability:kernel.pipeline`）、`publish(ptr, len) -> i64`（`capability:messaging.queue`）。权限须在清单声明且已授予（授予多于声明的部分不生效），未声明或未授予不陷阱，返回 `PLUGINS_DENIED_002` 的失败状态。

`samples/EchoWasmPlugin` 是 Rust 写的完整示例（`wasm32-wasip1`、`cdylib`，只依赖 `serde_json` 与 `base64`），清单如下；构建产物 `plugin/echo.wasm` 随仓库提交，测试与一致性套件直接加载它：

```json
{
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

重新构建示例（在仓库根执行）：

```bash
rustup target add wasm32-wasip1
cd framework/samples/EchoWasmPlugin
cargo build --release --target wasm32-wasip1
cp target/wasm32-wasip1/release/echo_wasm_plugin.wasm plugin/echo.wasm
```

## 行为与限制

| 情形 | 结果 |
| --- | --- |
| 模块不是 reactor、`xihan_abi_version` 不是 1、缺必需导出、签名不符、导入了宿主不提供的函数 | 加载失败 `PLUGINS_VALIDATION_005` |
| 声明了流式操作 | 握手失败 `PLUGINS_VALIDATION_004` |
| 未声明的操作 | 插件返回 `KERNEL_NOTFOUND_001` 的失败状态 |
| 陷阱、导出返回 `0` | 插件 `Failed`（`PLUGINS_UNEXPECTED_002`，细节 `trap` 只写陷阱种类或异常类型，陷阱原文进日志），按 1 秒起翻倍、上限 60 秒退避重建实例 |
| 调用超过截止时间 | 调用方得到 `KERNEL_TIMEOUT_001`，实例按崩溃重建 |
| 回调嵌套超过 8 层、客体内存分配失败 | `PLUGINS_UNAVAILABLE_002` |
| 中间件陷阱、超时或无响应 | 调用以 `PLUGINS_UNAVAILABLE_001` 失败（失败即关闭），插件按崩溃重建 |
| 中间件 before 短路却没给失败结果 | 按 `KERNEL_VALIDATION_001` 处理 |
| 运行期 `LoadAsync` 含 `contributes.pipeline` 的插件 | 中间件与代理操作、声明的权限码一次登记、全进或全不进（ADR-0088）；次序约束在加载时核对，目标未登记、有歧义、跨阶段（`KERNEL_PIPELINE_003`）或成环（`KERNEL_PIPELINE_001`）时返回该错误，插件不登记、管道不变 |
| `UnloadAsync` | 先按归属撤回插件贡献的中间件、代理操作与声明的权限码，再停止实例、等在途调用离开后释放（ADR-0085、ADR-0088）：之后开始的调用不再经过它的中间件、调用它的操作返回 `KERNEL_NOTFOUND_001`；在途调用照常完成或以 `PLUGINS_UNAVAILABLE_001` 结束；同一插件可以直接再加载，中间件回到原来的位置，不用重启宿主 |
| 卸载的插件的中间件被别的中间件以次序约束引用 | 卸载被拒（`KERNEL_PIPELINE_003`），什么都不撤，插件照常服务；先撤回引用方 |
| 清单声明的权限码与已登记的重复（宿主特性定义的或另一个插件声明的）、声明的操作与已有操作重名，或 `permission` 既不是宿主已登记的、也不在本插件清单里，或 `policy` 不存在 | 配置阶段发现的启动失败（`KERNEL_SECURITY_001` / `KERNEL_OPERATION_001` / `KERNEL_VALIDATION_005`；`policy` 不存在在授权能力启动时报 `SECURITY_VALIDATION_004`）；运行期 `LoadAsync` 返回失败，一个权限码与代理操作都不留下 |
| 清单 `contributes.permissions` 不合权限码文法或有重复 | 清单读不出，按发现时被拒（`PLUGINS_VALIDATION_001`） |
| 发现时清单不合法、权限未授予或映射、模块文件不存在 | 不实例化，插件以 `Failed` 登记（可 `FindStatus`、健康条目 `plugins.<清单 ID>`，清单读不出来时为 `plugins.<目录名>`），宿主启动时记错误日志，不重试 |
| Windows 进程强制用户态影子栈 | 创建 `WasmPluginHost` 时抛出 `FrameworkException`（`PLUGINS_UNAVAILABLE_003`） |

- 重建次数在 `RestartWindowSeconds` 内超过 `RestartLimit` 后保持 `Failed`；每个插件是健康条目 `plugins.<清单 ID>`，`Failed` / `Degraded` 让应用进入 `Degraded`。
- 中间件只看得到元数据、操作 ID 与结果，看不到载荷；before 的 `CONTINUE` 可改写已登记的 Baggage（`xihan.principal` 与 `xihan.tenant` 除外），after 只观察。中间件以 `<清单 ID>:<middleware>` 为名登记在所在阶段末尾，清单的 `before` / `after` 按 RFC-0004 参与排序：先按本插件的中间件名解析，否则作为管道里的中间件名（另一个插件的 `<清单 ID>:<middleware>` 或只登记一次的宿主中间件类型全名）；目标未登记、有歧义或跨阶段为 `KERNEL_PIPELINE_003`，成环为 `KERNEL_PIPELINE_001`。
- 不负责：组件模型 / WIT（ABI v2）、流式操作、多实例并发、`net.outbound`。
- 事件订阅与进程外插件相同：握手的 `Subscription.mode` 为空或 `competing` 是竞争消费，`broadcast` 是广播（每个宿主实例都交给自己的插件实例，RFC-0009「订阅方式」）；宿主只把投递交给以同一方式订阅了主题的插件，`mode` 不合法或同一（主题，类型）以两种方式各声明一次是 `PLUGINS_VALIDATION_004`。
- 清单的 `contributes.config` 声明 `XiHan:Plugins:Wasm`。宿主另读 `XiHan:Localization:DefaultLocale`（握手时告诉插件的缺省区域），属于本地化领域，不在清单里（RFC-0002「配置节」）；每个插件收到的是它自己清单声明的配置节。
- 运行期加载时中间件经 `PipelineBuilder.UseOwned` 成组登记并立即核对次序约束，卸载时经 `PipelineBuilder.RemoveOwnedBy` 按清单 ID 撤出管道。

::: warning Windows 上可执行项目必须设 CETCompat=false
.NET 9 起 apphost 默认开启 CET 兼容，影子栈强制时 wasmtime 的第一次陷阱就让进程 fail-fast（退出码 `0xC0000409`），到不了宿主的崩溃处理。宿主应用、测试项目与跑一致性套件的项目都要设 `<CETCompat>false</CETCompat>`。
:::

## 相关

- [插件](/plugins/wasm)
- [Plugins.Protocol](./plugins-protocol)、[Plugins.OutOfProcess](./plugins-outofprocess)
- [Conformance 一致性套件](./conformance#plugins)
- 规范：RFC-0018《Wasm 插件 ABI》；ADR-0009
