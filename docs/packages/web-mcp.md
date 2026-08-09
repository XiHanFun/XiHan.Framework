# XiHan.Framework.Web.Mcp

> MCP Server 服务端接入：把 AI 技能经 HTTP 传输暴露为 MCP tools，供外部 AI 客户端调用；配置门控，默认不暴露。

- **NuGet**：`XiHan.Framework.Web.Mcp`
- **模块类**：`XiHanWebMcpModule`
- **所在层**：Web 层
- **关键依赖**：`ModelContextProtocol.AspNetCore`（MCP 的 HTTP 传输与 `MapMcp`）

## 概述

XiHan.Framework.Web.Mcp 把 [XiHan.Framework.AI](./ai) 里注册的技能（`IAiSkill`）以 MCP（Model Context Protocol）Server 的形式对外暴露，让 Claude Code、Cursor 这类外部 AI 客户端能直接调用应用的能力。

它只做三件事：装配 MCP Server 的 HTTP 传输、复用 AI 包的 `AddXiHanMcpServerTools()` 把技能注册表投影进工具集、映射端点并挂上应用管理的 key 鉴权。技能到 MCP tool 的桥接本身在 AI 包内完成（`SkillMcpToolsConfigurator`），本包只负责「怎么让它对外可达、又不被随便访问」。

## 何时使用

- 需要把应用侧的 AI 技能开放给外部 MCP 客户端调用。
- 希望 MCP 端点的暴露与鉴权由框架统一管理，而不是在每个应用的 WebHost 里各写一遍。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Web.Mcp
```

```csharp
[DependsOn(typeof(XiHanWebMcpModule))]
public class MyModule : XiHanModule { }
```

依赖模块即可，应用侧无需再写任何 MCP 相关代码。

## 配置

配置节 `XiHan:AI:Mcp`（`XiHanMcpOptions`）：

| 键 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `false` | 是否启用 MCP Server |
| `ApiKey` | `string?` | 空 | 应用管理的访问密钥；为空则不暴露端点 |
| `HeaderName` | `string` | `X-Api-Key` | 携带密钥的请求头名；也接受 `Authorization: Bearer` |
| `Path` | `string` | `/mcp` | 端点路径 |
| `Stateless` | `bool` | `true` | 是否无状态 HTTP 传输（无服务端→客户端回调） |

```json
{
  "XiHan": {
    "AI": {
      "Mcp": {
        "Enabled": true,
        "ApiKey": "替换为足够长的随机密钥",
        "Path": "/mcp"
      }
    }
  }
}
```

## 核心能力

- **HTTP 传输装配**：`AddMcpServer().WithHttpTransport()`，`Stateless` 可配。
- **技能投影**：调用 AI 包的 `AddXiHanMcpServerTools()`，技能注册表里的每个 `IAiSkill` 自动成为一个 MCP tool。
- **端点映射与鉴权**：`MapMcp(Path)` 后 `AllowAnonymous()` 绕过框架全局鉴权 FallbackPolicy，改由 `McpApiKeyEndpointFilter` 校验 key，不匹配即 401。
- **fail-closed**：`Enabled` 与 `ApiKey` 任一不满足，则既不注册 MCP 服务也不映射端点。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanWebMcpModule` | 模块类；`ConfigureServices` 装配服务，`OnApplicationInitialization` 映射端点 |
| `XiHanMcpOptions` | 配置类，配置节 `XiHan:AI:Mcp`；`IsExposable` 为「启用且配了密钥」的合成判定 |
| `McpApiKeyEndpointFilter` | 端点过滤器，定长比较校验 key，防时序侧信道 |
| `AddXiHanWebMcp(IConfiguration)` | 服务集合扩展，绑定配置并按 `IsExposable` 装配 |
| `MapXiHanMcp(XiHanMcpOptions)` | 端点路由扩展，按 `IsExposable` 映射端点并挂鉴权过滤器 |

## 注意事项与最佳实践

- **key 是平台级凭据**：它代表「这个 MCP 客户端可以访问本应用」，不是某个用户/租户的身份。因此经 MCP 调进来的技能没有用户与租户上下文，涉及租户数据的技能需自行决定语义。
- **默认不开**：不配 `Enabled` 与 `ApiKey` 时端点根本不存在，误部署不会意外把能力暴露到公网。
- **走反代时**：`/mcp` 为 SSE 长连接，反向代理需关闭缓冲并放宽超时。
- **签名中间件**：若启用了开放接口签名中间件（默认关闭），需把 `/mcp` 加入其忽略路径。

## 依赖模块

- 内部依赖：[XiHan.Framework.Web.Core](./web-core)、[XiHan.Framework.AI](./ai)。
- 第三方核心：`ModelContextProtocol.AspNetCore`（HTTP 传输与 `MapMcp`）。

## 相关模块

- [XiHan.Framework.AI](./ai) — 技能注册表与 `AddXiHanMcpServerTools()` 的所在地。
- [XiHan.Framework.AI.Abstractions](./ai-abstractions) — `IAiSkill` 契约。
