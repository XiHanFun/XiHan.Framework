# XiHan.Framework.Protocols.Mcp

MCP（Model Context Protocol）协议适配器，提供 `protocols.mcp@1`：把 `Expose.Mcp` 的操作契约发布为 MCP tool，JSON-RPC 2.0，支持 Streamable HTTP 与 stdio 两种传输。适用于让大模型客户端经同一份契约调用业务操作的场景。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Protocols.Mcp` |
| 环层 | Ring 5（协议与插件） |
| 能力 | 提供 `protocols.mcp@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `McpProtocolFeature` |
| 配置节 | `XiHan:Protocols:Mcp` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Hosting.AspNetCore`、`XiHan.Framework.Hosting.CommandLine`、`XiHan.Framework.Localization.Abstractions`；共享框架 `Microsoft.AspNetCore.App` |
| AOT | 兼容（`IsAotCompatible`） |
| 稳定性 | Preview |

## 概述

MCP 协议版本 `2025-06-18`。`McpProtocolFeature` 登记选项、本包的消息目录、`McpServer` 与 `mcp` 命令；服务端在第一次解析时按登记表建 tool 表。tool 名按 `{resource}_{verb}` 推导；`tools/list` 的 `inputSchema` / `outputSchema` 就是操作的请求 / 响应 Schema，`title` / `description` 取消息目录里的 `<操作 ID>.title` / `<操作 ID>.description` 并按调用方区域翻译，`annotations` 由契约推导（只读契约只读且幂等，`delete` 破坏性，ADR-0041）。

每个 `tools/call` 以新的调用上下文、在自己的服务作用域里经完整内核管道执行，认证、按契约授权、校验与事务都与 HTTP 上相同；操作失败以 `isError: true` 的结果返回，文本是 `<错误码>: <文案>`，不是协议错误。

两种传输：

- Streamable HTTP：`MapXiHanMcp()` 映射 `POST /mcp`，一条消息一个答复，通知回 202，不开 SSE、不建会话。`/mcp` 经过 `UseXiHan()`，请求凭据在外层认证，凭据无效时整个请求 401。
- stdio：命令行宿主的 `mcp` 命令（`McpStdioCommand`），stdin / stdout 每行一条 JSON-RPC 消息，日志只写 stderr；调用不带凭据、租户与区域，以匿名主体执行。

## 何时使用

要让 MCP 客户端（编程助手、对话应用）调用应用的业务操作时用它。HTTP 传输适合远程服务，stdio 适合客户端在本机以子进程启动的工具；要求权限的操作只能经 HTTP 传输带凭据调用。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Protocols.Mcp" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Full` 已包含本包。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Mcp;
using XiHan.Framework.Protocols.Mcp.Transports;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<McpProtocolFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanMcp();      // POST /mcp
app.Run();
```

命令行宿主里装配 `McpProtocolFeature` 并调用 `AddXiHanCommandLine()` 后，`mcp` 命令即可用，见[命令行宿主](../guide/command-line#mcp-stdio)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Protocols:Mcp:ServerName` | string | 宿主的应用名 | `initialize` 答复的 `serverInfo.name` |
| `XiHan:Protocols:Mcp:ServerVersion` | string | 框架版本 | `serverInfo.version` |
| `XiHan:Protocols:Mcp:Instructions` | string | 空 | `initialize` 答复的 `instructions` |
| `XiHan:Protocols:Mcp:Tools:<操作 ID>` | string | — | 显式 tool 名，匹配 `^[A-Za-z0-9_-]{1,64}$` |

```json
{
  "XiHan": {
    "Protocols": {
      "Mcp": {
        "ServerName": "orders",
        "Instructions": "Use orders_list before placing an order.",
        "Tools": { "orders.create": "place_order" }
      }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `McpProtocolFeature` | 特性入口 |
| `McpProtocolOptions` | `ServerName`、`ServerVersion`、`Instructions`、`Tools`；`ToolNameFor(contract)` |
| `Transports.McpEndpointRouteBuilderExtensions` | `MapXiHanMcp(pattern)`，缺省 `/mcp` |
| `Transports.McpStdioCommand` | 命令行宿主的 `mcp` 命令 |
| `Transports.McpStdioTransport` | `RunAsync(server, services, input, output, cancellationToken)`：按行读写的 stdio 传输 |
| `Server.McpServer` | JSON-RPC 服务端：`HandleAsync`、`Tools`、`FindTool`；常量 `ProtocolVersion` = `2025-06-18`、`ProtocolVersionHeader` = `MCP-Protocol-Version` |
| `Server.McpReply` | 一条答复与是否为不合法消息（HTTP 400） |
| `Tools.McpTool` | 一个 tool：名字、操作描述、输入 / 输出 Schema、提示 |
| `Tools.McpToolAnnotations` | `For(contract)`：只读、破坏性、幂等提示 |
| `Tools.McpToolName`、`Tools.McpToolNameConvention` | 名字校验与推导 |
| `McpErrors` | 本包的错误码 |

## 使用示例

```bash
curl -s -X POST localhost:5000/mcp -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
curl -s -X POST localhost:5000/mcp -H "Content-Type: application/json" -H "Authorization: ApiKey dev-alice-key" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"orders_create","arguments":{"name":"widget","amount":3}}}'
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project next/samples/SimpleCli -- mcp
```

tool 描述的消息键、提示的推导表与 JSON-RPC 错误表见 [gRPC、MCP 与 CloudEvents](../guide/protocols#mcp)。

## 行为与限制

- `tools/list` 不分页；`inputSchema` 在请求 Schema 的 `type` 不是 `object` 或没有 Schema 时为 `{"type":"object"}`，`outputSchema` 只在响应 Schema 是对象时给出。
- `tools/call` 成功时 `content[0].text` 是响应 JSON，响应是对象时同时给 `structuredContent`。
- 不开 SSE 流、不建会话（不发 `Mcp-Session-Id`），`GET` / `DELETE` 为 405；`MCP-Protocol-Version` 不受支持时 400，缺失按当前版本处理。
- stdio 调用以匿名主体执行，要求权限或登录的操作总是得到 `isError` 结果。

| 错误码 | 情形 | JSON-RPC `error.code` |
| --- | --- | --- |
| `PROTOCOLS_VALIDATION_001` | 请求体不是 JSON / 不是 JSON-RPC 2.0 请求 / `tools/call` 缺 `name` | -32700 / -32600 / -32602 |
| `PROTOCOLS_VALIDATION_002` | 协议版本不受支持 | -32600 |
| `PROTOCOLS_VALIDATION_003` | tool 参数不是 JSON 对象 | -32602 |
| `PROTOCOLS_VALIDATION_004` | 推导或配置的 tool 名不合法（启动失败） | — |
| `PROTOCOLS_NOTFOUND_001` | JSON-RPC 方法不受支持 | -32601 |
| `PROTOCOLS_NOTFOUND_002` | tool 未登记 | -32602 |
| `PROTOCOLS_CONFLICT_001` | 两个操作得到同一个 tool 名（启动失败） | — |

框架错误码在 `error.data.code` 里。

## 相关

- [gRPC、MCP 与 CloudEvents](../guide/protocols)、[命令行宿主](../guide/command-line)、[操作契约](../guide/operations)
- [Hosting.AspNetCore](./hosting-aspnetcore)、[Hosting.CommandLine](./hosting-commandline)、[Protocols.Http](./protocols-http)
- 规范：`spec/protocol/mcp.md`《MCP 载体》、RFC-0011《操作契约与 XGI》
