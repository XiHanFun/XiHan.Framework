# MCP 载体

> 规范性。参考实现：`XiHan.Framework.Protocols.Mcp`。Model Context Protocol 版本 `2025-06-18`，消息为 JSON-RPC 2.0，传输为 Streamable HTTP。

## 上下文映射（RFC-0003）

Streamable HTTP 传输复用 HTTP 载体的头部映射（`protocol/http.md`）：trace、Baggage、截止时间、`Accept-Language`、`Authorization` 都取自承载 JSON-RPC 消息的 `POST` 请求头。每个 `tools/call` 是一次独立调用：以同一请求头构造新的调用上下文（自己的服务作用域、工作单元与管道），不与承载请求的外层调用共享。

## 操作映射（RFC-0011）

- tool 名按 `conventions/operation-ids.md` 推导：`{resource}_{verb}`；显式覆盖写在 `XiHan:Protocols:Mcp:Tools:<操作 ID>`。文法 `^[A-Za-z0-9_-]{1,64}$`；不合法或两个操作同名即启动失败（`PROTOCOLS_VALIDATION_004` / `PROTOCOLS_CONFLICT_001`）。
- 发布范围：`Expose` 含 `Mcp` 且非 `Stability.Internal` 的操作。
- `tools/list`（不分页）：
  - `inputSchema` = 操作的请求 Schema（`OperationSchema.Request`，`type` 须为 `object`，否则 `{"type":"object"}`）；
  - `outputSchema` = 响应 Schema，仅当其 `type` 为 `object`；
  - `title` / `description` = 消息目录里 `<操作 ID>.title` / `<操作 ID>.description`（ID 里的 `-` 换成 `_`）按调用方区域的文案，没有就不给；
  - `annotations.readOnlyHint` / `destructiveHint` / `idempotentHint` 按动词：`get` / `list` 只读且幂等，`update` 幂等，`delete` 破坏性且幂等，`create` 既不破坏也不幂等，其他动词按 MCP 默认（可能破坏、不幂等）。
- `tools/call`：`arguments`（缺省为 `{}`，必须是对象）就是 XGI 载荷。成功：`content[0]` 为 `text`，文本是响应 JSON；响应为对象时同时给 `structuredContent`。操作失败（校验、未授权、冲突……）：`isError: true`，`content[0].text` = `<错误码>: <文案>`，文案按调用方区域翻译；不是协议错误。
- JSON-RPC 错误：`error.data.code` 携带框架错误码。

| 情形 | `error.code` | `error.data.code` | HTTP |
| --- | --- | --- | --- |
| 请求体不是 JSON | -32700 | `PROTOCOLS_VALIDATION_001` | 400 |
| 不是 JSON-RPC 2.0 请求（非对象、`jsonrpc` 不是 `2.0`、`method` 缺失、`id` 为 `null`、`params` 非对象） | -32600 | `PROTOCOLS_VALIDATION_001` | 400 |
| `MCP-Protocol-Version` 头不受支持 | -32600 | `PROTOCOLS_VALIDATION_002` | 400 |
| 方法不受支持 | -32601 | `PROTOCOLS_NOTFOUND_001` | 200 |
| `tools/call` 缺 `name` | -32602 | `PROTOCOLS_VALIDATION_001` | 200 |
| tool 未登记 | -32602 | `PROTOCOLS_NOTFOUND_002` | 200 |
| `arguments` 不是对象 | -32602 | `PROTOCOLS_VALIDATION_003` | 200 |

## 生命周期与传输

- `initialize`：答复 `protocolVersion`（固定为本服务端版本；客户端请求其他版本时也如此，由客户端决定是否继续）、`capabilities.tools.listChanged = false`、`serverInfo`（`name` 默认应用名、`version` 默认框架版本，可由 `XiHan:Protocols:Mcp:ServerName` / `ServerVersion` 覆盖）、可选 `instructions`（`XiHan:Protocols:Mcp:Instructions`）。
- `ping` → `{}`。
- 通知（没有 `id` 的消息，如 `notifications/initialized`）与客户端发来的响应（有 `result` / `error` 而无 `method`）不答复：HTTP 202，无正文。
- `POST <pattern>`（默认 `/mcp`）一条消息一个答复，`Content-Type: application/json; charset=utf-8`；不开 SSE 流，不建会话（不发 `Mcp-Session-Id`）；`GET` / `DELETE` 为 405。
- 请求凭据由宿主在外层认证：凭据无效时整个请求以 Problem Details 拒绝（403 `SECURITY_DENIED_003`），不进入 JSON-RPC；应用未就绪时 503。按契约的授权在每个 `tools/call` 的管道里判定，未授权是 `isError` 结果。
