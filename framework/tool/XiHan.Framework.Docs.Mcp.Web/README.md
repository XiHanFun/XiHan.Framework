# XiHan.Framework.Docs.Mcp.Web

## 概述

把 `XiHan.Framework.Docs.Mcp` 那套文档检索能力搬到 HTTP 后面，让**不在本机**的 AI 客户端也能查曦寒框架的文档。仓库内部工具，自行部署，不发布为 NuGet 包（`IsPackable=false`）。

三组关系必须一开始就分清，否则很容易找错地方改代码：

| 与谁 | 关系 |
| --- | --- |
| `framework/tool/XiHan.Framework.Docs.Mcp`（stdio 服务端） | **同一套工具、同一套索引，只换传输。**`search_docs` / `read_doc` / `list_docs` 三个工具、bigram 倒排索引、同义词扩展、相关性截断全部来自那个项目，本项目一行检索逻辑都没有。改检索行为要去那边改，改完两个宿主一起生效 |
| `framework/src/XiHan.Framework.Web.Mcp` | **不相干。**那个包把**宿主应用**的 `IAiSkill` 投影成 MCP 工具，服务的是业务应用；本项目服务的是仓库文档。两者的 fail-closed 写法一致（刻意照搬的范式），鉴权过滤器更是**同一份源文件**（由 csproj 链接进来，不是程序集引用）；除此之外没有任何依赖——本项目**不引用**那个包 |
| MCP 客户端（Claude Code、Cursor、VS Code…） | 客户端连 `POST /mcp`，带 key。stdio 那套 `.mcp.json` 配置在这里不适用 |

什么时候用哪个：客户端与仓库在同一台机器上，用 stdio 那个，省一次网络往返也不用管密钥；客户端在别处（团队共用一台文档服务器、云端 Agent、CI），才用本项目。

## 核心能力

- 以流式 HTTP（Streamable HTTP）传输暴露 `search_docs` / `read_doc` / `list_docs` 三个 MCP 工具，能力与 stdio 服务端逐字一致
- **fail-closed 门控**：没开启或没配密钥时，既不注册 MCP 服务也不映射端点——半配好的部署暴露出来的是 404，而不是一个不设防的 `/mcp`
- 应用管理的 API Key 鉴权：请求头（默认 `X-Api-Key`）或 `Authorization: Bearer`，定长比较防时序侧信道
- **启动期校验配置**：要暴露却把请求头名、端点路径写错，或密钥短于 16 字符，进程直接启动失败，不会带着一份配错的配置上线
- 启动时同步建索引，建完才开始接受请求；此后按文件 mtime 热更新，文档改了不用重启
- 找不到仓库根时**直接以非零退出码结束**并把原因写进 stderr，不会带着空索引进入「连得上但永远搜不到」的状态

## 依赖关系

- `ModelContextProtocol.AspNetCore` 2.2.0：HTTP 传输与 `MapMcp`（`WithHttpTransport` / `MapMcp` 仅此包提供）
- `XiHan.Framework.Docs.Mcp`（项目引用）：索引、检索与三个工具的全部实现
- 不引用 `XiHan.Framework.Web.Mcp`，因而也不引入 `XiHan.Framework.AI` 与 `XiHan.Framework.Web.Core`——一个独立的文档服务端不该把整套模块系统拖进来。鉴权过滤器则**链接**自 `framework/src/XiHan.Framework.Web.Mcp/Filters/McpApiKeyEndpointFilter.cs`（csproj 里的 `<Compile Include=... Link=... />`）：同一份源文件编进两个程序集，没有程序集引用，也没有多出来的 NuGet 包，两边不可能漂移。副作用是那份文件一旦用上 `XiHan.Framework.Web.Core` 的类型，本项目立刻编不过——这是刻意留的硬约束
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

### 启动期校验

就绪暴露时，配置还要过 `Options/XiHanDocsMcpWebOptionsValidator.cs`（`IValidateOptions` + `ValidateOnStart()`）。以下几项配错会让**进程启动即失败**，而不是等到部署之后表现成 404、恒 401 或路由异常：

| 设置 | 拒绝条件 | 合法示例 |
| --- | --- | --- |
| `HeaderName` | 空白，或含 RFC 9110 token 之外的字符（合法字符为字母、数字与 `` !#$%&'*+-.^_`\|~ ``） | `X-Api-Key` |
| `Path` | 空白、不以 `/` 开头、含空白字符 | `/mcp`、`/docs-mcp` |
| `ApiKey` | 短于 **16** 个字符（本服务没有限流，短密钥可被在线爆破） | `openssl rand -base64 32` 的输出 |

**未启用的部署不做这些校验**：仓库里提交的默认配置就是「关闭且没有密钥」，一台刻意关掉的服务必须能干干净净地起来，否则 fail-closed 就变成了 fail-always。

生成一把够长的密钥：

```bash
openssl rand -base64 32
```

```powershell
[Convert]::ToBase64String((1..32|%{Get-Random -Max 256}))
```

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

配了却配错（要暴露但请求头名、路径或密钥不合规）则**根本起不来**，退出前打出的是逐条列明的校验失败，详见上文「启动期校验」。

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

### 摆到反向代理后面

本项目自己不提供任何传输层安全：没有 TLS、没有限流、没有审计日志。要暴露到本机以外，前面必须摆一层反向代理，由它负责这几件事。`deploy/` 下有两份可以直接改用的样例：

| 文件 | 适合谁 |
| --- | --- |
| `deploy/Caddyfile.example` | 新部署、没有历史包袱。证书自动申请续期、HTTP→HTTPS 自动跳转都不用写指令，正确配好所需的行数最少 |
| `deploy/nginx.conf.example` | 机器上已经在跑 Nginx |

一处得提前知道的差别：Caddy 的限流指令 `rate_limit` **不在标准发行版里**，要用 `xcaddy build --with github.com/mholt/caddy-ratelimit` 自行构建；标准版 Caddy 读到这个指令会直接启动失败（而不是静默忽略）。不想自建就删掉那一块，改由防火墙或 WAF 限流。Nginx 的 `limit_req` 是内置模块，没有这个问题。

**这两份是起点，不是审计过的配置。** 一份被原样粘贴、然后被默认「它是安全的」的样例，比没有样例更糟：它把一个还没想过的问题，换成了一个以为已经想过的问题。它们能替你做的，只是那些容易写错、写错了代价又特别大的部分；替不了你做的是这四个决定：

1. **谁能碰到这个端点。** 两份样例里的 IP 白名单都是注释掉的，因为只有你知道调用方在哪。这一条比限流有效得多：密钥泄露之后，网段限制仍然拦得住
2. **密钥怎么轮换。** 换一次密钥要同时改服务端环境变量和每一个客户端配置，中间必然有一段两者不一致的窗口。多久换、怎么换、谁保管，本项目一行代码都不管
3. **访问日志留多久。** 样例里写的 30 天是随手填的数字，不是建议值。日志里有客户端 IP，留存期限是合规问题
4. **这个端点到底该不该上公网。** 它服务的是一份公开仓库的文档，内容泄露本身损失有限；真正的风险是它成了一个能被匿名扫到、可以持续消耗 CPU 的检索入口。摆在 VPN、内网或跳板机后面，通常同样够用，而且比「公网 + 密钥」稳妥得多

部署前提：密钥来自环境变量（`XiHan__Docs__Mcp__ApiKey`，ASP.NET Core 把 `__` 映射成 `:`）或密钥管理服务，**永远不来自随仓库提交的 `appsettings.json`**。仓库里提交的默认值是 `Enabled: false` 且根本没有 `ApiKey` 这个键，这是设计的一部分，不是待填的空。

样例里关于上游的每一条断言都是实测出来的，不是照通用模板抄的：

| 实测到的行为 | 对代理配置的影响 |
| --- | --- |
| `/mcp` 与 `/mcp/` 都返回 200；`/`、`/health`、`/mcp/sse`、`/mcp/message` 全是 404 | 只放行这两个路径。Nginx 用 `location ~ ^/mcp/?$`：精确匹配会漏掉尾斜杠，前缀匹配又会把 `/mcpx` 收进来 |
| `GET /mcp` 与 `DELETE /mcp` 返回 `405 Allow: POST` | 只有 POST，**不需要任何 websocket / Upgrade 配置**。网上的 MCP 代理模板常带这一段，抄了不会报错，但它描述的不是这个上游 |
| 响应 `Content-Type: text/event-stream`，且上游主动带 `X-Accel-Buffering: no` | 代理不能缓冲响应。Nginx 显式 `proxy_buffering off`；Caddy 对这个 Content-Type 自动立即冲刷，不用配 |
| 响应里没有 `Mcp-Session-Id`（默认 `Stateless: true`） | 多实例部署不需要会话粘滞，可以随便轮询 |
| `Accept` 少写 `text/event-stream` 时返回 406 | 代理不要改写 `Accept`。406 是内容协商失败，别误判成鉴权失败 |
| 请求体极小：initialize 152 字节，tools/call 115~550 字节 | 请求体上限设到 64KB 已是几十倍余量，代价为零，却挡掉了最省事的那类 DoS |

最容易漏、漏掉代价最大的一条单独说：**默认配置下，代理会把密钥写进访问日志。** Caddy 的访问日志默认记录整个请求头集合，自动写成 `REDACTED` 的只有 `Cookie` / `Set-Cookie` / `Authorization` / `Proxy-Authorization`——`X-Api-Key` 不在这个名单里，会原样落盘。Nginx 内置的 `combined` 格式不打印自定义请求头，本来是安全的，但只要有人为了排查方便往 `log_format` 里加一个 `$http_x_api_key`（或 `$http_authorization`），密钥就明文进了磁盘，还会跟着日志进备份、进采集系统，此后唯一的撤回手段是换密钥。两份样例都显式处理了这件事，动日志格式之前先读那几行注释。

改完先自检再重载：Caddy 用 `caddy validate --config ./Caddyfile`，Nginx 用 `nginx -t`。**这两份样例没有在真实的 Caddy / Nginx 上跑过**，指令与参数是逐条对着官方文档核的；上游那一侧的行为则是起进程实测的。

上了代理之后，客户端配置里的 URL 换成 `https://mcp.example.com/mcp`，请求头写法不变。

## 扩展点

- **换检索行为**：去 `XiHan.Framework.Docs.Mcp` 改，本项目不动。改完必须重跑那边的黄金查询集 `framework/test/XiHan.Framework.Docs.Mcp.Tests/GoldenQueryTests.cs`
- **加传输方式**：再建一个宿主项目引用 `XiHan.Framework.Docs.Mcp`，照 `Extensions/DependencyInjection/XiHanDocsMcpWebServiceCollectionExtensions.cs` 那几行装配即可。注意 `WithToolsFromAssembly()` 在这种形态下**不管用**——它扫的是调用方所在程序集，而 `DocsMcpTools` 在被引用的程序集里，必须用 `WithTools<DocsMcpTools>()` 显式登记
- **换鉴权方式**（改成 OAuth、mTLS 或反向代理鉴权）：只需替换 `Extensions/ApplicationBuilderExtensions.cs` 里挂的那个端点过滤器。**fail-closed 判定不要动**：`IsExposable` 同时守着服务注册与端点映射两处，只改一处会退化成「注册了但没暴露」或更糟的「暴露了但没守门」
- **暴露到公网前**：本项目只做 key 鉴权，没有限流、没有 TLS、没有审计。放到公网应当摆在反向代理后面，由代理负责 TLS 与限流；`deploy/` 下有 Caddy 与 Nginx 两份带注释的样例，配套说明见「摆到反向代理后面」

## 目录结构

```text
XiHan.Framework.Docs.Mcp.Web/
  README.md
  Program.cs
  appsettings.json
  Options/
    XiHanDocsMcpWebOptions.cs
    XiHanDocsMcpWebOptionsValidator.cs
  Filters/
    McpApiKeyEndpointFilter.cs   ← 链接自 framework/src/XiHan.Framework.Web.Mcp/Filters/，本目录下没有实体文件
  Extensions/
    ApplicationBuilderExtensions.cs
    DependencyInjection/
      XiHanDocsMcpWebServiceCollectionExtensions.cs
  deploy/
    Caddyfile.example
    nginx.conf.example
```
