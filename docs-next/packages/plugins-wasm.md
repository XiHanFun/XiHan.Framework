# XiHan.Framework.Plugins.Wasm

Wasm 插件宿主（`isolation: wasm`，Experimental）：用 wasmtime 在进程内沙箱里运行 WASI preview 1 reactor 模块，消息与进程外插件同为 `xihan.plugin.v1`，按 proto3 JSON 经导出函数与线性内存传递。适用于运行不可信代码，以及需要在管道阶段插入中间件的插件。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Plugins.Wasm` |
| 环层 | Ring 5（协议与插件） |
| 能力 | 提供 `plugins.wasm@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `WasmPluginsFeature` |
| 配置节 | `XiHan:Plugins:Wasm` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Plugins.Protocol`、`XiHan.Framework.Messaging.Abstractions`、`XiHan.Framework.Localization.Abstractions`；`Wasmtime` 48.0.2（含各平台原生库） |
| AOT | 不兼容（wasmtime 原生绑定与 `Google.Protobuf` 都未声明 AOT 兼容） |
| 稳定性 | Experimental |

## 概述

特性在配置阶段按 `Directories` 发现插件：读 `xihan.manifest.json`，校验权限（`capability:` 看 `Grants`，`fs.read:` / `fs.write:` 看 `Mounts`，`net.outbound:` 永不授予），编译 `entry` 指向的模块（`.wasm`，参考实现也接受 `.wat` 文本），校验导出与导入表（ABI v1，`spec/plugin/wasm.md`），实例化并调用 `xihan_handshake`，核对清单。声明的操作登记为代理操作，消息目录接进本地化器，订阅登记事件处理器，清单 `contributes.pipeline` 的每一项按 `清单 ID:中间件名` 插进对应阶段（不允许 `handle` 阶段）。启动阶段对 `onStartup` 的插件依次调用 `xihan_configure`、`xihan_start` 并开始健康轮询；懒激活的插件在握手后丢弃实例，首次调用时再实例化。

每个插件一个实例，调用串行，并发到达的调用排队，排队时间计入截止时间。沙箱的边界：线性内存上限 `MemoryLimitBytes`、栈上限 `StackLimitBytes`；CPU 按 epoch 中断强制截止（操作与事件用调用上下文的剩余时间，没有截止时间时取 `CallTimeout`，中间件用 `MiddlewareTimeout`，生命周期调用用 `CallTimeout`）；没有网络；文件系统只有映射的预开放目录；WASI 的 stdout / stderr 写到插件工作目录下的 `stdout.log` / `stderr.log`，日志应走导入 `xihan.log`。

回调 `xihan.invoke` / `xihan.publish` 以插件身份经宿主完整管道执行，按权限判定，嵌套深度上限 8；回调期间管道再次进入同一插件（它自己的中间件、操作或订阅）时，在同一线程上、导入函数返回前同步再入，受外层调用的截止时间约束。

## 何时使用

- 插件代码不受信任，需要内存、CPU 与文件系统边界：用本包。
- 插件要在 Authenticate、Tenant、Observe 等阶段插入逻辑（审计、租户拦截、补充 Baggage）：只有本包支持插件贡献中间件。
- 插件需要网络访问、流式操作或多实例并发：ABI v1 不支持，用 [Plugins.OutOfProcess](./plugins-outofprocess)。
- 生产环境需要稳定的 ABI：本包是 Experimental，ABI 只增不改但 5.x 内不承诺不升版本。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Plugins.Wasm" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

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
| `Grants:<清单 ID>` | 字符串数组 | 空授予 | 授予的 `capability:` 权限 |
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
| `WorkDirectory` | 字符串 | 临时目录下的 `xihan-plugins-wasm` | 插件工作目录的根，每个插件一个子目录，存放 `stdout.log` / `stderr.log` |

```json
{
  "XiHan": {
    "Plugins": {
      "Wasm": {
        "Directories": [ "plugins/echo-wasm" ],
        "Grants": { "xihan.samples.echowasm": [ "capability:kernel.pipeline" ] },
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

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `WasmPluginsFeature` | 特性入口，能力常量 `Capability`（`plugins.wasm`） |
| `WasmPluginHost` | 宿主：实现 `IPluginHost`、`IHealthContributor`、`IAsyncDisposable`；`Describe(manifestId)` 返回诊断快照；常量 `HealthPrefix`（`plugins.`） |
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

宿主提供模块名 `xihan` 下的三个导入：`log(level, ptr, len)`（无需权限）、`invoke(ptr, len) -> i64`（`capability:kernel.pipeline`）、`publish(ptr, len) -> i64`（`capability:messaging.queue`）。权限未授予不陷阱，返回 `PLUGINS_DENIED_002` 的失败状态。

`samples/EchoWasmPlugin` 是 Rust 写的完整示例（`wasm32-wasip1`、`cdylib`，只依赖 `serde_json` 与 `base64`），清单如下；构建产物 `plugin/echo.wasm` 随仓库提交，测试与一致性套件直接加载它：

```json
{
  "id": "xihan.samples.echowasm",
  "package": "EchoWasmPlugin",
  "version": "5.0.0-preview.1",
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
cd next/samples/EchoWasmPlugin
cargo build --release --target wasm32-wasip1
cp target/wasm32-wasip1/release/echo_wasm_plugin.wasm plugin/echo.wasm
```

## 行为与限制

| 情形 | 结果 |
| --- | --- |
| 模块不是 reactor、`xihan_abi_version` 不是 1、缺必需导出、签名不符、导入了宿主不提供的函数 | 加载失败 `PLUGINS_VALIDATION_005` |
| 声明了流式操作 | 握手失败 `PLUGINS_VALIDATION_004` |
| 未声明的操作 | 插件返回 `KERNEL_NOTFOUND_001` 的失败状态 |
| 陷阱、导出返回 `0` | 插件 `Failed`（`PLUGINS_UNEXPECTED_002`），按 1 秒起翻倍、上限 60 秒退避重建实例 |
| 调用超过截止时间 | 调用方得到 `KERNEL_TIMEOUT_001`，实例按崩溃重建 |
| 回调嵌套超过 8 层、客体内存分配失败 | `PLUGINS_UNAVAILABLE_002` |
| 中间件陷阱、超时或无响应 | 调用以 `PLUGINS_UNAVAILABLE_001` 失败（失败即关闭），插件按崩溃重建 |
| 中间件 before 短路却没给失败结果 | 按 `KERNEL_VALIDATION_001` 处理 |
| 运行期 `LoadAsync` 含 `contributes.pipeline` 的插件 | `PLUGINS_VALIDATION_005` |
| Windows 进程强制用户态影子栈 | 创建 `WasmPluginHost` 时抛出 `FrameworkException`（`PLUGINS_UNAVAILABLE_003`） |

- 重建次数在 `RestartWindowSeconds` 内超过 `RestartLimit` 后保持 `Failed`；每个插件是健康条目 `plugins.<清单 ID>`，`Failed` / `Degraded` 让应用进入 `Degraded`。
- 中间件只看得到元数据、操作 ID 与结果，看不到载荷；before 的 `CONTINUE` 可改写已登记的 Baggage（`xihan.principal` 除外），after 只观察。参考实现把中间件放在所在阶段末尾，清单里的 `before` / `after` 目前不参与排序。
- 不负责：组件模型 / WIT（ABI v2）、流式操作、多实例并发、`net.outbound`。

::: warning Windows 上可执行项目必须设 CETCompat=false
.NET 9 起 apphost 默认开启 CET 兼容，影子栈强制时 wasmtime 的第一次陷阱就让进程 fail-fast（退出码 `0xC0000409`），到不了宿主的崩溃处理。宿主应用、测试项目与跑一致性套件的项目都要设 `<CETCompat>false</CETCompat>`。
:::

## 相关

- [插件](../guide/plugins#wasm-插件贡献中间件)
- [Plugins.Protocol](./plugins-protocol)、[Plugins.OutOfProcess](./plugins-outofprocess)
- [Conformance 一致性套件](./conformance#plugins)
- 规范：RFC-0018《Wasm 插件 ABI》；ADR-0009
