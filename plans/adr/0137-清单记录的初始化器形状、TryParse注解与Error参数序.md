# ADR-0137 清单记录的初始化器形状、TryParse 注解与 Error 参数序

## 状态

已采纳（规范 `rfcs/0001-primitives.md` 修订）。

## 日期

2026-11-11

## 背景

原语层只增不改（RFC-0001「冻结规则」），一旦随 5.0.0 发布，形状就永远改不了。P5-05 的 API 审查在这一层发现三处问题：

1. **清单记录的形状不一致**。
   - `PipelineContribution` 是清单模型里唯一的位置记录：`Middleware` 被重定义为只读，`Stage` 却能 `init`；`Deconstruct` 输出的 `Before?` / `After?` 是可空的，属性本身却是非空的。
   - `Binding(name, type, required = true)`、`CapabilityRequirement(id, version, optional = false)`、`ConfigContribution(section, schema = null)` 都靠可选参数扩展。以后再加字段只能追加参数，而追加参数会让已编译的调用方找不到旧签名。
2. **`TryParse` 的可空注解不对**。`CapabilityDeclaration.TryParse` 与 `CapabilityRequirement.TryParse` 失败时 `out` 出 `null!`，签名却标成非空。
3. **`Error.Unexpected` 的参数顺序反了**。它是 `(code, key, inner, details)`，构造函数与 `Literal` 都是 `(code, key, details, inner)`；有调用方为了绕开这个差异，只能写 `details:` 具名参数。

## 决策

1. **清单记录统一成「必需进构造、可选走初始化器」**：
   - `Binding(name, type)`，`Required { get; init; } = true`。
   - `CapabilityRequirement(id, version)`，`Optional { get; init; }`。
   - `ConfigContribution(section)`，`Schema { get; init; }`。
   - `PipelineContribution(middleware, stage)` 改为非位置记录，`Before` / `After` 为 `init` 属性，缺省是空列表。它们标 `[AllowNull]`，赋 `null` 按空处理，`ManifestReader` 可以把 JSON 里缺省的数组直接交进来。
   - 以后再加字段只加 `init` 属性。
2. **`TryParse` 签名**：改为 `TryParse(string?, [NotNullWhen(true)] out T?)`，失败时输出 `null`。
3. **`Error.Unexpected` 参数顺序**：改为 `(code, messageKey, details = null, inner = null)`，与构造函数、`Literal` 一致。
4. **改写调用方**：
   - 框架内 14 处按位置传 `inner` 的调用：四个参数的交换后两个，三个参数的改成具名的 `inner:`。
   - 17 处可选构造参数改写成初始化器，包括 `ManifestReader` 与各 Provider 的清单。

## 影响

- 源码迁移：
  - `new Binding(name, type, required: false)` → `new Binding(name, type) { Required = false }`
  - `new CapabilityRequirement(id, version, optional: true)` → `{ Optional = true }`
  - `new ConfigContribution(section, schema)` → `{ Schema = schema }`
  - `new PipelineContribution(m, s, Before: [...])` → `{ Before = [...] }`
  - 位置解构 `PipelineContribution` 的代码改为读属性
  - `Error.Unexpected(code, key, inner)` → `Error.Unexpected(code, key, inner: inner)`；四个参数的交换后两个
- 用到 `TryParse` 的代码：成功分支里的值不变；可空分析会要求在失败分支里不再读它。
- 原语层在 5.0.0 之前修改，不需要登记豁免（ADR-0131）。BasicApp 在清单里写了 `optional: true` 等可选参数，升级时要按上面的写法改。
