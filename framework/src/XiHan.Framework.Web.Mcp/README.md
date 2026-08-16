# XiHan.Framework.Web.Mcp

## 概述
XiHan.Framework.Web.Mcp 把 AI 技能以 MCP（Model Context Protocol）Server 的形式经 HTTP 传输对外暴露，供外部 MCP 客户端调用。

这里暴露的是**宿主应用自己注册的 `IAiSkill` 实现**，与仓库内 `framework/tool/XiHan.Framework.Docs.Mcp`（本机 stdio、检索本仓库文档的内部工具）是两回事，两者常被混为一谈：本包不提供 `search_docs`／`read_doc`／`list_docs` 这些文档工具。

## 核心能力
- MCP Server 的 HTTP 传输装配（可选无状态模式）
- 技能注册表到 MCP tools 的投影（复用 `XiHan.Framework.AI` 的 `AddXiHanMcpServerTools`）
- `/mcp` 端点映射与应用管理 key 鉴权（定长比较，401 拒绝）
- fail-closed：未开启或未配置密钥时既不注册服务也不映射端点

## 依赖关系
- 通过 `XiHanWebMcpModule` 参与模块化生命周期
- 依赖 `XiHanWebCoreModule` 与 `XiHanAIModule`

## 配置与约定
配置节 `XiHan:AI:Mcp`（`XiHanMcpOptions`）：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `Enabled` | `false` | 是否启用 MCP Server |
| `ApiKey` | 空 | 应用管理的访问密钥，空则不暴露端点 |
| `HeaderName` | `X-Api-Key` | 携带密钥的请求头名（也接受 `Authorization: Bearer`） |
| `Path` | `/mcp` | 端点路径 |
| `Stateless` | `true` | 是否无状态 HTTP 传输 |

### appsettings.json 长什么样

配置节名 `XiHan:AI:Mcp` 是冒号分隔的**配置路径**，落到 JSON 里是三层嵌套，写平了绑不上：

```json
{
  "XiHan": {
    "AI": {
      "Mcp": {
        "Enabled": true,
        "ApiKey": "",
        "Path": "/mcp",
        "Stateless": true
      }
    }
  }
}
```

上面这份就是**该进源码管理的样子**：`ApiKey` 留空（或整个键都不写）。此时 `IsExposable` 为 false，端点不会被映射，提交进仓库的配置默认什么都不暴露。

> **`ApiKey` 绝不能写进任何 appsettings 文件。** 它是能调用宿主全部 AI 技能的凭据，落进版本库就等于公开，事后删掉也仍在历史里。密钥只能从环境变量或 Secret Manager 注入。

开发期用 Secret Manager（在宿主项目目录执行）：

```bash
dotnet user-secrets init
dotnet user-secrets set "XiHan:AI:Mcp:ApiKey" "<随机生成的密钥>"
```

部署期用环境变量。ASP.NET Core 把双下划线 `__` 映射成配置路径分隔符 `:`，所以键名是 `XiHan__AI__Mcp__ApiKey`：

```bash
export XiHan__AI__Mcp__Enabled=true
export XiHan__AI__Mcp__ApiKey='<随机生成的密钥>'
```

PowerShell：

```powershell
$env:XiHan__AI__Mcp__Enabled = 'true'
$env:XiHan__AI__Mcp__ApiKey = '<随机生成的密钥>'
```

### fail-closed 门控

`XiHanMcpOptions.IsExposable` 的定义是 `Enabled && ApiKey 非空白`，**服务注册与端点映射共用这一个判定**：

- 判定为 false 时，`AddXiHanWebMcp` 只绑定选项、不注册任何 MCP 服务；`MapXiHanMcp` 直接返回，不映射任何端点
- 于是配错的部署（开了 `Enabled` 却漏了 `ApiKey`）暴露出去的是**什么都没有**，而不是一个无鉴权的 `/mcp`

这是本包最要紧的安全性质：漏配的后果只会是「用不了」，不会是「敞开了」。

### 客户端怎么带密钥

端点用 `AllowAnonymous()` 绕开框架的全局鉴权 FallbackPolicy，改由 `McpApiKeyEndpointFilter` 守门（定长比较防时序侧信道，不匹配即 401）。客户端二选一：

- 请求头 `X-Api-Key: <密钥>`（头名由 `HeaderName` 决定，默认即此）
- `Authorization: Bearer <密钥>`

只有 `HeaderName` 指定的那个头缺失或为空时，才回退去看 `Authorization`。

冒烟验证（把地址换成宿主实际监听的地址；MCP 的 Streamable HTTP 传输要求 `Accept` 同时列出 `application/json` 与 `text/event-stream`）：

```bash
# 不带密钥：401
curl -i -X POST http://localhost:5000/mcp \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"0"}}}'

# 带密钥：请求进入 MCP 处理器
curl -i -X POST http://localhost:5000/mcp \
  -H "X-Api-Key: <随机生成的密钥>" \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"0"}}}'
```

### Stateless 的取舍

`Stateless` 默认 `true`，含义是 HTTP 传输不保持会话，**没有服务端 → 客户端的回调通道**。

- 检索、查询、计算这类「进去一次、出来一次」的技能，无状态足够，也更好水平扩缩
- 技能若需要 sampling（回头找客户端要一次模型推理）或 elicitation（回头问用户要输入），必须把 `Stateless` 设为 `false`

## 使用方式
```csharp
[DependsOn(typeof(XiHanWebMcpModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 实现 `IAiSkill` 并注册到技能注册表，即自动成为 MCP tool
- 自行调用 `MapXiHanMcp` 在指定端点路由构建器上映射

## 目录结构
```text
XiHan.Framework.Web.Mcp/
  README.md
  XiHanWebMcpModule.cs
  Extensions/
    ApplicationBuilderExtensions.cs
    DependencyInjection/
      XiHanWebMcpServiceCollectionExtensions.cs
  Filters/
    McpApiKeyEndpointFilter.cs
  Options/
    XiHanMcpOptions.cs
```
