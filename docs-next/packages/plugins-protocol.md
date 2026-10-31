# XiHan.Framework.Plugins.Protocol

插件协议 `xihan.plugin.v1`（RFC-0017）的 .NET 类型：端点与握手行、版本协商、环境变量名、`PLUGINS_*` 错误码与消息键。进程外宿主、Wasm 宿主与插件一致性套件共用它，应用通常不直接引用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Plugins.Protocol` |
| 环层 | Ring 3（实现） |
| 能力 | — |
| 特性入口 | — |
| 配置节 | — |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Messaging.Abstractions`、`XiHan.Framework.Localization.Abstractions`；`Google.Protobuf` 3.35.1、`Grpc.Core.Api` 2.84.0（`Grpc.Tools` 2.84.0 只在构建期） |
| AOT | 不兼容（`Google.Protobuf` 的描述符反射未声明裁剪兼容） |
| 稳定性 | Preview |

## 概述

协议消息与 gRPC 桩由规范里的同一份 `spec/plugin/plugin.proto` 在构建期生成，生成的类型是 `internal`，不进公开面。握手核对（`HandshakeValidator`）、上下文与 `InvocationMetadata` 的映射、`Error` / `Result` 与协议状态的映射、消息信封映射、代理操作描述符这些载体无关的部分也在本包，经 `InternalsVisibleTo` 只给 `Plugins.OutOfProcess`、`Plugins.Wasm`、`Plugins.Conformance` 使用。

公开的只有协议常量与端点类型：写 .NET 宿主扩展、诊断工具或测试时可以用它们解析握手行、协商版本、按错误码判断插件失败。用 .NET 写进程外插件不需要引用本包，插件只要从 `plugin.proto` 生成自己的桩（见 `samples/EchoPlugin`）。

## 何时使用

- 应用只用插件：不直接引用，`Plugins.OutOfProcess` 或 `Plugins.Wasm` 会带上它。
- 要按错误码区分插件失败（如把 `PLUGINS_UNAVAILABLE_001` 映射成重试）：引用本包，用 `PluginErrors` 的常量，不手写字符串。
- 写插件宿主之外的工具（握手行检查、端点解析）：用 `PluginEndpoint` 与 `PluginProtocol`。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Plugins.Protocol" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。本包没有特性入口；两个插件宿主特性在 `Configure` 时自己登记消息目录 `PluginMessages.Catalog`（目录名 `plugins.protocol`，10 种区域各一个文件）。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `PluginEndpoint` | 本机 gRPC 端点：`Network`（`unix` / `tcp` / `npipe`）与 `Address`；`tcp` 只接受带端口的回环地址。`TryParseHandshakeLine` 解析握手行，`TryParse` 解析环境变量形式 `<network> <address>`，`ToHandshakeLine()` 生成握手行 |
| `PluginProtocol` | 协议版本 `Version`（`1`）与 `SupportedVersions`、版本协商 `Negotiate`、会话元数据键 `SessionMetadataKey`（`xihan-plugin-session`）、错误码尾部元数据键 `ErrorCodeTrailer`（`xihan-error-code`）、六个环境变量名 |
| `PluginErrors` | 进程外与 Wasm 载体共用的 `PLUGINS_*` 错误码常量 |
| `PluginMessages` | 对应的消息键常量与消息目录 `Catalog` |

`PluginProtocol` 的环境变量常量：

| 常量 | 环境变量 |
| --- | --- |
| `ProtocolVersionsVariable` | `XIHAN_PLUGIN_PROTOCOL_VERSIONS` |
| `PrimitivesVariable` | `XIHAN_PLUGIN_PRIMITIVES` |
| `SessionVariable` | `XIHAN_PLUGIN_SESSION` |
| `HostAddressVariable` | `XIHAN_PLUGIN_HOST_ADDRESS` |
| `InstanceVariable` | `XIHAN_PLUGIN_INSTANCE` |
| `LocaleVariable` | `XIHAN_PLUGIN_LOCALE` |

## 使用示例

解析插件写到 stdout 的握手行并协商版本：

```csharp
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;

if (PluginEndpoint.TryParseHandshakeLine("xihan-plugin/1 tcp 127.0.0.1:50123", out var endpoint))
{
    Console.WriteLine($"{endpoint.Network} {endpoint.Address}");
}

// 非回环的 TCP 地址不是合法端点
Console.WriteLine(PluginEndpoint.TryParseHandshakeLine("xihan-plugin/1 tcp 10.0.0.5:50123", out _));

// 取双方都支持的最高版本；没有交集为无值
var negotiated = PluginProtocol.Negotiate(PluginProtocol.SupportedVersions, [1u, 2u]);
Console.WriteLine(negotiated.HasValue);
```

按错误码区分插件不可用与业务失败：

```csharp
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Primitives;

static bool IsTransient(Error error)
    => error.Code is PluginErrors.Unavailable or PluginErrors.Crashed;
```

## 行为与限制

错误码全集（`spec/conventions/error-codes.md`）：

| 常量 | 错误码 | 含义 |
| --- | --- | --- |
| `InvalidSource` | `PLUGINS_VALIDATION_001` | 插件清单、种类或隔离级别不合法，或缺少 `entry` |
| `InvalidHandshake` | `PLUGINS_VALIDATION_003` | 握手行不合法，或协议版本没有交集 |
| `HandshakeMismatch` | `PLUGINS_VALIDATION_004` | 握手响应与清单不一致：原语层版本、清单字段、未声明或重复的操作 / 健康项 / 订阅，重启后的声明与首次不同 |
| `InvalidModule` | `PLUGINS_VALIDATION_005` | Wasm 模块不合法，或运行期加载含中间件贡献的插件 |
| `NotFound` | `PLUGINS_NOTFOUND_001` | 插件目录、清单或可执行文件不存在，或插件未加载 |
| `PermissionDenied` | `PLUGINS_DENIED_001` | 插件声明的权限未被宿主授予 |
| `CallbackDenied` | `PLUGINS_DENIED_002` | 插件回调的会话令牌无效，或所需权限未授予 |
| `AlreadyLoaded` | `PLUGINS_CONFLICT_001` | 同一清单 ID 的插件已加载 |
| `Unavailable` | `PLUGINS_UNAVAILABLE_001` | 插件未就绪、正在停止、已退出或不可达；Wasm 中间件失败即关闭 |
| `ResourceExhausted` | `PLUGINS_UNAVAILABLE_002` | Wasm 沙箱资源不足：回调嵌套超限、客体内存分配失败 |
| `HostUnsupported` | `PLUGINS_UNAVAILABLE_003` | 宿主进程不满足载体要求：Windows 强制用户态影子栈时 Wasm 陷阱会终止进程 |
| `Timeout` | `PLUGINS_TIMEOUT_001` | 握手、配置、启动或停止超时 |
| `Unexpected` | `PLUGINS_UNEXPECTED_001` | 配置、启动、停止或调用时发生未预期的失败 |
| `Crashed` | `PLUGINS_UNEXPECTED_002` | 插件进程异常退出，或 Wasm 实例陷阱、导出函数返回 0 |

`PLUGINS_UNAVAILABLE_003` 是配置错误，以 `FrameworkException` 抛出、不本地化；其余错误以 `Result` 返回，文案按调用方区域翻译。进程内宿主有自己的一组错误码常量，见 [Plugins.InProcess](./plugins-inprocess)。

## 相关

- [插件](../guide/plugins)
- [Plugins.OutOfProcess](./plugins-outofprocess)、[Plugins.Wasm](./plugins-wasm)、[Conformance 一致性套件](./conformance#plugins)
- 规范：RFC-0017《进程外插件协议》、RFC-0018《Wasm 插件 ABI》
