# ADR-0200 CloudEvents 入站只收映射过的类型

## 状态

已采纳（`spec/protocol/cloudevents.md`「入站」一条修订，`spec/conventions/error-codes.md` 的 `PROTOCOLS_VALIDATION_009` 改写说明）。

## 日期

2026-11-12

## 背景

5.0 安全审查 S-13（`plans/5.0-安全审查.md`），中危。审查时按规范判定为设计，这里改设计。

- 入站事件的主题先查 `XiHan:Protocols:CloudEvents:Topics:<type>`，没有映射时以类型本身为主题，只要它合主题文法。
- 宿主对端点授权（如 `RequireXiHanPermission("events.deliver")`），授的是「投递事件」，不分类型。
- 只该投 `orders.placed` 的发送方，把 `ce-type` 写成任意有订阅的主题名，就能投进那个主题，包括插件订阅的主题。事件的租户与主体也由它自报（ADR-0121 接受了这一点，前提是主题由宿主决定）。ADR-0121 接受的风险因此从一个主题扩大到全部主题。

复现：`CloudEventsProtocolTests.Inbound_RejectsUnmappedTypes_UnlessAccepted`。修正前，只映射了 `orders.placed` 的宿主照样把类型 `notices` 派发给 `notices` 的订阅者。

## 备选

1. **按类型分别授权（每个类型一个权限码）**：否决。授权模型要为一个协议适配器长出新的维度；类型到主题的映射本来就是宿主对「哪些类型能进来」的声明。
2. **保持缺省，加一个「只收映射过的类型」开关**：否决。缺省仍然开放，一个忘了配开关的宿主就落回原来的风险。
3. **缺省只收映射过的类型，开放要显式打开**：采纳。

## 决策

1. **缺省**：没有在 `Topics` 映射的类型是 400 `PROTOCOLS_VALIDATION_009`，消息键 `protocols.cloudevents.type_unmapped`（参数 `type`，10 种语言）。batch 里任一事件没有映射，整批拒绝。
2. **开放**：`XiHan:Protocols:CloudEvents:AcceptUnmappedTypes = true` 时，没有映射的类型以它本身为主题，要合主题文法，否则仍是 `PROTOCOLS_VALIDATION_009`（沿用原消息键 `protocols.cloudevents.topic_unresolved`）。只在发送方可信时打开。
3. `CloudEventsProtocolOptions.TopicFor` 按同一规则返回主题。

## 影响

- 破坏性变更：依赖「类型直接当主题」的应用，入站事件得到 400。
  - 迁移：把类型映射进 `Topics`（值可以与类型同名）。发送方可信时也可以打开 `AcceptUnmappedTypes`。
- 示例 SimpleApp 已经映射了 `orders.placed`，不受影响。
- 公开 API：`CloudEventsProtocolOptions.AcceptUnmappedTypes`、`CloudEventsMessages.TypeUnmapped`。
