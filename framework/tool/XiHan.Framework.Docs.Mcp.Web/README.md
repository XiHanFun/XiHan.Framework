# XiHan.Framework.Docs.Mcp.Web

## 概述

把 `XiHan.Framework.Docs.Mcp` 那套文档检索能力搬到 HTTP 后面，让**不在本机**的 AI 客户端也能查曦寒框架的文档。仓库内部工具，自行部署，不发布为 NuGet 包（`IsPackable=false`）。

三组关系必须一开始就分清，否则很容易找错地方改代码：

| 与谁 | 关系 |
| --- | --- |
| `framework/tool/XiHan.Framework.Docs.Mcp`（stdio 服务端） | **同一套工具、同一套索引，只换传输。**`search_docs` / `read_doc` / `list_docs` 三个工具、bigram 倒排索引、同义词扩展、相关性截断全部来自那个项目，本项目一行检索逻辑都没有。改检索行为要去那边改，改完两个宿主一起生效 |
| `framework/src/XiHan.Framework.Web.Mcp` | **不相干。**那个包把**宿主应用**的 `IAiSkill` 投影成 MCP 工具，服务的是业务应用；本项目服务的是仓库文档。两者的 fail-closed 与 key 鉴权写法一致（是刻意照搬的范式），但没有任何代码依赖——本项目**不引用**它 |
| MCP 客户端（Claude Code、Cursor、VS Code…） | 客户端连 `POST /mcp`，带 key。stdio 那套 `.mcp.json` 配置在这里不适用 |

什么时候用哪个：客户端与仓库在同一台机器上，用 stdio 那个，省一次网络往返也不用管密钥；客户端在别处（团队共用一台文档服务器、云端 Agent、CI），才用本项目。

## 核心能力

- 以流式 HTTP（Streamable HTTP）传输暴露 `search_docs` / `read_doc` / `list_docs` 三个 MCP 工具，能力与 stdio 服务端逐字一致
- **fail-closed 门控**：没开启或没配密钥时，既不注册 MCP 服务也不映射端点——半配好的部署暴露出来的是 404，而不是一个不设防的 `/mcp`
- 应用管理的 API Key 鉴权：请求头（默认 `X-Api-Key`）或 `Authorization: Bearer`，定长比较防时序侧信道
- 启动时同步建索引，建完才开始接受请求；此后按文件 mtime 热更新，文档改了不用重启
- 找不到仓库根时**直接以非零退出码结束**并把原因写进 stderr，不会带着空索引进入「连得上但永远搜不到」的状态

## 依赖关系

- `ModelContextProtocol.AspNetCore` 2.2.0：HTTP 传输与 `MapMcp`（`WithHttpTransport` / `MapMcp` 仅此包提供）
- `XiHan.Framework.Docs.Mcp`（项目引用）：索引、检索与三个工具的全部实现
- 不引用 `XiHan.Framework.Web.Mcp`，因而也不引入 `XiHan.Framework.AI` 与 `XiHan.Framework.Web.Core`——一个独立的文档服务端不该把整套模块系统拖进来。代价是 `Filters/McpApiKeyEndpointFilter.cs` 是那边同名文件的一份**刻意复制**，两边改动须同步；按住这份复制不漂移的是 `framework/test/XiHan.Framework.Docs.Mcp.Web.Tests/ApiKeyAuthTests.cs`
- 仅把 `XiHan.Framework.Analyzers` 作为分析器引用（`XHFH001` 文件头检查），不进运行期

## 配置与约定

配置节 `XiHan:Docs:Mcp`（对应 `Options/XiHanDocsMcpWebOptions.cs`）：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `Enabled` | `false` | 总开关。仓库里提交的默认值就是 `false` |
| `ApiKey` | **无** | 访问密钥。**仓库里没有这个键，也永远不该有** |
| `HeaderName` | `X-Api-Key` | 携带密钥的请求头名 |
| `Path` | `/mcp` | 端点路径 |
| `Stateless` | `true` | 无状态 HTTP。三个工具都是纯检索，不需要服务端→客户端回调 |

`Enabled && ApiKey 非空白` 才算「就绪暴露」（`IsExposable`）。两者缺一，进程照常起、照常建索引，但**不注册任何 MCP 服务、不映射任何端点**，并在日志里点名缺的是哪一项。

环境变量：

| 变量 | 说明 |
| --- | --- |
| `XIHAN_DOCS_ROOT` | 显式指定仓库根。不设置时从程序集所在目录逐层向上找同时含 `docs/` 与 `framework/` 的目录 |
| `XiHan__Docs__Mcp__ApiKey` | 注入密钥。ASP.NET Core 把 `__` 映射成 `:`，因此这个变量等价于配置键 `XiHan:Docs:Mcp:ApiKey` |

排序、截断等检索侧参数不在本项目，全部在 `framework/tool/XiHan.Framework.Docs.Mcp/Options/DocsMcpOptions.cs`。

## 使用方式

### 密钥绝不进仓库

`appsettings.json` 里没有 `ApiKey` 这个键，这是设计的一部分而不是待填的空。**任何情况下都不要把密钥写进 `appsettings.json`、`appsettings.Development.json` 或任何随仓库提交的文件**——提交历史里的密钥即便后来删掉也仍然泄露了。下面两条路径分别对应开发与部署，都不经过仓库。

### 开发期：用户机密

```bash
cd framework/tool/XiHan.Framework.Docs.Mcp.Web
dotnet user-secrets set "XiHan:Docs:Mcp:Enabled" "true"
dotnet user-secrets set "XiHan:Docs:Mcp:ApiKey" "$(openssl rand -hex 32)"
dotnet run
```

用户机密存在用户配置目录下（Windows 为 `%APPDATA%\Microsoft\UserSecrets\xihan-docs-mcp-web`），不在仓库里。启动后默认监听 `http://localhost:5000`；换端口用 `dotnet run --urls http://127.0.0.1:5199`。

### 部署期：环境变量

```bash
dotnet build framework/tool/XiHan.Framework.Docs.Mcp.Web/XiHan.Framework.Docs.Mcp.Web.csproj -c Release

XiHan__Docs__Mcp__Enabled=true \
XiHan__Docs__Mcp__ApiKey='<32 字节随机串>' \
XIHAN_DOCS_ROOT=/srv/xihan-framework \
ASPNETCORE_URLS=http://0.0.0.0:5199 \
dotnet framework/tool/XiHan.Framework.Docs.Mcp.Web/bin/Release/net10.0/XiHan.Framework.Docs.Mcp.Web.dll
```

服务端跑起来的样子（日志走 stdout，这里 stdout **不是**协议通道，与 stdio 服务端相反）：

```text
info: XiHan.Framework.Docs.Mcp.Indexing.DocIndex[0]
      文档索引已重建：163 个文件，1720 个章节。
info: XiHan.Framework.Docs.Mcp.Web[0]
      文档 MCP 端点已映射到 /mcp，仓库根 /srv/xihan-framework；请求须携带 X-Api-Key 或 Authorization: Bearer。
```

没配好则是这一行，缺哪项写哪项，**且没有任何端点**：

```text
warn: XiHan.Framework.Docs.Mcp.Web[0]
      文档 MCP 端点未暴露：XiHan:Docs:Mcp:Enabled 为 false；XiHan:Docs:Mcp:ApiKey 未配置。进程已启动但不提供任何端点，补齐后重启即可。
```

### 用 curl 验证

两种鉴权写法都接受，任选其一：

```bash
# 写法一：自定义请求头
curl -sN -X POST http://127.0.0.1:5199/mcp \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -H 'X-Api-Key: <你的密钥>' \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"1.0.0"}}}'

# 写法二：Authorization: Bearer
curl -sN -X POST http://127.0.0.1:5199/mcp \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -H 'Authorization: Bearer <你的密钥>' \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"1.0.0"}}}'
```

握手成功的响应：

```text
event: message
data: {"result":{"protocolVersion":"2025-06-18","capabilities":{"logging":{},"tools":{}},"serverInfo":{"name":"XiHan.Framework.Docs.Mcp.Web","version":"3.13.0.0"}},"id":1,"jsonrpc":"2.0"}
```

`Accept` 少写 `text/event-stream` 会拿到 406 而不是 401，别误判成鉴权失败。三个状态码分别对应三件事：**404 是端点根本没映射（没开启或没配密钥），401 是密钥不对，200 才是通了。**

### 接进 MCP 客户端

```json
{
  "mcpServers": {
    "xihan-docs-http": {
      "type": "http",
      "url": "http://127.0.0.1:5199/mcp",
      "headers": { "X-Api-Key": "<你的密钥>" }
    }
  }
}
```

这份配置本身也含密钥，放进客户端自己的配置目录，不要提交。

## 扩展点

- **换检索行为**：去 `XiHan.Framework.Docs.Mcp` 改，本项目不动。改完必须重跑那边的黄金查询集 `framework/test/XiHan.Framework.Docs.Mcp.Tests/GoldenQueryTests.cs`
- **加传输方式**：再建一个宿主项目引用 `XiHan.Framework.Docs.Mcp`，照 `Extensions/DependencyInjection/XiHanDocsMcpWebServiceCollectionExtensions.cs` 那几行装配即可。注意 `WithToolsFromAssembly()` 在这种形态下**不管用**——它扫的是调用方所在程序集，而 `DocsMcpTools` 在被引用的程序集里，必须用 `WithTools<DocsMcpTools>()` 显式登记
- **换鉴权方式**（改成 OAuth、mTLS 或反向代理鉴权）：只需替换 `Extensions/ApplicationBuilderExtensions.cs` 里挂的那个端点过滤器。**fail-closed 判定不要动**：`IsExposable` 同时守着服务注册与端点映射两处，只改一处会退化成「注册了但没暴露」或更糟的「暴露了但没守门」
- **暴露到公网前**：本项目只做 key 鉴权，没有限流、没有 TLS、没有审计。放到公网应当摆在反向代理后面，由代理负责 TLS 与限流

## 目录结构

```text
XiHan.Framework.Docs.Mcp.Web/
  README.md
  Program.cs
  appsettings.json
  Options/
    XiHanDocsMcpWebOptions.cs
  Filters/
    McpApiKeyEndpointFilter.cs
  Extensions/
    ApplicationBuilderExtensions.cs
    DependencyInjection/
      XiHanDocsMcpWebServiceCollectionExtensions.cs
```
