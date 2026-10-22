# XiHan.Framework.Plugins.Protocol

插件协议 `xihan.plugin.v1`（RFC-0017）的 .NET 类型，进程外宿主（`Plugins.OutOfProcess`）、Wasm 宿主（`Plugins.Wasm`）与一致性套件（`Plugins.Conformance`）共用；应用通常不直接引用。

## 内容

- 公开：`PluginEndpoint`（本机端点与握手行）、`PluginProtocol`（版本协商、元数据键、环境变量名）、`PluginErrors` / `PluginMessages`（`PLUGINS_*` 错误码与消息键，文案 10 种区域各一个文件）。
- internal（经 `InternalsVisibleTo` 给上述三个包）：从 `spec/plugin/plugin.proto` 生成的消息与 gRPC 桩、`HandshakeValidator`（握手响应与清单核对）、`PluginMetadata`（上下文 ↔ `InvocationMetadata`）、`PluginErrorMapper`（`Error` / `Result` ↔ 协议错误与状态，gRPC 状态映射）、`PluginEnvelopeMapper`（消息信封）、`PluginJson`（错误细节与配置节 JSON）、`ProxyOperationDescriptor` + `IPluginInvoker`（代理操作）。

## 依赖方向

Ring 3。依赖 `Kernel`、`Messaging.Abstractions`、`Localization.Abstractions`，以及 `Google.Protobuf`、`Grpc.Core.Api`（`Grpc.Tools` 只在构建期）。非 AOT。

## 状态

P4：可用。
