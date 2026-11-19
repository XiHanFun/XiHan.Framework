# ADR-0244 Result 与 Error 的线上 JSON 编码

## 状态

已采纳（规范 `rfcs/0001-primitives.md` 的 .NET 映射、README 的生态一节与 `CHANGELOG.md` 随之修订；线上编码的规则与向量不变）。

## 日期

2026-11-19

## 背景

RFC-0001「Result」规定了 Result 与 Error 的 JSON 线上编码，向量是 `primitives/wire-encoding.json`。六个 SDK 都实现了这套编码，也都回放这个向量。

.NET 一直没有实现它。原语层不含序列化代码，内核也没有，线上的 Error 走插件协议的 protobuf `Status` 与 HTTP 的 Problem Details。ADR-0242 先把这个向量豁免，ADR-0243 按一致性档改登为 `core` 里唯一的缺口。由此带来两个问题：参考实现在 `core` 里有缺口；.NET 应用与 SDK 交换 Result（消息载荷、工作流状态、应用自己的通道）时，只能各写一份编码。

动手前核对过现状：.NET 里没有 `"ok"` 的编解码，也没有 Result 的 `JsonConverter`。插件协议的 `PluginJson` 已有错误细节的值模型读写，内核已有 `JsonText` 共同检查。

## 备选

1. **放进原语层**：否决。RFC-0001 写明原语层不含序列化代码，`DependencyTests` 守护原语层「无任何引用」。System.Text.Json 虽在共享框架里，原语层加编码也会破坏零行为的约定，冻结的一层还会随编码细节变化。
2. **System.Text.Json 的转换器**：否决，原因有三：
   - `Result<T>` 的转换器工厂要按类型参数构造泛型转换器，这需要反射，而内核零反射。
   - 转换器的 `Read` 只能用异常表达不合法，与「可恢复的失败返回 Result」不合。
   - 转换器拿不到整份原文，重复成员与孤立代理项要在整份文本上判定。
3. **把编码不合法与对方的失败合成一个 `Result<T>`**：否决。对方也可能在线上发 `PRIMITIVES_VALIDATION_001`，调用方分不出是谁的失败。
4. **新增内核错误码**：否决。六个 SDK 对不合法的编码都报 `PRIMITIVES_VALIDATION_001`，同一件事不该各语言一个码。
5. **经内部入口构造文案只有空白的 Error**：否决。`Error` 对外保证文案不是空白（`Literal` 与 `WithText` 都拒绝），读入的错误不能处于公开 API 构造不出的状态。
6. **写出时把整数值的 `double` 补成 `2.0`**：否决。写出时已不知道数字原来的类型，要做到只能重建序列化选项或替换调用方的转换器，两者都改变了调用方 `JsonTypeInfo<T>` 的契约。无类型的值用 `JsonElement`，原文本来就保留。

## 决策

1. **内核新增 `XiHan.Framework.Kernel.Json.ResultJson`**（`Stable`，5.0）：
   - 写出：`Write(Result)`、`Write<T>(Result<T>, JsonTypeInfo<T>)`、`WriteMaybe<T>(Result<Maybe<T>>, JsonTypeInfo<T>)`、`WriteError(Error)`，返回文本。
   - 读取：`Read(string)` 返回 `Result<Result>`，`Read<T>(string, JsonTypeInfo<T>)` 返回 `Result<Result<T>>`，`ReadMaybe<T>` 返回 `Result<Result<Maybe<T>>>`，`ReadError(string)` 返回 `Result<Error>`。外层失败表示编码不合法，外层成功时内层才是对方的成功或失败。
2. **写出照 RFC 逐字**：
   - 无值成功只写 `{"ok":true}`。
   - `Error` 的成员顺序是 `code`、`message`、`messageKey`、`details`、`inner`，`message` 取 `Error.Message`。没有键、细节为空、没有内部错误时，对应成员不写。
   - 细节按值模型写出：整数值的浮点带小数点，非有限值写成字符串。
   - 成功值写出来是 `null` 时抛 `ArgumentException`，可能没有的值用 `WriteMaybe`。`default` 的结果按 `PRIMITIVES_VALIDATION_002` 失败写出。
3. **读取的顺序**：
   - 文本按严格的 UTF-8 编码，先过 `JsonText` 共同检查。
   - 再核对外层：`ok`、`value` 与 `error` 的取舍。
   - 然后核对 `Error`，`inner` 递归。
   - 最后按 `JsonRequestShape` 与 `JsonTypeInfo<T>` 读值。
   - 失败是 `PRIMITIVES_VALIDATION_001`，消息键 `kernel.wire.invalid_json` 或 `kernel.wire.invalid`，`reason` 是出错的路径。
4. **读出的 Error**：
   - 带键的读成 `new Error(code, key, details, inner).WithText(message)`，没有键的读成 `Error.Literal`。
   - 空的 `details` 读成没有细节。
   - 细节按值模型读（`long`、`double`、字符串、布尔、空、列表、字典），溢出的数读成 ±∞，与 Go 和 System.Text.Json 一致。
   - `message` 只有空白时读成不合法，写进 RFC 的 .NET 映射。
   - 还原的代码在内部类型 `WireErrorReader`，它用的是线上的键与文案，不是常量，所以登记进架构测试允许线上还原的文件（`LocalizationTests`）；`ResultJson` 自己构造的错误仍受「只用消息键常量」的检查。
5. **值模型共用一份**：读写从插件协议的 `PluginJson` 挪进内核的内部类型 `JsonValues`，`PluginJson` 与 `ResultJson` 都用它。插件协议的行为不变：`ParseDetails` 仍把溢出的数当作没有细节。
6. **消息键**：新增 `kernel.wire.invalid_json` 与 `kernel.wire.invalid`，10 个语言文件同时补齐。
7. **测试**：
   - Kernel.Tests 新增 `WireEncodingVectorTests`，回放 results、errors 与 duplicates，按值模型比较：整数与浮点不相等，成员顺序无关。
   - `ResultJsonTests` 核对读出的值、`reason` 路径与写出一侧的约束。
   - `SpecVectorCoverageTests` 的缺口表清空。
8. **规范与文档**：
   - RFC-0001「跨语言语义」之后的 .NET 映射改写。
   - 规范 README 生态一节里 .NET 的缺口改为「无」，CHANGELOG 记一条 PATCH。
   - 文档站的 `docs/appendix/spec.md` 与 `docs/packages/kernel.md` 同步。

## 理由

- 放在内核，原语层保持零行为与冻结，内核已有共同检查、路径写法与载荷形状核对，编码直接复用它们，与操作载荷的读取同一套判定。
- 读取返回结果的结果，「编码不合法」与「对方说失败」在类型上就分开，不靠错误码猜。
- 值经调用方的源生成元数据，零反射、可裁剪，与操作的请求与响应同一种用法。

## 影响

- 线上的规则与向量都不变，六个 SDK 不受影响；参考实现在认领的档里没有缺口了。
- RFC 的 .NET 映射写明 .NET 与「Result」一节的两处差异：
  - `message` 只有空白时，.NET 读成不合法，其他语言照收。要让各语言一致，需要把规范收紧为「不能只有空白」并补向量，六个 SDK 跟着改。`v1.0.0` 之前的 rc 允许这种改动，留待以后决定。
  - 类型化的值里，整数值的 `double` 写成整数。无类型的对端按值模型会读成整数，要原样往返用 `JsonElement`。
- HTTP 与插件协议的错误编码（Problem Details、protobuf `Status`）不改用这套编码。
- 测试做过反证：把浮点写出时补的 `.0` 去掉，向量测试报出「细节里整数值的浮点」。
