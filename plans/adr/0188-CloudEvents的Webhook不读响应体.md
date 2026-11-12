# ADR-0188 CloudEvents 的 Webhook 不读响应体

## 状态

已采纳（`spec/protocol/cloudevents.md` 出站一节修订）。

## 日期

2026-11-12

## 背景

5.0 安全审查 S-26（`plans/5.0-安全审查.md`），低危。

- `CloudEventsWebhookPublisher.SendAsync` 用 `HttpClient.SendAsync(request, ct)`，缺省 `ResponseContentRead`：先把接收方的整个响应体缓冲进内存（上限是缺省的约 2 GiB），再看状态码。
- 结果只用到状态码，响应体从不读取。接收方是第三方，返回超大的正文或一点点慢慢吐，就能让宿主为每次投递缓冲上 GiB 或长期占着连接。

复现：`WebhookResponseTests`。修正前，2xx 与 5xx 两种响应的 64 MiB 正文都被整个读完。

## 决策

- 改用 `ResponseHeadersRead`：读到响应头即按状态码判定，响应体一个字节也不读，释放响应时连接随之中止或回池。
- 状态码的映射不变：2xx 成功，其余 `PROTOCOLS_UNAVAILABLE_001`（详情带 `status`），连接失败 `PROTOCOLS_UNAVAILABLE_002`。

## 影响

- 行为：接收方的响应体不再被读取。以前慢慢吐正文的接收方会让投递一直等到正文读完，现在读到响应头即返回。
- 公开 API 不变。
