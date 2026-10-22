# ADR-0009 Wasm 插件 ABI v1 取核心模块 + 线性内存 + JSON，组件模型留给 v2

## 状态

已采纳

## 日期

2026-10-23

## 背景

ADR-0006 把 `Plugins.Wasm` 定为 5.0 的 Experimental 隔离级别。Wasm 插件要与进程外插件共用 `xihan.plugin.v1` 的消息，并额外能在管道阶段贡献中间件。需要决定模块形态（核心模块还是组件模型）、消息编码（protobuf 二进制还是 JSON）与宿主运行时。

## 备选

1. WASI 0.2 组件模型 + WIT 世界：ABI 由工具链生成，语言绑定最规范。
2. WASI preview 1 核心模块 + 自定义导出表 + 线性内存传参，消息按 proto3 JSON 映射编码。
3. 备选 2 但消息按 protobuf 二进制编码。

## 决策

采用备选 2（RFC-0018）。宿主运行时为 wasmtime（NuGet `Wasmtime` 48.x），模块是 WASI preview 1 reactor，导出 `xihan_<call>(ptr, len) -> i64` 与 `xihan_alloc` / `xihan_free`，宿主导入 `xihan.log` / `invoke` / `publish`；消息是 `plugin.proto` 的 proto3 JSON；每插件单实例、调用串行；中间件按 Envoy 过滤器模式两阶段调用且失败即关闭。`xihan_abi_version` 返回 1；组件模型待 wasmtime-dotnet 提供宿主侧组件绑定后以 ABI v2 引入。

## 理由

- wasmtime-dotnet 48 提供核心模块、WASI 配置、内存与栈上限、epoch 中断，但没有组件模型的宿主侧绑定 API；备选 1 现在无法在 .NET 宿主里实现。
- 几乎所有 Wasm 工具链自带 JSON，protobuf 运行时在 Wasm 里体积大、语言覆盖不全；载荷本身已是 JSON，二进制编码收益小。
- 单实例串行与失败即关闭是最保守的语义：不可信代码既不能并发破坏线性内存，也不能靠失败绕过安全阶段。

## 影响

- 新增规范性文本 `spec/plugin/wasm.md`、`plugin.proto` 的中间件消息、向量 `wasm-exports.json`。
- `Plugins.OutOfProcess` 中与载体无关的部分抽成 `Plugins.Protocol`（Ring 3），包 ID 文法加 `Plugins.Protocol`。
- ABI v1 只增不改；升到 v2 时 `xihan_abi_version` 返回 2，v1 模块继续可加载。
- Windows 宿主的可执行项目必须设置 `<CETCompat>false</CETCompat>`：wasmtime 的陷阱处理与 .NET 9 起默认开启的 CET 影子栈冲突，否则陷阱让进程 fail-fast；宿主创建时检测并以 `PLUGINS_UNAVAILABLE_003` 拒绝。
