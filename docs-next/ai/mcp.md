# MCP Server

本页说明怎样把基于 5.0 的应用作为 MCP 服务端接入 Claude Code、Cursor、VS Code、Codex 等 AI 工具，让 AI 工具在开发过程中直接调用应用的操作。5.0 目前没有提供文档知识库的 MCP 服务端，4.x 的 `xihan-framework mcp` 不适用于 5.0。

## 什么是 MCP

MCP（Model Context Protocol）是 AI 工具调用外部工具的开放协议。`Protocols.Mcp` 把应用里标记了 `Expose.Mcp` 的操作发布为 MCP tool：tool 名是 `{resource}_{verb}`（`orders.create` → `orders_create`），输入与输出的 JSON Schema 取自操作契约，标题与描述取自消息目录里的 `<操作 ID>.title` / `<操作 ID>.description`。`tools/call` 以一次独立调用经过完整的内核管道，认证、授权、校验、事务与普通调用一致。协议细节见 [gRPC、MCP 与 CloudEvents](../guide/protocols)。

## 两种传输

| 传输 | 启用方式 | 认证 | 适用 |
| --- | --- | --- | --- |
| Streamable HTTP | Web 应用加入 `McpProtocolFeature`，`app.MapXiHanMcp()`，端点 `POST /mcp` | 请求头 `Authorization` 由 `UseXiHan()` 在外层认证 | 需要身份与权限的操作；连接已在运行的开发服务器 |
| stdio | 命令行宿主加入 `McpProtocolFeature` 后运行 `mcp` 命令，AI 工具以子进程方式启动 | 没有凭据，调用是匿名的 | 不要求权限的操作，如本地工具类应用 |

HTTP 传输的 Web 应用：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Mcp;
using XiHan.Framework.Protocols.Mcp.Transports;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<McpProtocolFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanMcp();   // POST /mcp
app.Run();
```

stdio 传输的命令行应用（仓库示例 `next/samples/SimpleCli`）：

```csharp
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.Mcp;

var builder = Host.CreateApplicationBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseMinimalProfile()
    .UseFeature<McpProtocolFeature>());
builder.Services.AddXiHanCommandLine();

return await builder.Build().RunXiHanCommandLineAsync(args);
```

验证 stdio 服务端：

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project next/samples/SimpleCli -- mcp
```

stdio 传输下 stdout 每行一条 JSON-RPC 消息，日志只写 stderr。

## 在各工具中配置

以下示例中，HTTP 端点假定应用运行在 `http://localhost:5000`，`dev-alice-key` 是 `Security.InMemory` 在开发配置里登记的 ApiKey；stdio 示例以 `dotnet run` 启动项目。

### Claude Code

项目根目录的 `.mcp.json`：

```json
{
  "mcpServers": {
    "shop": {
      "type": "http",
      "url": "http://localhost:5000/mcp",
      "headers": { "Authorization": "ApiKey dev-alice-key" }
    },
    "shop-cli": {
      "command": "dotnet",
      "args": ["run", "--project", "src/Shop.Cli", "--", "mcp"]
    }
  }
}
```

### Cursor

项目的 `.cursor/mcp.json`：

```json
{
  "mcpServers": {
    "shop": {
      "url": "http://localhost:5000/mcp",
      "headers": { "Authorization": "ApiKey dev-alice-key" }
    }
  }
}
```

### VS Code

项目的 `.vscode/mcp.json`：

```json
{
  "servers": {
    "shop": {
      "type": "http",
      "url": "http://localhost:5000/mcp",
      "headers": { "Authorization": "ApiKey dev-alice-key" }
    },
    "shop-cli": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "src/Shop.Cli", "--", "mcp"]
    }
  }
}
```

### Codex

`~/.codex/config.toml`：

```toml
[mcp_servers.shop-cli]
command = "dotnet"
args = ["run", "--project", "src/Shop.Cli", "--", "mcp"]
```

### 其他工具

支持 MCP 的工具都可以按上面两种形式配置：HTTP 传输填端点地址与 `Authorization` 头，stdio 传输填启动命令与 `mcp` 参数。

## 使用约束

- 只有 `Expose` 含 `Mcp`、稳定性不是 `Internal` 的操作会出现在 `tools/list` 里；没写 `Expose` 时取程序集级缺省，再无则是全部协议。
- 操作失败时 `tools/call` 以 `isError: true` 返回，文本为「错误码: 文案」，文案按请求的 `Accept-Language` 翻译。
- `get` / `list` 与声明了 `ReadOnly = true` 的操作带只读、幂等提示，`delete` 带破坏性提示，AI 工具据此决定是否需要用户确认。
- 开发用的 ApiKey 只放在本地配置里，不要提交进仓库；生产环境的 MCP 端点按[认证](../guide/authentication)配置正式的认证 Provider。

## 相关

- [For Agents](./agents) · [CLI](./cli) · [LLMs.txt](./llms)
- [gRPC、MCP 与 CloudEvents](../guide/protocols)
- [Protocols.Mcp 包](../packages/protocols-mcp)
