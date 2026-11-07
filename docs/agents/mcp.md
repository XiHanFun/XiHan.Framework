# 28.2 MCP Server

本页说明两种 MCP 服务端怎样接入 Claude Code、Cursor、VS Code、Codex 等 AI 工具：框架自带的知识库服务 `xihan mcp`，让 AI 工具按版本查证包、能力、配置键与文档；以及把基于本框架的应用作为 MCP 服务端，让 AI 工具在开发过程中直接调用应用的操作。

## 框架知识库：xihan mcp

`XiHan.Framework.Cli` 的 `mcp` 命令以 stdio 提供框架的离线知识库，数据随工具版本一起构建（包与特性清单、各 Options 读取的配置键与缺省值、文档站、RFC 与各包 README），用 `dnx` 按版本运行，不需要安装：

| tool | 用途 |
| --- | --- |
| `xihan_framework_info` | 包、特性与能力：安装命令、环层、依赖、特性清单（提供与依赖的能力、绑定、配置节、管道中间件、操作、权限码、健康项）；能力 ID 给出提供者与依赖方 |
| `xihan_framework_config` | 配置键：完整路径、类型、缺省值、必填、取值范围与 appsettings 片段 |
| `xihan_framework_search` | 检索文档，或以 `page` 取一页全文 |

三个 tool 都只读，结果是结构化 JSON，查不到时返回 `isError` 与最接近的候选。

最省事的是在项目根运行 `setup`，它读出项目引用的框架版本，把服务写进对应工具的配置，并写入使用技能（只要 MCP 时加 `--mode mcp`，`--dry-run` 只打印，`--check` 核对是否就绪）：

```bash
dnx XiHan.Framework.Cli --yes -- setup --client claude    # 或 cursor、vscode、codex
```

手写配置时，项目根的 `.mcp.json`（Claude Code）或 `.cursor/mcp.json`（Cursor）：

```json
{
  "mcpServers": {
    "xihan-framework": {
      "command": "dnx",
      "args": ["XiHan.Framework.Cli@<版本>", "--yes", "--", "mcp"]
    }
  }
}
```

版本号写项目引用的 `XiHan.Framework.*` 版本；不一致时服务端在说明与 stderr 里提示。Windows（非 WSL）上 Claude Code 要经 `cmd /c` 调用：`command` 写 `cmd`，`args` 前加 `"/c", "dnx"`。VS Code 的 `.vscode/mcp.json` 顶层键是 `servers`，每一项加 `"type": "stdio"`；Codex 在 `~/.codex/config.toml` 写 `[mcp_servers.xihan-framework]`，`command = "dnx"`，`args` 同上。需要 .NET 10 SDK（`dnx`）。命令细节见 [Cli 包](/packages/cli#mcp)。

下文讲的是另一种：把你的应用作为 MCP 服务端。

## 什么是 MCP

MCP（Model Context Protocol）是 AI 工具调用外部工具的开放协议。`Protocols.Mcp` 把应用里标记了 `Expose.Mcp` 的操作发布为 MCP tool：tool 名是 `{resource}_{verb}`（`orders.create` → `orders_create`），输入与输出的 JSON Schema 取自操作契约，标题与描述取自消息目录里的 `<操作 ID>.title` / `<操作 ID>.description`。`tools/call` 以一次独立调用经过完整的内核管道，认证、授权、校验、事务与普通调用一致。协议细节见 [gRPC、MCP 与 CloudEvents](/protocols/grpc)。

## 两种传输

| 传输 | 启用方式 | 认证 | 适用 |
| --- | --- | --- | --- |
| Streamable HTTP | Web 应用加入 `McpProtocolFeature`，`app.MapXiHanMcp()`，端点 `POST /mcp` | 请求头 `Authorization` 由 `UseXiHan()` 在外层认证 | 需要身份与权限的操作；连接已在运行的开发服务器 |
| stdio | 命令行宿主加入 `McpProtocolFeature` 后运行 `mcp` 命令，AI 工具以子进程方式启动 | 环境变量 `XIHAN_AUTHORIZATION` 或启动参数 `--authorization`，每个 `tools/call` 在管道里认证；不给就是匿名 | 本机以子进程启动的工具类应用 |

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

stdio 传输的命令行应用（仓库示例 `framework/samples/SimpleCli`）：

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
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project framework/samples/SimpleCli -- mcp
```

stdio 传输下 stdout 每行一条 JSON-RPC 消息，日志只写 stderr。`mcp` 命令接受与 `invoke` 相同的上下文选项：`--authorization`（或环境变量 `XIHAN_AUTHORIZATION`）、`--tenant`、`--correlation`、`--locale`、`--timeout`，作用于会话里的每个 `tools/call`。

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
      "args": ["run", "--project", "src/Shop.Cli", "--", "mcp", "--locale", "zh-CN"],
      "env": { "XIHAN_AUTHORIZATION": "ApiKey dev-alice-key" }
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
args = ["run", "--project", "src/Shop.Cli", "--", "mcp", "--locale", "zh-CN"]
env = { XIHAN_AUTHORIZATION = "ApiKey dev-alice-key" }
```

### 其他工具

支持 MCP 的工具都可以按上面两种形式配置：HTTP 传输填端点地址与 `Authorization` 头，stdio 传输填启动命令与 `mcp` 参数，凭据放进环境变量 `XIHAN_AUTHORIZATION`（比写在参数里更不容易出现在进程列表与日志中）。

## 使用约束

- 只有 `Expose` 含 `Mcp`、稳定性不是 `Internal` 的操作会出现在 `tools/list` 里；没写 `Expose` 时取程序集级缺省，再无则是全部协议。
- 操作失败时 `tools/call` 以 `isError: true` 返回，文本为「错误码: 文案」，文案按请求的 `Accept-Language` 翻译。
- `get` / `list` 与声明了 `ReadOnly = true` 的操作带只读、幂等提示，`delete` 带破坏性提示，AI 工具据此决定是否需要用户确认。
- 开发用的 ApiKey 只放在本地配置里，不要提交进仓库；生产环境的 MCP 端点按[认证](/security/authentication)配置正式的认证 Provider。

## 相关

- [For Agents](./agents) · [CLI](./cli) · [LLMs.txt](./llms)
- [gRPC、MCP 与 CloudEvents](/protocols/grpc)
- [Protocols.Mcp 包](/packages/protocols-mcp)
