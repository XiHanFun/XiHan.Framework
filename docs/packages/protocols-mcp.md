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
- stdio：命令行宿主的 `mcp` 命令，stdin / stdout 每行一条 JSON-RPC 消息，日志只写 stderr；stdin 的字节按 UTF-8 解码，与控制台代码页无关（Windows 上重定向的标准输入不按 936、437 等代码页解码，中文参数不会成乱码）；答复里的非 ASCII 字符按 JSON 转义成 `\uXXXX`，只含 ASCII，按任何控制台输出代码页写出都是合法的 UTF-8；会话的凭据（`--authorization` 或 `XIHAN_AUTHORIZATION`）、租户、关联、区域与时限取自启动进程的命令行选项与环境变量，与 `invoke` 同一套映射，作用于每个 `tools/call`。凭据在每次调用的管道里认证，无效时需要认证的 tool 是 `isError`；选项不合法时退出码 64。

## 何时使用

要让 MCP 客户端（编程助手、对话应用）调用应用的业务操作时用它。HTTP 传输适合远程服务，stdio 适合客户端在本机以子进程启动的工具；要求权限的操作经 HTTP 传输时由请求头带凭据，经 stdio 时由启动参数或环境变量带凭据。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Protocols.Mcp --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。`Profiles.Full` 已包含本包。

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

命令行宿主里装配 `McpProtocolFeature` 并调用 `AddXiHanCommandLine()` 后，`mcp` 命令即可用，见[命令行宿主](/protocols/command-line)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Protocols:Mcp:ServerName` | string | 宿主的应用名 | `initialize` 答复的 `serverInfo.name` |
| `XiHan:Protocols:Mcp:ServerVersion` | string | 应用的版本 | `serverInfo.version`；缺省取入口程序集的信息版本（去掉构建元数据），不写框架版本（ADR-0209） |
| `XiHan:Protocols:Mcp:Instructions` | string | 空 | `initialize` 答复的 `instructions` |
| `XiHan:Protocols:Mcp:Tools:<操作 ID>` | string | — | 显式 tool 名，匹配 `^[A-Za-z0-9_-]{1,64}$` |
| `XiHan:Protocols:Mcp:AllowedOrigins:<序号>` | string | 空 | HTTP 入口允许的浏览器源（`scheme://host[:port]`）；请求带的 `Origin` 不在其中即 403 |
| `XiHan:Protocols:Mcp:MaxRequestBodyBytes` | 正整数 | 4194304 | HTTP 入口的请求体上限，超过即 413 |

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
| `McpProtocolOptions` | `ServerName`、`ServerVersion`、`Instructions`、`Tools`、`AllowedOrigins`、`MaxRequestBodyBytes`；`ToolNameFor(contract)`、`IsAllowedOrigin(origin)` |
| `Transports.McpEndpointRouteBuilderExtensions` | `MapXiHanMcp(pattern)`，缺省 `/mcp` |
| `Transports.McpStdioTransport` | `RunAsync(server, services, input, output, cancellationToken)`：按行读写的 stdio 传输；自己托管时按 UTF-8 构造 `input`（`new StreamReader(Console.OpenStandardInput(), Encoding.UTF8)`），不要直接用 `Console.In` |
| `Server.McpServer` | JSON-RPC 服务端：`Tools`、`FindTool`，由特性构造、交给传输处理消息；常量 `ProtocolVersion` = `2025-06-18`、`ProtocolVersionHeader` = `MCP-Protocol-Version` |
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
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project framework/samples/SimpleCli -- mcp
```

tool 描述的消息键、提示的推导表与 JSON-RPC 错误表见 [gRPC、MCP 与 CloudEvents](/protocols/mcp)。

## 行为与限制

- `tools/list` 不分页；`inputSchema` 在请求 Schema 的 `type` 不是 `object` 或没有 Schema 时为 `{"type":"object"}`，`outputSchema` 只在响应 Schema 是对象时给出。
- `tools/call` 成功时 `content[0].text` 是响应 JSON，响应是对象时同时给 `structuredContent`。
- `tools/call` 的调用来源记 `mcp` 与 tool 名，HTTP 传输时远端地址、User-Agent 与来源页沿用外层请求（ADR-0043）；stdio 的答复只含 ASCII，`mcp` 命令按 UTF-8 解码 stdin、不随控制台代码页，直接调用 `McpStdioTransport` 时自己按 UTF-8 构造读取器。
- 不开 SSE 流、不建会话（不发 `Mcp-Session-Id`），`GET` / `DELETE` 为 405；`MCP-Protocol-Version` 不受支持时 400，缺失按当前版本处理。
- HTTP 入口先校验源、内容类型与请求体大小（ADR-0199）：带 `Origin` 而不在 `AllowedOrigins` 里 403，`Content-Type` 不是 `application/json` 415，请求体超过 `MaxRequestBodyBytes` 413。
- stdio 调用以匿名主体执行，要求权限或登录的操作总是得到 `isError` 结果。

| 错误码 | 情形 | JSON-RPC `error.code` |
| --- | --- | --- |
| `PROTOCOLS_VALIDATION_001` | 请求体不是 JSON / 不是 JSON-RPC 2.0 请求 / `tools/call` 缺 `name` | -32700 / -32600 / -32602 |
| `PROTOCOLS_VALIDATION_002` | 协议版本不受支持 | -32600 |
| `PROTOCOLS_VALIDATION_003` | tool 参数不是 JSON 对象 | -32602 |
| `PROTOCOLS_VALIDATION_004` | 推导或配置的 tool 名不合法（启动失败） | — |
| `PROTOCOLS_DENIED_001` | HTTP 请求的 `Origin` 不在 `AllowedOrigins` 里（HTTP 403） | -32600 |
| `PROTOCOLS_VALIDATION_015` | HTTP 请求的 `Content-Type` 不是 `application/json`（HTTP 415） | -32600 |
| `PROTOCOLS_VALIDATION_016` | HTTP 请求体超过 `MaxRequestBodyBytes`（HTTP 413） | -32600 |
| `PROTOCOLS_NOTFOUND_001` | JSON-RPC 方法不受支持 | -32601 |
| `PROTOCOLS_NOTFOUND_002` | tool 未登记 | -32602 |
| `PROTOCOLS_CONFLICT_001` | 两个操作得到同一个 tool 名（启动失败） | — |

框架错误码在 `error.data.code` 里。

## 相关

- [gRPC、MCP 与 CloudEvents](/protocols/grpc)、[命令行宿主](/protocols/command-line)、[操作契约](/web/operations)
- [Hosting.AspNetCore](./hosting-aspnetcore)、[Hosting.CommandLine](./hosting-commandline)、[Protocols.Http](./protocols-http)
- 规范：`spec/protocol/mcp.md`《MCP 载体》、RFC-0011《操作契约与 XGI》
