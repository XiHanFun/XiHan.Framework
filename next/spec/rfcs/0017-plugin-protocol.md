# RFC-0017 进程外插件协议

> 状态：已采纳 | 日期：2026-10-23 | 对应 ADR：0006 | 参考实现：`XiHan.Framework.Plugins.OutOfProcess`（P4-02，待实现） | 规范性文本：`spec/plugin/plugin.proto`、`spec/plugin/handshake.md`、`spec/plugin/lifecycle.md`、`spec/plugin/invocation.md`

## 摘要

进程外插件是宿主启动的子进程，以 gRPC 协议 `xihan.plugin.v1` 与宿主通信：宿主调用插件的握手、配置、启停、健康、操作调用与事件投递；插件回调宿主的操作调用与消息发布。协议只搬运 XGI 的 JSON 载荷与 RFC-0001 的错误编码，不定义业务消息，因而同时是跨语言插件与远程 Provider 的基础（ADR-0006），也是 Go SDK 的第一个真实用例（ADR-0004）。

## 范围

- 隔离级别 `out-of-process`（RFC-0002 清单的 `isolation`）；`in-process` 已由 `Plugins.InProcess` 提供，`wasm` 为 Experimental，不在本文。
- 插件可贡献操作与事件处理器；不可贡献中间件（中间件在宿主进程内，跨进程调用的延迟与失败语义不适合管道阶段）。
- 任意语言：只需 protobuf、gRPC 与 JSON。

## 设计

### 传输与启动

子进程 + 本机 gRPC（Unix 域套接字、Windows 命名管道或回环 TCP）。启动参数经环境变量下发，插件以一行 stdout 握手行公布端点（go-plugin 模式），宿主随后连接并调用 `Handshake`。stderr 是插件日志。见 `handshake.md`。

### 握手

协商协议版本（整数，`1`）、核对原语层大版本、交换清单（JSON 文本，必须与目录里的文件一致）、操作声明（契约 + JSON Schema）、事件订阅与消息目录。清单仍是装配的唯一事实源：握手只能细化清单里声明过的东西（操作 ID 必须在 `contributes.operations`，健康项必须在 `contributes.health`）。

### 生命周期

状态词汇与 `LifecycleState` 相同；`Configure` → `Start` → `Ready`，`Stop` 带宽限期；健康以 `WatchHealth` 推送为主、`Health` 轮询为备；崩溃进入 `Failed` 并按退避重启，宿主进入 `Degraded` 但不退出；激活事件 `onStartup` / `onOperation:` / `onEvent:` 决定进程何时启动。见 `lifecycle.md`。

### 调用

插件操作在宿主登记为代理操作，与本地操作一样经完整管道并由协议适配器发布；Handle 阶段经 `Invoke`（一元）或 `InvokeStream`（双向帧流）执行。上下文按 RFC-0003 平铺为 `InvocationMetadata`（traceparent、截止时间、已登记 Baggage）；业务失败走 `Status.error`，gRPC 状态只表示传输失败并映射为 `PLUGINS_*` / `KERNEL_*` 错误码。事件按 RFC-0009 至少一次投递。见 `invocation.md`。

### 回调与权限

插件以会话令牌回调宿主的 `Host.Invoke` 与 `Host.Publish`，分别需要清单权限 `capability:kernel.pipeline` 与 `capability:messaging.queue`；宿主以插件身份（主体 ID = 清单 ID，方案 `Plugin`）执行，授权仍按操作契约判定。`net.outbound:` 与 `fs.*:` 权限由宿主沙箱强制，不经协议。

## 错误码

| 错误码 | 含义 |
| --- | --- |
| `PLUGINS_VALIDATION_003` | 握手行不合法，或协议版本没有交集 |
| `PLUGINS_VALIDATION_004` | 握手响应与清单不一致：原语层版本不符、清单字段不同、操作或健康项未在清单声明、声明重复 |
| `PLUGINS_DENIED_002` | 插件回调的会话令牌无效，或所需权限未授予 |
| `PLUGINS_UNAVAILABLE_001` | 插件进程未就绪、正在停止、已退出或不可达 |
| `PLUGINS_TIMEOUT_001` | 握手、配置、启动或停止超时 |
| `PLUGINS_UNEXPECTED_002` | 插件进程异常退出 |

## 一致性

- 数据向量：`conformance/vectors/plugin/handshake-lines.json`（握手行文法）、`protocol-versions.json`（版本协商）。
- gRPC 黑盒运行器（P4-04）：以本 RFC 的三份规范性文本为用例来源，对插件实现与远程 Provider 做一致性测试。

## 与 .NET 的映射（P4-02）

- `Plugins.OutOfProcess`（Ring 5）：`OutOfProcessPluginHost : IPluginHost`，进程管理、握手、健康、重启退避；代理操作以 `OperationDescriptor` 登记进 `OperationRegistry`（`DecodeRequest` / `EncodeResponse` 直通 JSON，`ExecuteAsync` 发 `Invoke`）；事件订阅经 `AddMessageHandler`。
- protobuf 线格式由 `Protocols.Grpc` 已有的编解码器按本 `.proto` 的消息描述处理，不引入 `Google.Protobuf`；其他语言用各自的 protobuf 工具链从同一份 `.proto` 生成。
- 错误码常量 `PluginErrors` 与消息键 `PluginMessages` 随包实现，文案 10 种区域各一个文件（RFC-0010）。

## 非目标

- 流式操作在 .NET 侧的契约类型（`IOperation` 的流式形态）另起 RFC；本协议先定义帧格式。
- 插件热重载、空闲停止、插件市场、资源配额（CPU / 内存）由宿主沙箱与后续 RFC 处理。
- Wasm 插件的 ABI 不在本文；`Plugins.Wasm` 以 Experimental 发布时另写 RFC。
