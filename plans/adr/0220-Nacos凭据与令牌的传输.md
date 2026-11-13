# ADR-0220 Nacos 凭据与令牌的传输

## 状态

已采纳（RFC-0014「Nacos Provider」一条修订）。

## 日期

2026-11-13

## 背景

5.0 安全审查「加固建议」（`plans/5.0-安全审查.md` 第四节）。`Configuration.Nacos` 三处：

- **令牌在查询串里**：登录后的每个请求把 `accessToken=<令牌>` 拼进地址。查询串会进 Nacos、网关与代理的访问日志，令牌在有效期内能拿去读全部配置。
- **失败原因带响应正文**：非 2xx 时把整段响应正文放进异常消息，再进错误结果的 `reason` 与日志。正文里可能有服务端回显的请求内容。
- **http 上明文传口令**：地址是 `http` 时，登录把用户名与口令以表单明文发出，令牌也随每个请求明文发出。

复现：`NacosTransportTests.Token_TravelsInTheAuthorizationHeader_NotTheQueryString` 与 `FailureReason_CarriesTheStatus_ButNotTheResponseBody`。修正前，前者的请求没有 `Authorization` 头、令牌在查询串里，后者的原因里有响应正文。

## 决策

1. **令牌**：经请求头 `Authorization: Bearer <令牌>` 发送，不进查询串。Nacos 1.x 的鉴权与 2.x 的缺省鉴权插件都先认这个头，再看 `accessToken` 参数。
2. **失败原因**：只带动作（`fetch` / `listen`）与状态码。
3. **http 上的凭据**：配置了 `UserName` 或 `Password` 时，非回环的 `http` 地址让建客户端时抛出 `ArgumentException`（消息列出地址，不带凭据）。
   - 回环地址（`localhost`、`127.0.0.1`、`::1`）不出本机，不要求。
   - 新选项 `AllowInsecureHttp`（配置键 `XiHan:Configuration:Nacos:AllowInsecureHttp`，默认 `false`）：链路另有保护（同机、服务网格的双向 TLS）时显式打开。
   - 没有凭据时 `http` 照常。

## 影响

- 破坏性变更：配置了用户名密码、地址是非本机 `http` 的应用，启动从成功变为失败。迁移：改用 `https`，或确认链路受保护后设 `AllowInsecureHttp` 为 `true`。
- 经网关转发、网关只放行 `accessToken` 查询参数而丢掉 `Authorization` 头的部署，要让网关透传这个头。
- 公开 API：`NacosOptions.AllowInsecureHttp`。
