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

`Program.cs` 沿用[第一个应用](/getting-started/first-app)的骨架，装配 `McpProtocolFeature`、注册消息目录，再多映射一个端点：

```csharp
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Protocols.Mcp;
using XiHan.Framework.Protocols.Mcp.Transports;

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<McpProtocolFeature>()
    .UseFeature<OrdersFeature>());

// 应用自己的消息目录：tool 的 title 与 description 从这里取
builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("samples", typeof(SampleMessages)));

// app.UseXiHan() 之后
app.MapXiHanMcp();   // POST /mcp：initialize、ping、tools/list、tools/call
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

入口校验按顺序进行，任一不过即以 JSON-RPC 错误答复、不进入 JSON-RPC（ADR-0199）：请求带 `Origin` 时它必须在 `AllowedOrigins` 里，否则 403（`PROTOCOLS_DENIED_001`）；`Content-Type` 必须是 `application/json`，否则 415（`PROTOCOLS_VALIDATION_015`）；请求体不超过 `MaxRequestBodyBytes`，否则 413（`PROTOCOLS_VALIDATION_016`）。不带 `Origin` 的非浏览器客户端不受第一条影响；浏览器里的客户端（如 MCP Inspector）要把它的源配进 `AllowedOrigins`。这样本机开着的 MCP 服务不会被任意网页用简单请求调用，也不会借 DNS 重绑定被读到结果。

幂等键（Baggage `xihan.idempotency-key`）交给 `tools/call`：外层的 `/mcp` 请求不按它去重，每个 `tools/call` 按它去重与回放，重试同一个调用得到首次的结果（ADR-0205）。

`/mcp` 本身是经过 `UseXiHan()` 的普通端点：请求凭据在外层认证，凭据无效时整个请求以 401 Problem Details 拒绝、带 `WWW-Authenticate` 质询，不进入 JSON-RPC；应用未就绪时 503。没有凭据的请求照常进入，按契约的授权在每个 `tools/call` 的管道里判定，所以匿名客户端能 `tools/list`，调需要权限的 tool 时得到 `isError`。

两种传输交来的每条消息先整条过 JSON 文本的共同检查（RFC-0001「JSON 文本」）：字符串里非法的 UTF-8、转义出的孤立代理项（`"\ud800"`，`method`、`id` 与 `arguments` 里都算）、任何一层重复的成员（含重复的 `id`、`method`、`params.name` 与 `arguments` 里的成员）、嵌套超过 64 层，都与不是 JSON 一样答复解析错误（-32700，`id` 为 `null`，`data.code` 为 `PROTOCOLS_VALIDATION_001`，HTTP 400），不执行任何 tool（ADR-0331）。所以重复的 `name` 不会悄悄执行后一个 tool，`arguments` 里的这类问题也不再是带位置的 `isError` 结果；客户端发合法的 JSON 即可。

JSON-RPC 错误的 `error.data.code` 是框架错误码。解析与入口校验的失败带 HTTP 错误状态：请求体不是 JSON 或没过上面的共同检查（-32700）、不是 JSON-RPC 2.0 请求、协议版本不受支持是 400，`Origin` 不被允许是 403，`Content-Type` 不对是 415，请求体过大是 413（后五种都是 -32600）。进入 JSON-RPC 之后的失败是 HTTP 200 加错误对象：方法不受支持是 -32601，`tools/call` 缺 `name`、tool 未登记、`arguments` 不是对象是 -32602。逐项的错误码见[包参考](/packages/protocols-mcp#行为与限制)。

## stdio 传输

桌面 MCP 客户端常以子进程方式启动服务端，经 stdin / stdout 通信。装配了 `McpProtocolFeature` 与 `AddXiHanCommandLine()` 的命令行宿主提供 `mcp` 命令：stdin 与 stdout 每行一条 JSON-RPC 消息，日志只写 stderr，stdin 关闭即结束会话。stdin 按字节读：先按 LF、CR 或 CR LF 分行（开头的 UTF-8 BOM 不算内容），再把每一行严格解码成 UTF-8，与控制台代码页无关（Windows 上重定向的标准输入不按 936、437 等代码页解码，中文参数不会成乱码）。一行里有非法的 UTF-8（截断的序列、单独的 `0xFF`、编码成 UTF-8 的代理项）时与不是 JSON 的一样答复解析错误（`id` 为 `null`、-32700、`data.code` 为 `PROTOCOLS_VALIDATION_001`），这一行不执行，接着读下一行，不换成 U+FFFD 再处理；只有空白的行跳过。一行不超过 `MaxStdioLineBytes`（缺省 4194304 字节，即 4 MiB，与 HTTP 入口的请求体上限相同；行尾与开头的 BOM 不算），刚好等于上限的照常处理；超过的一行读到行尾为止丢弃、不再缓冲，同样答复解析错误，接着读下一行，会话不结束（ADR-0331）。要收更长的消息就配大 `XiHan:Protocols:Mcp:MaxStdioLineBytes`。解码之后的每一行同样过上面的共同检查，`method` 或 `id` 里转义出的孤立代理项答复解析错误、接着读下一行，不再让会话异常结束。取消在读一行的中途到达时，每读一块之前核对，`RunAsync` 抛出 `OperationCanceledException`。答复里的非 ASCII 字符按 JSON 转义成 `\uXXXX`，只含 ASCII，按任何控制台输出代码页写出都是合法的 UTF-8。

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project framework/samples/SimpleCli -- mcp
```

自己托管、不经命令行宿主时，把 stdin 的字节流交给 `McpStdioTransport.RunAsync`，不要用 `Console.In`（它在 Windows 上按控制台代码页解码）；流由调用方释放：

```csharp
// services 是装配了 McpProtocolFeature 的根服务
var server = services.GetRequiredService<McpServer>();
await using var stdin = Console.OpenStandardInput();
await McpStdioTransport.RunAsync(server, services, stdin, Console.Out, cancellationToken);
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

每个 `tools/call` 经完整管道，凭据在这次调用的 Authenticate 阶段认证；stdio 没有外层认证，凭据无效时需要认证的 tool 得到 `isError`（`SECURITY_DENIED_003`），会话继续。选项不合法（凭据不是 `<scheme> <value>`、时限不是正数）时不进入会话，stderr 写一行用法错误（`detail` 按 `--locale` 翻译，没给或不合法时是默认区域），退出码 64。调用来源只有协议 `mcp` 与 tool 名。宿主的搭建见[命令行宿主](/protocols/command-line)。

## 选项

配置节 `XiHan:Protocols:Mcp`（`McpProtocolOptions`）有服务端的名字、版本与说明（`ServerName`、`ServerVersion`、`Instructions`，后者是给大模型的使用说明，空时不写）、按操作的 tool 名（`Tools:<操作 ID>`）、HTTP 入口允许的浏览器源（`AllowedOrigins`）与请求体上限（`MaxRequestBodyBytes`）、stdio 一行的字节上限（`MaxStdioLineBytes`），类型与缺省值见[包参考](/packages/protocols-mcp#配置)。

`initialize` 总是答复本服务端的协议版本与 `capabilities.tools.listChanged = false`，由客户端决定是否继续；`ping` 答复 `{}`。

tool 表跟随操作登记表（ADR-0251）：`tools/list` 与 `tools/call` 按处理这条消息时的登记表，插件运行期加载的操作随即是 tool，卸载之后随即不是（`tools/call` 得 `PROTOCOLS_NOTFOUND_002`）。服务端建立之后登记的操作 tool 名不合法、与已有的或同一组里的重名，整组不登记，登记返回 `PROTOCOLS_VALIDATION_004` / `PROTOCOLS_CONFLICT_001`。

## 常见问题

### tool 列表里没有我的操作

依次检查：`[Operation]` 的 `Expose` 是否含 `Mcp`；`Stability` 是否为 `Internal`（内部操作不在任何协议上发布）；操作所在的特性是否 `UseFeature` 了、`Configure` 里是否调用了 `AddGeneratedOperations()`。用命令行宿主的 `operations` 命令能直接看到每个操作暴露在哪些协议上。

### 标题与描述是空的

`tools/list` 只在消息目录里有 `<操作 ID>.title` / `<操作 ID>.description` 键时才输出这两个字段。确认语言文件里有这两个键、文件以 `LogicalName="Localization/%(Filename)%(Extension)"` 嵌入、`AddMessageCatalog` 已注册。操作 ID 带连字符时键里要换成下划线：`tenant-members.list` 的键是 `tenant_members.list.title`。

### 客户端每次都要求确认我的查询 tool

MCP 客户端按 `destructiveHint` 决定是否确认。只有 `get`、`list` 两个动词自动算只读，`search`、`export` 这类自定义动词要在 `[Operation]` 上写 `ReadOnly = true`，否则按默认标成可能破坏。

### 能用 SSE 或长连接吗

不能。这个实现只做 Streamable HTTP 的请求 / 响应部分：一条 `POST` 一个答复，不开 SSE 流、不建会话。服务端到客户端的通知与 `listChanged` 都不支持：tool 列表随登记表变化（插件加载与卸载），客户端重新 `tools/list` 才看得到。

## 相关包与下一步

- 包参考：[Protocols.Mcp](/packages/protocols-mcp)、[Hosting.CommandLine](/packages/hosting-commandline)
- 标题与描述的来源：[全球化和本地化](/common/localization)
- 契约与 Schema：[操作契约](/web/operations)
- 用 MCP 开发框架本身（给 Agent 的 MCP Server）：[MCP Server](/agents/mcp)
- 规范：RFC-0011《操作契约与 XGI》
- 下一步：[CloudEvents](/protocols/cloudevents)，把 HTTP 上的事件接进消息子系统
