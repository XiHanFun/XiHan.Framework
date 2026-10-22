# XiHan.Framework.Protocols.Mcp

MCP（Model Context Protocol）协议适配器（Ring 5）。提供 `protocols.mcp@1`，把登记表里标记为 `Expose.Mcp` 的操作发布为 MCP tool（RFC-0011、`spec/protocol/mcp.md`）：JSON-RPC 2.0，Streamable HTTP 传输，协议版本 `2025-06-18`。

```csharp
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<McpProtocolFeature>().UseFeature<OrdersFeature>());
var app = builder.Build();
app.UseXiHan();        // 构造上下文、认证请求凭据、回写 Problem Details
app.MapXiHanMcp();     // POST /mcp：initialize、ping、tools/list、tools/call
```

## 规则

- tool 名：`{resource}_{verb}`（`orders.create` → `orders_create`），显式覆盖 `XiHan:Protocols:Mcp:Tools:<操作 ID>`；文法 `^[A-Za-z0-9_-]{1,64}$`，不合法或重复即启动失败。`Stability.Internal` 与 `Expose` 不含 `Mcp` 的操作不发布。
- `tools/list`：`inputSchema` 取操作的请求 Schema（`Kernel.Generators` 产出，`type` 必须是 `object`，否则为 `{"type":"object"}`），`outputSchema` 取响应 Schema（仅对象类型）；`annotations` 按动词推导（`get` / `list` 只读幂等，`delete` 破坏性，`create` 不幂等）；`title` 与 `description` 来自消息目录里的 `<操作 ID>.title` / `<操作 ID>.description`（ID 里的 `-` 换成 `_`），按 `Accept-Language` 翻译，没有就不给。不分页。
- `tools/call`：`arguments`（缺省为 `{}`）作为 XGI 载荷，以新的调用上下文经完整内核管道执行（认证、按契约授权、事务）；成功时 `content[0].text` 是响应 JSON，响应为对象时同时给 `structuredContent`；操作失败以 `isError: true` 返回，文本为 `错误码: 文案`。未登记的 tool、非对象参数是 JSON-RPC 错误（-32602）。
- 传输：`POST` 一条消息一个答复（`application/json`）；通知回 202；非 JSON 或非 JSON-RPC 2.0 的消息回 400；`MCP-Protocol-Version` 头不受支持时回 400；不开 SSE 流、不建会话，`GET` / `DELETE` 为 405。请求凭据由 `UseXiHan()` 在外层认证，凭据无效时整个请求以 Problem Details 拒绝。
- 选项 `XiHan:Protocols:Mcp`：`ServerName`（默认应用名）、`ServerVersion`（默认框架版本）、`Instructions`、`Tools:<操作 ID>`。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Hosting.AspNetCore` 与 `XiHan.Framework.Localization.Abstractions`，以及 `Microsoft.AspNetCore.App`。协议适配器在宿主之上（Ring 5）。

## 状态

P3：可用（Streamable HTTP 传输）。stdio 传输随 `Hosting.CommandLine` / `Cli` 复用 `McpServer`。
