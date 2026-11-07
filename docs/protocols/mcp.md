# MCP

这一页讲 MCP（Model Context Protocol）适配器：怎样把操作发布成大模型可调用的 tool，`tools/list` 里的名字、Schema、标题描述与注解从哪里来，`tools/call` 怎样经过完整管道并区分业务失败与协议错误，HTTP 与 stdio 两种传输各自的规则，以及可配置的选项。读完你能让 Claude、Cursor 这类 MCP 客户端直接调用你的业务操作，而不另写一个 MCP 服务端。前置知识是[操作契约](/web/operations)与上一页 [gRPC](/protocols/grpc) 开头的「一份契约，多个载体」。

## 为什么需要

要让大模型调用业务系统，通常得为每个功能手写一个 tool：名字、描述、参数 Schema、调用代码，再处理认证与错误。这些信息业务系统其实都有，只是分散在控制器、DTO、注释与权限配置里，手写一遍就多一份会过期的拷贝，而且 tool 的实现往往绕过了业务层的授权与校验。

框架把 MCP 当作操作契约的又一个载体：tool 名由操作 ID 推出，`inputSchema` / `outputSchema` 就是契约的 JSON Schema，标题与描述来自消息目录并按调用方语言翻译，注解按动词推导。每个 `tools/call` 在自己的服务作用域里经完整内核管道执行，认证、按契约授权、校验与事务和 HTTP 上完全一样，大模型拿不到比当前用户更多的权限。

## tool 名与 `tools/list`

tool 名按 `{resource}_{verb}` 推导（`orders.create` → `orders_create`），显式覆盖写在配置 `XiHan:Protocols:Mcp:Tools:<操作 ID>`。名字必须匹配 `^[A-Za-z0-9_-]{1,64}$`，不合法时以 `PROTOCOLS_VALIDATION_004`、两个操作同名时以 `PROTOCOLS_CONFLICT_001` 启动失败。`tools/list` 不分页，每个 tool 的字段：

| 字段 | 来源 |
| --- | --- |
| `name` | tool 名 |
| `title` / `description` | 消息目录 `<操作 ID>.title` / `<操作 ID>.description`（操作 ID 里的 `-` 换成 `_`），按调用方区域翻译；没有就不给 |
| `inputSchema` | 请求 Schema；Schema 的 `type` 不是 `object` 或操作没有 Schema 时为 `{"type":"object"}` |
| `outputSchema` | 响应 Schema，仅当其 `type` 是 `object` |
| `annotations` | 按契约推导，见下表 |

| 契约 | `readOnlyHint` | `destructiveHint` | `idempotentHint` |
| --- | --- | --- | --- |
| 只读（`get`、`list`，或 `ReadOnly = true` 的非标准动词） | true | false | true |
| `create` | false | false | false |
| `update` | false | false | true |
| `delete` | false | true | true |
| 其他动词 | false | true | false |

只读必须在 `[Operation]` 上显式声明：没有声明的查询类自定义动词（如 `orders.search`）按 MCP 的默认标成可能破坏，客户端会要求用户确认。描述里不写授权要求，由协议适配器统一呈现。

## 最小示例

我们给 SimpleApp 加上 MCP。三步：引用 `XiHan.Framework.Protocols.Mcp` 并装配 `McpProtocolFeature`；在消息目录里给要暴露的操作写标题与描述；`UseXiHan()` 之后调用 `MapXiHanMcp()`。

```bash
dotnet add package XiHan.Framework.Protocols.Mcp --prerelease
```

操作在 `Expose` 里带上 `Mcp`（上一页的 `CreateOrder` 已经带了）。标题与描述放在应用的语言文件里，每种语言一个文件，写法见[全球化和本地化](/common/localization)。这是 `Localization/en-US.json`：

```json
{
  "orders.create.title": "Place an order",
  "orders.create.description": "Place an order with a name and an amount.",
  "orders.list.title": "List orders",
  "orders.list.description": "List the orders of the current tenant in creation order."
}
```

```csharp
using SimpleApp;
using SimpleApp.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Mcp;
using XiHan.Framework.Protocols.Mcp.Transports;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<McpProtocolFeature>()
    .UseFeature<OrdersFeature>());

// 应用自己的消息目录：tool 的 title 与 description 从这里取
builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("samples", typeof(SampleMessages)));

var app = builder.Build();

app.UseXiHan();
app.MapXiHanOperations();
app.MapXiHanMcp();   // POST /mcp：initialize、ping、tools/list、tools/call

app.Run();
```

运行后先列 tool，再以 `bob` 的凭据下单：

```bash
curl -s -X POST localhost:5000/mcp -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
curl -s -X POST localhost:5000/mcp -H "Content-Type: application/json" -H "Authorization: ApiKey dev-bob-key" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"orders_create","arguments":{"name":"widget","amount":3}}}'
```

第一个请求返回 `orders_create` 与 `orders_list` 两个 tool，带英文标题与描述（加 `Accept-Language: zh-CN` 头就是中文）。`bob` 只有 `orders.read`，第二个请求的答复不是协议错误，而是业务失败：

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "content": [{ "type": "text", "text": "SECURITY_DENIED_004: Permission 'orders.write' is missing." }],
    "isError": true
  }
}
```

换成 `alice`（`dev-alice-key`，持有 `orders.write`）再调一次，`content[0].text` 是新订单的 JSON，同时给出 `structuredContent`。

## `tools/call` 的语义

`arguments`（缺省 `{}`，必须是对象）就是操作的载荷。每个 `tools/call` 是一次独立调用：以承载请求的头部建新的调用上下文，在自己的服务作用域里经完整管道执行，工作单元与作用域服务不与外层 HTTP 请求共享；调用来源记协议 `mcp` 与 tool 名。

成功时 `content[0].text` 是响应 JSON，响应是对象时同时给 `structuredContent`。操作失败（校验、未授权、冲突）不是协议错误，而是 `isError: true` 的结果，文本是 `<错误码>: <文案>`，文案按调用方区域翻译。这样区分的原因：大模型需要读到业务失败的原因来调整下一步（改参数、换 tool、告诉用户没权限），而协议错误只说明请求本身不合法。

## Streamable HTTP 传输与 JSON-RPC 错误

消息为 JSON-RPC 2.0，HTTP 上是 Streamable HTTP 传输：`POST` 一条消息一个答复（`application/json`）；通知（没有 `id`，如 `notifications/initialized`）与客户端发来的响应不答复，回 202；不开 SSE 流、不建会话，`GET` 与 `DELETE` 为 405。请求带的 `MCP-Protocol-Version` 头与服务端版本不同时回 400，缺失按当前版本处理。

`/mcp` 本身是经过 `UseXiHan()` 的普通端点：请求凭据在外层认证，凭据无效时整个请求以 401 Problem Details 拒绝、带 `WWW-Authenticate` 质询，不进入 JSON-RPC；应用未就绪时 503。没有凭据的请求照常进入，按契约的授权在每个 `tools/call` 的管道里判定，所以匿名客户端能 `tools/list`，调需要权限的 tool 时得到 `isError`。

| 情形 | `error.code` | `error.data.code` | HTTP |
| --- | --- | --- | --- |
| 请求体不是 JSON | -32700 | `PROTOCOLS_VALIDATION_001` | 400 |
| 不是 JSON-RPC 2.0 请求（非对象、`jsonrpc` 不是 `2.0`、缺 `method`、`id` 为 `null`、`params` 不是对象） | -32600 | `PROTOCOLS_VALIDATION_001` | 400 |
| `MCP-Protocol-Version` 不受支持 | -32600 | `PROTOCOLS_VALIDATION_002` | 400 |
| 方法不受支持 | -32601 | `PROTOCOLS_NOTFOUND_001` | 200 |
| `tools/call` 缺 `name` | -32602 | `PROTOCOLS_VALIDATION_001` | 200 |
| tool 未登记 | -32602 | `PROTOCOLS_NOTFOUND_002` | 200 |
| `arguments` 不是对象 | -32602 | `PROTOCOLS_VALIDATION_003` | 200 |

## stdio 传输

桌面 MCP 客户端常以子进程方式启动服务端，经 stdin / stdout 通信。装配了 `McpProtocolFeature` 与 `AddXiHanCommandLine()` 的命令行宿主提供 `mcp` 命令：stdin 与 stdout 每行一条 JSON-RPC 消息，日志只写 stderr，stdin 关闭即结束会话。stdin 的字节按 UTF-8 解码，与控制台代码页无关（Windows 上重定向的标准输入不按 936、437 等代码页解码，中文参数不会成乱码）；答复里的非 ASCII 字符按 JSON 转义成 `\uXXXX`，只含 ASCII，按任何控制台输出代码页写出都是合法的 UTF-8。

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project framework/samples/SimpleCli -- mcp
```

`mcp` 命令接受与 `invoke` 相同的上下文选项，作用于整个会话里的每个 `tools/call`：

| 选项 | 作用 |
| --- | --- |
| `--authorization '<scheme> <value>'` 或环境变量 `XIHAN_AUTHORIZATION` | 凭据；选项优先。MCP 规范要求 stdio 服务端从运行环境取凭据，所以客户端配置里通常写环境变量 |
| `--tenant <id>`、`--correlation <id>` | 租户与关联 ID |
| `--locale <区域>` | `tools/list` 的标题与描述、失败文案按它翻译 |
| `--timeout <秒>` | 每个 `tools/call` 的时限 |

```bash
XIHAN_AUTHORIZATION="ApiKey dev-alice-key" dotnet run --project framework/samples/SimpleCli -- mcp --tenant acme --locale zh-CN
```

每个 `tools/call` 经完整管道，凭据在这次调用的 Authenticate 阶段认证；stdio 没有外层认证，凭据无效时需要认证的 tool 得到 `isError`（`SECURITY_DENIED_003`），会话继续。选项不合法（凭据不是 `<scheme> <value>`、时限不是正数）时不进入会话，stderr 写一行用法错误，退出码 64。调用来源只有协议 `mcp` 与 tool 名。宿主的搭建见[命令行宿主](/protocols/command-line)。

## 选项

配置节 `XiHan:Protocols:Mcp`（`McpProtocolOptions`）：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `ServerName` | string | 宿主的应用名 | `initialize` 答复的 `serverInfo.name` |
| `ServerVersion` | string | 框架版本 | `serverInfo.version` |
| `Instructions` | string | 空 | `initialize` 答复的 `instructions`，给大模型的使用说明，空时不写 |
| `Tools:<操作 ID>` | string | — | 显式 tool 名 |

`initialize` 总是答复本服务端的协议版本与 `capabilities.tools.listChanged = false`，由客户端决定是否继续；`ping` 答复 `{}`。

## 常见问题

### tool 列表里没有我的操作

依次检查：`[Operation]` 的 `Expose` 是否含 `Mcp`；`Stability` 是否为 `Internal`（内部操作不在任何协议上发布）；操作所在的特性是否 `UseFeature` 了、`Configure` 里是否调用了 `AddGeneratedOperations()`。用命令行宿主的 `operations` 命令能直接看到每个操作暴露在哪些协议上。

### 标题与描述是空的

`tools/list` 只在消息目录里有 `<操作 ID>.title` / `<操作 ID>.description` 键时才输出这两个字段。确认语言文件里有这两个键、文件以 `LogicalName="Localization/%(Filename)%(Extension)"` 嵌入、`AddMessageCatalog` 已注册。操作 ID 带连字符时键里要换成下划线：`tenant-members.list` 的键是 `tenant_members.list.title`。

### 客户端每次都要求确认我的查询 tool

MCP 客户端按 `destructiveHint` 决定是否确认。只有 `get`、`list` 两个动词自动算只读，`search`、`export` 这类自定义动词要在 `[Operation]` 上写 `ReadOnly = true`，否则按默认标成可能破坏。

### 能用 SSE 或长连接吗

不能。这个实现只做 Streamable HTTP 的请求 / 响应部分：一条 `POST` 一个答复，不开 SSE 流、不建会话。服务端到客户端的通知与 `listChanged` 都不支持，tool 列表只在重启后变化。

### 大模型会不会拿到比用户更多的权限

不会。每个 `tools/call` 带着这次 HTTP 请求（或 stdio 会话）的凭据经完整管道，Authorize 阶段按契约里的 `Permission` / `Policy` 判定，和 HTTP 上调同一个操作完全一样。凭据没有的权限，tool 也没有。

## 相关包与下一步

- 包参考：[Protocols.Mcp](/packages/protocols-mcp)、[Hosting.CommandLine](/packages/hosting-commandline)
- 标题与描述的来源：[全球化和本地化](/common/localization)
- 契约与 Schema：[操作契约](/web/operations)
- 用 MCP 开发框架本身（给 Agent 的 MCP Server）：[MCP Server](/agents/mcp)
- 规范：RFC-0011《操作契约与 XGI》
- 下一步：[CloudEvents](/protocols/cloudevents)，把 HTTP 上的事件接进消息子系统
