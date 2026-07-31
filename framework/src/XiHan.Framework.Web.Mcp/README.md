# XiHan.Framework.Web.Mcp

## 概述
XiHan.Framework.Web.Mcp 把 AI 技能以 MCP（Model Context Protocol）Server 的形式经 HTTP 传输对外暴露，供外部 MCP 客户端调用。

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
