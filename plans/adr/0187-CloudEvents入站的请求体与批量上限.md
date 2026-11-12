# ADR-0187 CloudEvents 入站的请求体与批量上限

## 状态

已采纳（`spec/protocol/cloudevents.md` 入站一节修订，新增错误码 `PROTOCOLS_VALIDATION_013`、`PROTOCOLS_VALIDATION_014`）。

## 日期

2026-11-12

## 背景

5.0 安全审查 S-20（`plans/5.0-安全审查.md`），低危。

- `CloudEventsHttpBinding.ReadAsync` 把请求体整个复制进内存，框架不设上限，只受宿主服务器的请求体上限约束（Kestrel 缺省约 30 MB）。
- batch 的事件数不受限。派发模式下每个事件都在请求内经一次完整的内核管道，几十 MB 的 batch 就是几十万次管道调用。
- 审查还提到不防重放。规范定的是至少一次的语义，处理器按 `id` + `source` 去重；端点的调用方由宿主授权（ADR-0121），能调端点的人本就能发新事件，按时间窗拒绝旧事件挡不住他们。这一点不改。

复现：`CloudEventsInboundLimitTests`。修正前：101 个事件的 batch 与 2 MiB 的事件都返回 202，照常处理。

## 备选

1. **复用 HTTP 协议的 `MaxRequestBodyBytes`**：否决。CloudEvents 不依赖 `Protocols.Http`，事件与操作的大小也不是一个量级。
2. **超限时截断 batch、只处理前面的部分**：否决。静默丢弃事件，发送方以为整批成功。
3. **请求体与 batch 条数各设上限，超过整批 400**：采纳。

## 决策

1. `CloudEventsProtocolOptions` 新增两项，读配置时不是正整数即抛出 `ArgumentException`：
   - `MaxRequestBodyBytes`（缺省 1 MiB）。
   - `MaxBatchSize`（缺省 100）。
2. 请求体：
   - `Content-Length` 已超过的不读。
   - 没有声明长度的边读边计数，超过即停。
   - 结果为 400 `PROTOCOLS_VALIDATION_013`（消息键 `protocols.cloudevents.body_too_large`，参数 `max`）。
3. batch：解析成 JSON 数组后先数元素，超过即 400 `PROTOCOLS_VALIDATION_014`（消息键 `protocols.cloudevents.batch_too_large`，参数 `count`、`max`），一个也不解码。
4. 两者都在解析主题之前判定，一个事件也不处理。
5. `CloudEventsHttpBinding.ReadAsync(request, ct)` 改为 `ReadAsync(request, options, ct)`。

## 影响

- 行为：超过 1 MiB 的请求与超过 100 个事件的 batch 被拒绝，发送方要拆开发，或由宿主调大上限。
- 源码迁移：直接调用 `CloudEventsHttpBinding.ReadAsync` 的代码传入 `CloudEventsProtocolOptions`。
- `Protocols.CloudEvents` 是 `Preview`，GA 前修改。
