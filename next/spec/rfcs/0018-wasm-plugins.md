# RFC-0018 Wasm 插件 ABI（Experimental）

> 状态：草案 | 日期：2026-10-23 | 对应 ADR：0006（范围）；采纳后另立 ADR-0009（ABI 形态） | 参考实现：`XiHan.Framework.Plugins.Wasm`（P4-03，待实现） | 规范性文本（采纳后并入）：`spec/plugin/wasm.md`、`spec/plugin/plugin.proto`（中间件消息）、`spec/conformance/vectors/plugin/wasm-exports.json`

## 摘要

Wasm 插件是 `xihan.plugin.v1` 的第二种载体：**同一套消息（`plugin.proto`），调用约定从 gRPC 换成导出函数 + 线性内存**。插件是一个 WASI reactor 形态的核心 Wasm 模块，宿主用 wasmtime 在进程内加载，以沙箱（内存上限、CPU 截止、无环境权限）运行不可信代码；与进程外插件相比，它多了一种贡献——在管道阶段插入中间件（Envoy 过滤器模式）。5.0 以 Experimental 发布：ABI 只增不改，但不承诺 5.x 内不升版本。

## 范围

- 隔离级别 `wasm`（RFC-0002 清单的 `isolation`）；`entry` 指向插件目录里的 `.wasm` 文件（.NET 参考实现同时接受 `.wat` 文本，便于示例与测试，文本格式不是规范性内容）。
- 可贡献：操作、事件处理器、中间件。不可贡献：协议适配器、宿主、Provider。
- 任意能编译到 `wasm32-wasip1` 的语言：Rust、Go（TinyGo）、C / C++、AssemblyScript、.NET（NativeAOT-LLVM）等；只需 JSON 编解码，不需要 protobuf 运行时。

## 设计

### 1. 模块形态

- 目标 `wasm32`，WASI preview 1（`wasi_snapshot_preview1`）reactor：可选导出 `_initialize`（有则实例化后调用一次）；导出 `_start` 的命令式模块不被接受。
- 导出 `memory`（线性内存），宿主经它读写消息。
- 不启用 threads、SIMD 以外的实验性提案；bulk memory、multi-value、reference types 按 wasmtime 默认开启。
- 一个插件一个模块；宿主在加载时编译（可缓存编译结果），每个插件一个长期存活的实例（见 §5）。

### 2. 编码与内存约定

- 所有消息按 **proto3 JSON 映射**（canonical JSON）编码为 UTF-8 文本：字段名 lowerCamelCase，`bytes` 为 base64，枚举为名字，缺省值可省略。选择 JSON 而非 protobuf 二进制的理由：几乎所有 Wasm 工具链自带 JSON，而 protobuf 运行时在 Wasm 里体积大、语言覆盖不全；载荷本身（`payload`、`data`）已经是 JSON。二进制编码留给 ABI v2 的握手协商。
- 传入：宿主调用 `xihan_alloc(len)` 取得客体内存偏移，写入请求字节，再调用导出函数 `(ptr, len)`。
- 传出：导出函数返回 `i64`，高 32 位是响应在客体内存里的偏移、低 32 位是长度（`(ptr << 32) | len`）；宿主复制后调用 `xihan_free(ptr, len)`。返回 `0` 表示插件无法给出响应，宿主按 `PLUGINS_UNEXPECTED_002` 处理并重启实例。
- 宿主写入的内存由插件在处理完请求后自行回收（宿主不替它 `free`）。

### 3. 导出（插件实现）

| 导出 | 签名 | 必需 | 对应 `plugin.proto` 消息 |
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
| `xihan_middleware` | 同上 | 清单 `contributes.pipeline` 非空时 | `MiddlewareRequest` → `MiddlewareResponse`（§6 新增） |

- `xihan_abi_version` 返回宿主不支持的版本即加载失败（`PLUGINS_VALIDATION_005`）；缺少必需导出或签名不符同样失败。
- `WatchHealth` 与 `InvokeStream` 没有 Wasm 对应：健康只轮询，流式操作不支持（声明 `streaming: true` 的操作在握手时被拒绝）。
- 握手里的 `HandshakeRequest.hostEndpoint` 为空：回调不走网络，走 §4 的导入函数。

### 4. 导入（宿主提供，模块名 `xihan`）

| 导入 | 签名 | 权限 | 语义 |
| --- | --- | --- | --- |
| `log` | `(level: i32, ptr: i32, len: i32)` | 无 | 写一行日志；`level` 0–5 对应 trace / debug / information / warning / error / critical，来源 `plugins.<清单 ID>` |
| `invoke` | `(ptr: i32, len: i32) -> i64` | `capability:kernel.pipeline` | `InvokeRequest` → `InvokeResponse`，语义同 `Host.Invoke`（以插件身份经宿主完整管道） |
| `publish` | `(ptr: i32, len: i32) -> i64` | `capability:messaging.queue` | `PublishRequest` → `PublishResponse`，语义同 `Host.Publish` |

- 导入函数的响应由宿主经 `xihan_alloc` 写进客体内存并返回 `(ptr << 32) | len`（宿主函数回调客体导出是允许的，同一实例内重入）。
- 权限未授予不陷阱：返回 `Status.error = PLUGINS_DENIED_002`，与 gRPC 回调的 `PermissionDenied` 对应。
- 回调在当前调用的截止时间内完成；嵌套深度上限 8，超过返回 `PLUGINS_UNAVAILABLE_002`。
- WASI preview 1 由宿主提供：时钟、随机数；stdout / stderr 逐行转日志（规则同 `invocation.md` §6）；stdin 为空；无参数；环境变量只有 `XIHAN_PLUGIN_PRIMITIVES`、`XIHAN_PLUGIN_PROTOCOL_VERSIONS`、`XIHAN_PLUGIN_INSTANCE`、`XIHAN_PLUGIN_LOCALE`（没有会话令牌与宿主地址，它们在 Wasm 里没有意义）。
- 文件系统：清单 `fs.read:<客体路径>` / `fs.write:<客体路径>` 权限各预开放一个目录（只读 / 可写），宿主侧真实目录由宿主配置映射（`XiHan:Plugins:Wasm:Mounts:<清单 ID>:<客体路径>`），未映射视为未授予。
- 网络：ABI v1 不提供套接字；`net.outbound:` 权限宿主永不授予，声明了即加载失败（`PLUGINS_DENIED_001`）。

### 5. 实例、沙箱与生命周期

- **实例**：每插件一个 `Store` + 一个实例，调用串行（Wasm 线性内存不是线程安全的）；并发到达的调用在宿主排队，排队时间计入截止时间。多实例池是 v2 的宿主优化，不改变 ABI。
- **内存**：`MemoryLimitBytes`（默认 64 MiB）作为线性内存上限，`StackLimitBytes`（默认 1 MiB）作为 Wasm 栈上限；超限由 `memory.grow` 失败或陷阱表现，宿主把陷阱记为崩溃。
- **CPU**：宿主启用 epoch 中断，按 `EpochIntervalMilliseconds`（默认 10）递增 epoch；每次调用的截止时间换算成 epoch 配额：到达即陷阱，调用方得到 `KERNEL_TIMEOUT_001`，实例随后重建（陷阱后不能保证客体状态一致）。生命周期调用用 `CallTimeoutSeconds`（默认 30），中间件用 `MiddlewareTimeoutMilliseconds`（默认 100），操作与事件用调用上下文的截止时间（没有截止时间时取 `CallTimeoutSeconds`）。
- **状态机**与 `lifecycle.md` 相同：实例化 + `_initialize` + `xihan_handshake` 成功 → `Resolved`；`xihan_configure` → `Configured`；`xihan_start` → `Started` → `Ready`；健康按 `HealthIntervalSeconds` 轮询 `xihan_health`，连续失败达到阈值视为失效；`xihan_stop` 后丢弃实例 → `Stopped`。
- **崩溃**：任何陷阱（含栈溢出、内存超限、未处理 panic）或 `xihan_*` 返回 `0` 都是崩溃：插件 `Failed`（`PLUGINS_UNEXPECTED_002`，细节含陷阱消息）、宿主 `Degraded`、按 `lifecycle.md` §6 的退避重建实例（重建 = 重新实例化同一模块并重走握手、配置、启动，握手声明必须与首次一致）。
- **激活**：`onStartup` 加载时实例化并启动；`onOperation:` / `onEvent:` 在加载时实例化、握手取得声明后丢弃实例，首次调用或投递时再实例化（Wasm 实例化在毫秒级，懒激活成本远低于子进程）。
- 每个插件是宿主的健康条目 `plugins.<清单 ID>`（内核 `IHealthContributor`），语义同进程外插件。

### 6. 中间件贡献

清单 `contributes.pipeline[]`（`middleware`、`stage`、`before`、`after`，RFC-0002）声明的每一项在宿主管道的对应阶段插入一个中间件，归属为插件清单 ID，排序规则同 RFC-0004；`stage` 不得是 `handle`。每次调用，宿主在进入下游前后各调用一次 `xihan_middleware`：

```proto
// plugin.proto 新增（仅 wasm 隔离使用）
message MiddlewareRequest {
  string middleware = 1;              // 清单里的中间件名
  string stage = 2;                   // ingress … egress
  string phase = 3;                   // before | after
  InvocationMetadata metadata = 4;    // 追踪、截止时间、已登记 Baggage（含租户、主体、区域）
  string operation = 5;               // 操作 ID；非操作调用为空
  Status outcome = 6;                 // after：下游结果；before：空
}

message MiddlewareResponse {
  MiddlewareAction action = 1;        // CONTINUE | SHORT_CIRCUIT（只在 before 有效）
  Status outcome = 2;                 // SHORT_CIRCUIT 时的失败结果；必须 ok=false
  map<string, string> baggage = 3;    // 追加或改写的已登记 Baggage（只在 before 有效）
}

enum MiddlewareAction {
  MIDDLEWARE_ACTION_UNSPECIFIED = 0;
  MIDDLEWARE_ACTION_CONTINUE = 1;
  MIDDLEWARE_ACTION_SHORT_CIRCUIT = 2;
}
```

- `before` + `CONTINUE`：合并 `baggage`（未登记的键丢弃；`xihan.principal` 不可改写）后进入下游。
- `before` + `SHORT_CIRCUIT`：把 `outcome` 写为调用结果，不进入下游；`outcome` 不是失败或缺失时按 `KERNEL_VALIDATION_001` 处理（短路必须给出明确的失败）。
- `after`：只观察（审计、指标、日志），响应的 `action` 与 `baggage` 忽略。
- 中间件看不到请求与响应载荷（v1 范围：只有元数据、操作 ID 与结果），因此不能改写载荷；需要载荷的逻辑写成操作而不是中间件。
- **失败即关闭**：中间件陷阱、超时或返回 `0` 时，调用以 `PLUGINS_UNAVAILABLE_001` 失败（安全相关阶段不能失败即放行），插件按崩溃处理并重建；宿主 `Degraded` 直到重建成功。是否允许按贡献项配置"失败即放行"列入未决。

### 7. 发现与宿主配置

- 插件目录含 `xihan.manifest.json` 与 `.wasm`；宿主按 `XiHan:Plugins:Wasm:Directories` 发现，`Grants:<清单 ID>` 授予权限，`Mounts:<清单 ID>:<客体路径>` 映射文件系统；握手核对规则与 `handshake.md` §3 相同（清单一致、操作在 `contributes.operations`、健康项在 `contributes.health`、中间件在 `contributes.pipeline`）。
- 发现在配置阶段同步完成（与进程外插件一致），代理操作、消息目录、订阅与中间件据此登记；中间件必须在管道构建前登记，所以 Wasm 插件不支持运行期加载贡献中间件的插件（`LoadAsync` 对含 `contributes.pipeline` 的清单返回 `PLUGINS_VALIDATION_005`）。
- 编译缓存：`CacheDirectory` 非空时把编译产物按模块哈希缓存，二次加载免编译。

## 错误码

| 错误码 | 含义 |
| --- | --- |
| `PLUGINS_VALIDATION_005` | Wasm 模块不合法：不是 reactor、ABI 版本不支持、缺少必需导出或签名不符、导入了宿主不提供的函数、运行期加载含中间件贡献的插件 |
| `PLUGINS_UNAVAILABLE_002` | 沙箱资源不足：回调嵌套超限、响应内存分配失败 |

陷阱与 `0` 返回沿用 `PLUGINS_UNEXPECTED_002`；超时沿用 `KERNEL_TIMEOUT_001`；权限沿用 `PLUGINS_DENIED_001` / `PLUGINS_DENIED_002`。

## 一致性

- 向量 `conformance/vectors/plugin/wasm-exports.json`：必需 / 可选导出的名字与签名、导入表、ABI 版本。
- 套件 `WasmPluginConformance`（`Plugins.Conformance` 新增抽象类）：以模块文件为输入，核对导出表、`xihan_abi_version`、握手一致性、生命周期、未知操作返回 `KERNEL_NOTFOUND_001` 失败状态（Wasm 没有 gRPC `Unimplemented`，统一为 `Status.error`）、声明的操作都返回 `status`、中间件两阶段调用与短路、陷阱后可重建。
- 参考客体：`samples/EchoWasmPlugin`，以 WAT 文本手写（本仓库不要求 Wasm 工具链），实现 `echoes.create` 与一个 `observe` 阶段的中间件；`Plugins.Wasm.Tests` 以它验证宿主。

## 与 .NET 的映射（P4-03）

- 运行时：NuGet `Wasmtime`（wasmtime-dotnet，48.0.2，含各平台原生库）提供核心模块、`Module.FromText`、WASI 配置（参数、环境、stdio、预开放目录）、`Store.SetLimits`（内存、表、实例上限）、epoch 中断（`Config.WithEpochInterruption` + `Store.SetEpochDeadline`）与燃料；它没有组件模型的宿主侧绑定 API，这是 ABI v1 选核心模块的直接原因。
- 包拆分：把 P4-02 里与载体无关的部分从 `Plugins.OutOfProcess` 移到新包 `XiHan.Framework.Plugins.Protocol`（Ring 3）——从 `plugin.proto` 生成的消息类型（internal）、`PluginEndpoint`、`PluginProtocol`、`PluginErrors` / `PluginMessages` 与 10 种区域文案、`HandshakeValidator`、`PluginMetadata` / `PluginErrorMapper` / `PluginEnvelopeMapper` / `PluginJson`、`ProxyOperationDescriptor`、`PluginSubscription` / `ProxyOperation` / `HandshakeInfo`；`Plugins.OutOfProcess`、`Plugins.Wasm`、`Plugins.Conformance` 都依赖它（internal 部分经 `InternalsVisibleTo`）。包 ID 文法改为 `"Plugins." ( Isolation | "Protocol" | "Conformance" )`，`Plugins.Protocol` 在 Ring 3。
- `Plugins.Wasm`（Ring 5，`IsAotCompatible=false`）：`WasmPluginsFeature`（能力 `plugins.wasm@1`，配置 `XiHan:Plugins:Wasm`）、`WasmPluginHost : IPluginHost, IHealthContributor`、`WasmPluginRuntime`（模块、实例、串行调用队列、epoch 计时器、健康轮询、重建退避）、`WasmHostFunctions`（`xihan.log` / `invoke` / `publish` 与权限判定）、`WasmMiddleware : IPipelineMiddleware`（每个贡献项一个实例，`InvokeAsync` 做 before → next → after），`WasmPluginOptions`。
- 错误码常量与文案加进 `Plugins.Protocol` 的 `PluginErrors` / `PluginMessages`。

## 非目标与未决

- **非目标（v1）**：组件模型 / WIT（待 wasmtime-dotnet 提供宿主侧组件绑定后以 ABI v2 引入，届时 `xihan_abi_version` 返回 2 并以 WIT 世界 `xihan:plugin` 替代导出表）；流式操作；多实例并发；`net.outbound`（WASI 套接字属 preview 2）；中间件访问或改写载荷；热重载；燃料计量（epoch 足够）。
- **未决**：中间件是否允许按贡献项配置失败即放行；编译缓存的失效策略；是否允许插件声明所需的最小内存上限（清单字段）；`.wat` 是否进入规范（倾向不进入）。
