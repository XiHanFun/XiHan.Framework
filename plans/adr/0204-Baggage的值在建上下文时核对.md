# ADR-0204 Baggage 的值在建上下文时核对

## 状态

已采纳（`spec/conventions/baggage-keys.md` 一条修订）。

## 日期

2026-11-12

## 背景

5.0 安全审查 S-23（`plans/5.0-安全审查.md`），低危，这里复现确认。

- 载体只按键名过滤 Baggage（只收已登记的键），值原样进调用上下文。HTTP 的值按 URL 解码，`%0D%0A` 就是换行。
- 这些值写进日志作用域（关联 ID）、审计与消息信封。换行能伪造日志行，超长的值撑大每一条日志与信封，只受服务器的请求头上限约束（Kestrel 缺省 32 KB）。
- HTTP 入站显式给的 `xihan.locale` 不合 BCP 47 文法时照样占位：本地化把它协商成默认区域，`Accept-Language` 却因为「显式给了」不再参与，调用落到默认区域。

复现：`InboundBaggageTests.BaggageValues_TooLongOrWithControlCharacters_AreDropped`、`LocalizationTests.AcceptLanguage_IsNegotiatedIntoLocaleBaggage`。修正前，超长与含换行、制表符的关联 ID 都进了上下文，不合文法的区域挡住了 `Accept-Language`。

## 备选

1. **不合法的值整个请求 400**：否决。W3C Baggage 规定不合法的成员忽略，Baggage 是尽力传播的上下文，不应让一个关联 ID 决定调用成败。
2. **只在 HTTP 载体上校验**：否决。消息扩展、插件元数据、命令行都会建上下文，规则要在一处。
3. **在内核建上下文时丢弃不合法的值**：采纳。

## 决策

1. **原语层**：`BaggageKeys.MaxValueLength = 256`、`BaggageKeys.IsAcceptableValue(value)`：非空、不超长、不含控制字符。
2. **内核**：`App.CreateContext` 丢弃已登记键里不合格的值。
   - 幂等键例外：丢了它，重复的调用会被执行两次。不合法的幂等键照旧由 Idempotency 阶段以 `CACHING_VALIDATION_*` 拒绝。
   - 未登记的键不在这里管，由各载体剥离。
3. **HTTP 载体**：显式给的 `xihan.locale` 不合文法时当作没给，`Accept-Language` 照常协商。

## 影响

- 关联 ID 超过 256 个字符的调用方，关联 ID 不再传播。
- 公开 API：`BaggageKeys.MaxValueLength`、`BaggageKeys.IsAcceptableValue`（原语层只增）。
