# ADR-0199 MCP 的 HTTP 入口校验源与内容类型

## 状态

已采纳（`spec/protocol/mcp.md`「生命周期与传输」与错误表修订，`spec/conventions/error-codes.md` 登记三个错误码）。

## 日期

2026-11-12

## 背景

5.0 安全审查 S-12（`plans/5.0-安全审查.md`），中危，这里复现确认。

- `MapXiHanMcp` 的 `POST` 端点不校验 `Origin`，也接受任意 `Content-Type`。MCP 规范要求服务端校验 `Origin`，带了无效的就回 403。
- 开发者本机开着 MCP 服务时访问一个网页，网页用 `text/plain` 发出的简单请求不经预检就到达端点，能调用匿名 tool。借 DNS 重绑定，网页还能读到结果。
- 请求体用 `CopyToAsync` 整读进内存，只受 Kestrel 的 30 MB 限制。HTTP 操作端点在 ADR-0176 已经设了上限，MCP 端点漏了。

复现：`McpProtocolTests` 的 `HttpEntry_*` 与 `AllowedOrigins_MustBeOrigins`。修正前，带别处 `Origin` 的请求、`text/plain` 的请求、超过上限的请求体都照常执行。

## 备选

1. **只要求 `application/json`，不校验 `Origin`**：否决。挡得住简单请求，挡不住 DNS 重绑定之后同源发出的预检请求，也不符合 MCP 规范。
2. **缺省允许 `localhost` 源**：否决。DNS 重绑定之后，攻击网页的源就是攻击者的域名；允许 `localhost` 只方便本机浏览器客户端，安全上没有收益，缺省应当最窄。
3. **按白名单校验 `Origin`、只收 JSON、请求体设上限**：采纳。

## 决策

1. **顺序**：源 → 内容类型 → 协议版本头 → 请求体大小。任一不过即以 JSON-RPC 错误答复（没有 `id`，码 -32600，`data.code` 是框架错误码），不进入 JSON-RPC。
2. **源**：
   - 请求带 `Origin` 时，它必须是 `XiHan:Protocols:Mcp:AllowedOrigins` 之一，否则 403、`PROTOCOLS_DENIED_001`。
   - 按 `scheme://host[:port]` 比较：不分大小写，缺省端口省略。`null` 与解析不了的值一律拒绝。
   - 配置项只收 http 与 https 的源（没有路径、查询、片段、用户信息），别的值启动失败。
   - 缺省列表为空。不带 `Origin` 的非浏览器客户端不受影响。
3. **内容类型**：必须是 `application/json`（不分大小写，忽略参数），否则 415、`PROTOCOLS_VALIDATION_015`。
4. **请求体**：最多 `XiHan:Protocols:Mcp:MaxRequestBodyBytes`（正整数，缺省 4 MiB），否则 413、`PROTOCOLS_VALIDATION_016`。`Content-Length` 已经超过的不读；没有声明长度的边读边计数，读到上限多一个字节即停。
5. **消息键**：`protocols.mcp.origin_denied`、`protocols.mcp.content_type_invalid`、`protocols.mcp.request_body_too_large`，10 种语言。

## 影响

- 浏览器里的 MCP 客户端（MCP Inspector 等）要把它的源配进 `AllowedOrigins`。
- 用 `text/plain` 或不写 `Content-Type` 的客户端要改发 `application/json`。
- stdio 传输不受影响。
- 公开 API：`McpProtocolOptions.AllowedOrigins`、`MaxRequestBodyBytes`、`DefaultMaxRequestBodyBytes`、`IsAllowedOrigin`；`McpErrors.OriginDenied`、`ContentTypeInvalid`、`BodyTooLarge`；三个消息键常量。
