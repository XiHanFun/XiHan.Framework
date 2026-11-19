# ADR-0245 Error 的文案不能只有空白

## 状态

已采纳（`spec/rfcs/0001-primitives.md` 新增「空白字符」一节并修订「Result」「Error 与 ErrorCode」，`plugin/invocation.md` §3 与 `plugin/handshake.md` §2 随之修订；向量 `wire-encoding.json`、`error-reading.json` 补上用例）。

## 日期

2026-11-19

## 背景

- RFC-0001「Result」原先只说 Error 的 `message`「是非空字符串」。.NET 的 `Error.Literal` 与 `WithText` 拒绝只有空白的文案（`ArgumentException.ThrowIfNullOrWhiteSpace`），ADR-0244 的 `ResultJson` 读线上编码时也照此拒绝。六个 SDK 照规范只拒绝空串，只有空白的 `message` 照收。这样同一份编码，.NET 判为不合法，其他语言读得出。ADR-0244 把这处差异写进了 RFC-0001，留待决定，用户定为统一成拒绝空白。
- 读入插件写来的 proto `Error` 时，规范说自由文本错误的 `text`「为空时取 `code`」，.NET 的 `PluginErrorMapper.FromProto` 也只查空串。插件写来只有空白的 `text` 时，它把这段文本交给 `Error.Literal`，后者抛出 `ArgumentException`：一个插件的错误回复就能让宿主的错误映射抛异常。
- 各语言内建的「空白」各不相同：
  - Java 的 `Character.isWhitespace` 不含 U+0085、U+00A0、U+2007、U+202F，另含 U+001C–U+001F；
  - Python 的 `str.isspace` 另含 U+001C–U+001F；
  - ECMAScript 的 `\s` 不含 U+0085，另含 U+FEFF；
  - PHP 的 `ctype_space` 只认 ASCII。
  只写「不能只有空白」而不写死集合，七个实现仍会各判各的。

复现：在 `error-reading.json` 补上「自由文本错误的 text 只有空白时取 code」后，.NET 的 `ProtocolVectorTests.ErrorReading_FollowsTheVectors` 修之前确定失败，`Error.Literal` 抛出 `ArgumentException`。

## 备选

1. **规范维持只拒绝空串，.NET 放宽**：否决。`Error` 的文案面向人，只有空白的文案等于没有文案。放宽要改 .NET 原语层已发布的校验，`Stable` 不改。
2. **统一成拒绝空白，按各语言内建的判断**：否决。内建判断互不相同，结果照样不一致。
3. **统一成拒绝空白，把集合写死**：采纳。集合就是 `plugin/handshake.md` §2 已经写死的那 25 个码位，即 Unicode 的 White_Space 属性，与 .NET 的 `char.IsWhiteSpace` 和正则 `\s` 相同。

## 决策

1. RFC-0001 新增「空白字符」一节，写死 25 个码位：U+0009–U+000D、U+0020、U+0085、U+00A0、U+1680、U+2000–U+200A、U+2028、U+2029、U+202F、U+205F、U+3000。规范里凡说「空白」都指它，各语言按这张表判断，不用内建的判断。`plugin/handshake.md` §2 改为引用这一节。
2. 线上编码（「Result」一节）：Error 的 `message` 至少要含一个空白之外的字符，空串与只有空白都不合法。
3. 原语（「Error 与 ErrorCode」一节）：`Literal` 的 `text` 与 `WithText` 的参数不能为空，也不能只有空白，否则是参数非法。
4. 读入 proto `Error`（`plugin/invocation.md` §3）：自由文本错误的 `text` 为空或只有空白时，文案取 `code`。
5. .NET：`PluginErrorMapper.FromProto` 改查 `string.IsNullOrWhiteSpace`。`Error.Literal`、`WithText` 与 `ResultJson` 本来就按 `char.IsWhiteSpace` 判断，与新写死的集合相同，不用改。
6. 向量与规则包：
   - `wire-encoding.json` 新增 5 条必须拒绝的：一个空格、25 个码位各一个、单独的 U+00A0、单独的 U+0085、单独的全角空格。
   - 新增 3 条必须读得出的：两端带空白的文案、U+001C–U+001F、零宽空格加 U+180E 加 U+FEFF。
   - 新增 1 条失败结果的 `error.message` 只有空白。
   - `error-reading.json` 新增 1 条 `text` 只有空白。
   - 规范的规则包（`rules.ReadError`、`rules.ReadProtoError`）改用 `IsWhitespace`。
7. 六个 SDK 照此修改，规范指针升到这次的规范提交。

## 理由

- 一个面向人的字段不该接受「看不见的文案」。.NET 已经这样执行，统一到它不改变参考实现的行为。
- 集合沿用握手行那一份，规范里「空白」只有一个意思。
- 向量专门放了各语言内建判断容易错的码位（U+00A0、U+0085、U+001C–U+001F、U+FEFF），用内建判断的实现回放时就会失败。

## 影响

- 线上行为：只有空白的 `message` 以前在 SDK 里读得出，现在不合法。这是改已经写明的规则，在规范 `v1.0.0` 之前允许，`CHANGELOG.md` 记为 MAJOR。插件写来只有空白的 `text` 以前会让 .NET 宿主抛异常，现在文案取错误码。
- 公开 API 不变。
- 测试：.NET 的 `ErrorReading_FollowsTheVectors` 修之前失败、修之后通过；`Plugins.OutOfProcess`、`Kernel`、`Architecture`、`Plugins.Wasm` 的测试与 Release 构建零警告通过。
