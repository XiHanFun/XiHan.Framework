# Wasm 插件 ABI v1

> 规范性（Experimental）。协议 `xihan.plugin.v1`（`plugin.proto`，RFC-0017、RFC-0018）。参考实现：`XiHan.Framework.Plugins.Wasm`；一致性：`XiHan.Framework.Plugins.Conformance` 的 `WasmPluginConformance`。

Wasm 插件与进程外插件共用同一套消息；本文只定义载体：模块形态、内存约定、导出与导入、沙箱、生命周期映射与中间件钩子。

## 1. 模块形态

- 目标 `wasm32`，WASI preview 1（`wasi_snapshot_preview1`）reactor：可选导出 `_initialize`（有则实例化后调用一次）；导出 `_start` 的命令式模块不被接受。
- 必须导出线性内存 `memory`。
- 清单 `isolation` 为 `wasm`，`entry` 指向插件目录里的 `.wasm`（二进制格式）；宿主可额外接受 `.wat` 文本，文本格式不是规范性内容。
- 一个插件一个模块；宿主在加载时编译，每个插件一个长期存活的实例。

## 2. 编码与内存约定

- 所有消息按 proto3 JSON 映射编码为 UTF-8 文本：字段名 lowerCamelCase，`bytes` 为 base64，枚举为名字，缺省值可省略，未知字段忽略。
- 传入：宿主调用 `xihan_alloc(len)` 取得偏移，写入请求，再调用导出函数 `(ptr, len)`；写入的内存由插件处理完请求后自行回收。
- 传出：导出函数返回 `i64`，`(ptr << 32) | len` 指向客体内存里的响应；宿主复制后调用 `xihan_free(ptr, len)`。返回 `0` 表示插件无法给出响应，宿主按崩溃处理。
- `xihan_alloc` 失败返回 `0`，宿主按 `PLUGINS_UNAVAILABLE_002` 处理。

## 3. 导出（插件实现）

| 导出 | 签名 | 必需 | 消息 |
| --- | --- | --- | --- |
| `memory` | memory | 是 | — |
| `xihan_abi_version` | `() -> i32` | 是 | 返回 `1` |
| `xihan_alloc` | `(size: i32) -> i32` | 是 | — |
| `xihan_free` | `(ptr: i32, size: i32)` | 是 | — |
| `xihan_handshake` | `(ptr: i32, len: i32) -> i64` | 是 | `HandshakeRequest` → `HandshakeResponse` |
| `xihan_configure` | 同上 | 是 | `ConfigureRequest` → `ConfigureResponse` |
| `xihan_start` | 同上 | 是 | `StartRequest` → `StartResponse` |
| `xihan_stop` | 同上 | 是 | `StopRequest` → `StopResponse` |
| `xihan_health` | 同上 | 是 | `HealthRequest` → `HealthResponse` |
| `xihan_invoke` | 同上 | 是 | `InvokeRequest` → `InvokeResponse` |
| `xihan_handle_event` | 同上 | 声明了订阅时 | `HandleEventRequest` → `HandleEventResponse` |
| `xihan_middleware` | 同上 | 清单 `contributes.pipeline` 非空时 | `MiddlewareRequest` → `MiddlewareResponse` |

- `xihan_abi_version` 返回宿主不支持的版本、缺少必需导出、签名不符、导入了宿主不提供的函数，都是 `PLUGINS_VALIDATION_005`。
- 没有 `WatchHealth` 与 `InvokeStream`：健康只轮询；声明 `streaming: true` 的操作在握手时按 `PLUGINS_VALIDATION_004` 拒绝。
- 未声明的操作：`xihan_invoke` 返回 `Status.error` 为 `KERNEL_NOTFOUND_001` 的应答（没有 gRPC `Unimplemented`）。
- 握手请求的 `hostEndpoint` 为空：回调经 §4 的导入函数。

## 4. 导入（宿主提供，模块名 `xihan`）

| 导入 | 签名 | 权限 | 语义 |
| --- | --- | --- | --- |
| `log` | `(level: i32, ptr: i32, len: i32)` | 无 | 一行日志；`level` 0–5 = trace / debug / information / warning / error / critical；来源 `plugins.<清单 ID>` |
| `invoke` | `(ptr: i32, len: i32) -> i64` | `capability:kernel.pipeline` | `InvokeRequest` → `InvokeResponse`，语义同 `Host.Invoke` |
| `publish` | `(ptr: i32, len: i32) -> i64` | `capability:messaging.queue` | `PublishRequest` → `PublishResponse`，语义同 `Host.Publish` |

- 导入函数的响应由宿主经 `xihan_alloc` 写入客体内存并返回 `(ptr << 32) | len`。
- 权限未授予不陷阱：返回 `Status.error = PLUGINS_DENIED_002`。
- 回调在当前调用的截止时间内完成；嵌套深度上限 8，超过返回 `PLUGINS_UNAVAILABLE_002`。
- WASI：时钟与随机数可用；stdin 为空；stdout / stderr 写到插件工作目录下的 `stdout.log` / `stderr.log`（宿主不逐行转发，日志应走 `xihan.log`）；无参数；环境变量只有 `XIHAN_PLUGIN_PRIMITIVES`、`XIHAN_PLUGIN_PROTOCOL_VERSIONS`、`XIHAN_PLUGIN_INSTANCE`、`XIHAN_PLUGIN_LOCALE`。
- 文件系统：`fs.read:<客体路径>` / `fs.write:<客体路径>` 各预开放一个目录（只读 / 可写）；宿主侧目录由宿主配置映射，未映射视为未授予（`PLUGINS_DENIED_001`）。
- 网络：ABI v1 不提供套接字；`net.outbound:` 永不授予。

## 5. 实例、沙箱与生命周期

- 每插件一个实例，调用串行；并发到达的调用排队，排队时间计入截止时间。
- 内存与栈有上限（宿主配置；参考实现默认 64 MiB / 1 MiB）；超限表现为 `memory.grow` 失败或陷阱。
- CPU：宿主按截止时间中断（epoch 中断）：生命周期调用用宿主的调用时限，中间件用中间件时限，操作与事件用调用上下文的截止时间（没有时取调用时限）；中断后调用方得到 `KERNEL_TIMEOUT_001`，实例重建。
- 状态机同 `lifecycle.md`：实例化（含 `_initialize`）+ `xihan_handshake` → `Resolved`；`xihan_configure` → `Configured`；`xihan_start` → `Started` → `Ready`；健康轮询 `xihan_health`；`xihan_stop` 后丢弃实例 → `Stopped`。
- 崩溃：任何陷阱或 `xihan_*` 返回 `0`：插件 `Failed`（`PLUGINS_UNEXPECTED_002`，细节含陷阱种类），宿主 `Degraded`，按 `lifecycle.md` §6 退避重建实例并重走握手、配置、启动；重建后的握手声明必须与首次一致。
- 激活：`onStartup` 加载时实例化并启动；`onOperation:` / `onEvent:` 在加载时实例化、握手后丢弃实例，首次调用或投递时再实例化。
- 每个插件是宿主的健康条目 `plugins.<清单 ID>`。

## 6. 中间件钩子

清单 `contributes.pipeline[]`（`middleware`、`stage`、`before`、`after`）的每一项在宿主管道的对应阶段插入一个中间件，归属为插件清单 ID，排序同 RFC-0004；`stage` 不得是 `handle`。每次调用，宿主在进入下游前后各调用一次 `xihan_middleware`（消息见 `plugin.proto` 的 `MiddlewareRequest` / `MiddlewareResponse`）：

- `before` + `CONTINUE`：合并 `baggage`（未登记的键丢弃，`xihan.principal` 不可改写）后进入下游。
- `before` + `SHORT_CIRCUIT`：把 `outcome` 写为调用结果，不进入下游；`outcome` 缺失或不是失败时按 `KERNEL_VALIDATION_001` 处理。
- `after`：只观察；`action` 与 `baggage` 忽略。
- 中间件看不到请求与响应载荷，不能改写载荷。
- 失败即关闭：中间件陷阱、超时或返回 `0` 时调用以 `PLUGINS_UNAVAILABLE_001` 失败，插件按崩溃处理并重建。
- 运行期加载含 `contributes.pipeline` 的插件被拒绝（`PLUGINS_VALIDATION_005`）：中间件必须在管道构建前登记。

## 7. 一致性

- 向量 `conformance/vectors/plugin/wasm-exports.json`：ABI 版本、必需 / 可选导出的名字与签名、导入表。
- 套件 `WasmPluginConformance`：导出表与 ABI 版本、握手一致性、生命周期、未知操作返回 `KERNEL_NOTFOUND_001`、声明的操作都返回 `status`、中间件两阶段与短路、陷阱后可重建。
