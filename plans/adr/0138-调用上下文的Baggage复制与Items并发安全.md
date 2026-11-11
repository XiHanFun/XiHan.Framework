# ADR-0138 调用上下文的 Baggage 复制与 Items 并发安全

## 状态

已采纳（规范 `rfcs/0001-primitives.md` 的 `InvocationContext` 注释修订）。

## 日期

2026-11-11

## 背景

P5-05 的 API 审查发现 `InvocationContext` 有两处正确性问题。它在原语层，发布后形状和语义都不能再改：

1. **`Baggage` 校验后仍引用调用方的字典**。`init` 只检查一遍键的格式，就原样保存调用方传进来的字典。调用方之后再改这个字典，有两个后果：
   - 键格式校验被绕过；
   - 已经建好、可能已经交给别处的上下文被改动。
2. **`Items` 并发写入会损坏**。`Items` 是普通的 `Dictionary<object, object?>`，`WithBaggage` 与 `WithCancellation` 派生出的上下文共享同一个实例。设计上有意共享：中间件在派生上下文上写入的结果槽，上游要能看到。但并行的分支各自经派生上下文写入时，会损坏这个字典。

审查同时建议把 `Items` 换成强类型的袋（把 Kernel 的 `ContextItemKey<T>` 下沉到原语层）。复核后发现，BasicApp 和测试都把 `Items` 当通用的键值袋用，例如 `context.Items.ContainsKey(...)`、以字符串作键。这与 ASP.NET Core 的 `HttpContext.Items` 同形，Kernel 的 `ContextItemKey<T>` 已经在它之上提供了强类型访问。换成强类型袋要搬动类型的命名空间，还会破坏这些用法，不值得。

## 决策

- `Baggage` 的 `init` 校验通过后，存一份 `FrozenDictionary<string, string>`（序数比较）。`WithBaggage` 内部本来就新建字典，行为不变。
- `Items` 的存储改为 `ConcurrentDictionary<object, object?>`。公开类型仍是 `IDictionary<object, object?>`，派生上下文之间照旧共享。
- 测试：
  - `Baggage_IsCopiedAtConstruction_SoTheCallerCannotChangeItLater`：构造之后改原字典，上下文不变。
  - `Items_SharedByDerivedContexts_TolerateConcurrentWriters`：8 个派生上下文并行各写 2000 项，全部可见、不损坏。
  - 两个测试在修复前都失败。

## 影响

- 公开 API 不变。
- 原先在构造之后改原字典、指望上下文跟着变的代码不再生效，这本来就不在契约之内。
- `Items` 的读写多一点并发字典的开销；一次调用写入的项很少，可以忽略。
