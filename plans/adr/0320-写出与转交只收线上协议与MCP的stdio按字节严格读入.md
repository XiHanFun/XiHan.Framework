# ADR-0320 写出与转交只收线上协议与 MCP 的 stdio 按字节严格读入

## 状态

已采纳（规范 RFC-0001「写出与转交」新增三条（Result 与 Error 的线上编码、插件协议的错误细节、为 Schema 校验编出的类型化请求），第一、二条里 stdio 的 MCP 消息挪到「以字节收到」一条，末段改写成范围说明；RFC-0011「校验」第 1 步、`plugin/invocation.md` §3「写出 `Error`」、`protocol/mcp.md`「生命周期与传输」的 stdio 一条，以及 RFC-0001、RFC-0011、RFC-0017 的状态行已改。内核新增 internal 的 `StrictJson`，`ResultJson` 写出严格，`OperationDescriptor.EncodeResponse` 只抛 `ArgumentException`，`ValidationMiddleware` 只在有 Schema 时编码类型化请求、编码严格；`Plugins.Protocol` 的 `PluginJson.SerializeDetails` 写不出即空；`Protocols.Mcp` 的 `McpStdioTransport.RunAsync` 改收 `Stream`，`OpenInput` 删去（`Preview` 的破坏，ADR-0131）。向量、错误码、消息键与配置键不变）。

## 日期

2026-11-27

## 背景

ADR-0312、0315、0318、0319 之后，「写出与转交」里还有几处待定，仓库所有者已拍板：

1. **三处线上写出照写出器的缺省**：System.Text.Json 把孤立的代理项写成转义的 U+FFFD，写出的仍是合法的 JSON，对方读到的是另一个值。
   - `ResultJson.Write`、`WriteMaybe` 与 `WriteError`（Result 与 Error 的线上编码，RFC-0001「Result」）：错误的文案、细节的成员名与值、成功值都这样写；成功值里原样写进去的文本（`JsonElement`、转换器的 `WriteRawValue`）带重复成员或让结果超过 64 层时也照写，`ResultJson.Read` 与各 SDK 读不进。
   - `PluginJson.SerializeDetails`（宿主写给插件的 `Error.details`，`plugin/invocation.md` §3）：ADR-0315 备选 21 记下「写出一侧没有失败，读入一侧把不合法的当作没有细节」，留作待定。
   - `ValidationMiddleware` 为 Schema 校验把类型化请求编成 JSON（RFC-0011「校验」第 1 步）：Schema 校验的是写成 U+FFFD 的另一份请求，操作照常执行。同一个请求以原始调用发来（载荷里是 `\ud800` 这样的转义），共同检查报 `KERNEL_VALIDATION_001`、`reason` 为 `$`。另外，没有 Schema 的描述也编码一次，编出的不用。
2. **范围之外的写出与读入**：诊断输出、类型化存储的序列化、写给外部服务的文本，以及 `xihan pack` / `migrate`、工作流 `Http` 活动的应答正文与 Nacos 的配置内容的读入，所有者定为维持缺省。RFC-0001 的末段以前逐项列出「不在这一条里」的写出，读者看不出这是定下的范围还是遗漏。
3. **MCP stdio 宽松读 stdin**：`McpStdioTransport.OpenInput` 以 `Encoding.UTF8` 打开 stdin，非法的字节换成 U+FFFD，`tools/call` 以另一份参数执行。ADR-0312 只让 `RunAsync` 严格编码读取器交来的字符串，`OpenInput` 读出的行已经不会有孤立的代理项，那道核对在 `mcp` 命令里拦不到任何东西。
4. **`EncodeResponse` 抛两种异常**：超过写出深度时是 System.Text.Json 的 `JsonException`，其余写不出是 `ArgumentException`（ADR-0318「修订」第 5 条）。XGI 入口处理相同，直接调这个 `Stable` 方法的代码要接两种。
5. **ADR-0318「修订」里的三处边角**：不经编码器原样写出的 `\uFFFD` 被当成写不出；序列化上下文的 `MaxDepth` 比 64 小时写出照它；只认原选项的自定义解析器第一次写出即失败。所有者定为维持，写进 XML 文档。

## 备选

范围：

1. **三处并进 RFC-0001「写出与转交」，范围之外写成明确的范围说明**：采纳。
   - 三处都是 XiHan 的线上协议：交给对方按「JSON 文本」读、读不进即失败的内容，与已经在规则里的应答载荷、配置节、信封属性同一类。
   - 末段改成一句范围（只管上面列出的几处）加四类范围之外的写出与读入，注明是定下的范围，不是遗漏。
2. **诊断输出、类型化存储、写给外部服务的文本也并进**：否决，照所有者的决定。诊断输出写不出时没有更好的去处（Problem Details 本身就是失败的结果）；存储的序列化是各子系统自己的格式，写回读出都在同一个实现里；外部服务按它们自己的规则读。
3. **只改 .NET，规范不写**：否决。Result 与 Error 的线上编码与 `Error.details` 是各语言共用的规则（ADR-0233、ADR-0237），规范是事实源。

Result 与 Error 的线上编码：

4. **写不出即抛出 `ArgumentException`**：采纳。`ResultJson.Write` 已有的失败路径就是它（成功值写成 JSON 的 `null`），调用方交来写不出的结果是编程错误；返回值是文本，没有放失败结果的位置。
5. **整段文本过共同检查，层数从结果的根算起**：采纳。对方的读取（`ResultJson.Read`、各 SDK）先过共同检查，写得出的就要读得进；成功值的最外层是第 2 层，单独检查值会多放过一层。
6. **System.Text.Json 自己的 `JsonException` 包进 `ArgumentException`**：采纳，与 `EncodeResponse` 同一个口径（备选 23），只抛一种。
7. **成功值写出之后单独核对字符串，信封只过共同检查**：采纳。本来就是 U+FFFD 的字符按值的编码器也写成转义，在信封上换宽松的编码器再写一遍时，原样嵌进去的值仍是转义，分辨不了；值按它自己的元数据再写一遍才分辨得出（与 `EncodeResponse` 同一份做法）。失败结果里没有成功值，错误对象整个由 `ResultJson` 逐个成员写出，在整段文本上核对。

错误细节：

8. **写不出即这个错误不带细节，其余字段照写**：采纳。
   - 与读入一侧对称：读入时通不过检查的细节当作没有（`plugin/invocation.md` §3），写出一侧不带，两边看到的相同。
   - 回调与发布交回的错误照常交出，插件拿到的错误码与消息键不变。
   - 内部错误的细节各自核对，外层的写得出就照写。
   - Go、Rust 与 PHP 的 SDK 写细节失败时已经是这样（Go 的 `ToProtoError` 在 `jsoncodec.Marshal` 出错时留空，Rust 的 `to_proto_error` 用 `.ok()`，PHP 的 `toProtoError` 接住 `JsonException`）。
9. **写不出让回调或发布失败**：否决。要交出的正是一次失败；为了细节换成另一个错误，插件拿不到原来的错误码。
10. **只去掉写不出的那几项**：否决。半份细节读来像完整的，读入一侧也是整份不要。

为 Schema 校验编出的请求：

11. **编不成即 `KERNEL_VALIDATION_001` + `kernel.operation.invalid_payload`，`reason` 为 `$`**：采纳。请求是调用方的输入；同一个请求以原始调用发来，共同检查报的就是这个结果，两种调用一样。
12. **按未处理的异常结束**：否决。那是把调用方的输入问题报成服务端的缺陷（HTTP 500），与原始调用不一样。
13. **序列化器自己写不出的（浮点成员是 NaN、超过序列化上下文的深度上限）照旧按未处理的异常**：采纳，这次不动。核对只认写出器换出来的 U+FFFD；编出的文本有重复成员或超过 64 层，照旧由第 2 步的共同检查报出。
14. **只在描述带 Schema 时编码**：采纳。编出的只给 Schema 校验用；没有 Schema 时类型化入口不经 JSON（ADR-0318 备选 12），否则带孤立代理项的类型化调用会因一次不用的编码失败。以前没有 Schema 也编码，请求写不出（NaN）时照样失败。
15. **编码也用深度上限放宽一层的选项副本（同 `EncodeResponse`）**：不做。类型化请求 64 层、最里层有值时 System.Text.Json 写不出，按未处理的异常结束，原始调用里同样的 64 层读得进；记在「影响」的发现里。

MCP 的 stdio：

16. **按字节分行，每一行严格解码**：采纳。
    - 一行以 LF、CR 或 CR LF 结束，与以前用的 `StreamReader.ReadLineAsync` 分行相同；整个输入开头的 UTF-8 BOM 去掉。
    - 每一行先过 `Utf8.IsValid`，不合法即答复解析错误（-32700，`PROTOCOLS_VALIDATION_001`，`id` 为 `null`），接着读下一行；合法的再判空白（RFC-0001「空白字符」，与以前的 `string.IsNullOrWhiteSpace` 相同），字节原样交给服务端。
17. **仍收 `TextReader`，读取器改用抛出的 `UTF8Encoding`**：否决。解码在读取器里按块进行，遇到非法字节时抛出的 `DecoderFallbackException` 打断的是整个读取，读取器的状态说不准，接不回下一行，答复不了 -32700 再继续。
18. **只认 LF 结束一行**：否决。以前单独的 CR 也结束一行，改了是另一处行为变化，与这次无关。
19. **`RunAsync` 改收 `Stream`，删去 `OpenInput`**：采纳。
    - 字节由传输自己分行解码，`OpenInput` 没有要做的事了；留着它就得交出另一种读取器类型，多一个公开类型。
    - `Preview` 的破坏，照 ADR-0131：5.0.0 之前没有包验证基线，不生成 `CompatibilitySuppressions.xml`；`PublicAPI.Shipped.txt` 是空的，`PublicAPI.Unshipped.txt` 直接改；迁移方式写进更新日志。
20. **一行设长度上限**：不做。以前的读取器同样不设上限，stdio 的对端是启动这个进程的本机客户端；记在「影响」的发现里。
21. **`xihan pack` / `migrate`、工作流 `Http` 活动的应答正文、Nacos 的配置内容维持宽松解码**：采纳，照所有者的决定，写进 RFC-0001 的范围说明与 `Cli`、`Workflow.Abstractions`、`Configuration.Nacos` 的 README。它们读的是应用自己的或第三方的文本，没有现成的「不是 JSON」的失败语义（ADR-0312「影响」）。

`EncodeResponse`：

22. **超过写出深度的 `JsonException` 包成 `ArgumentException`，原异常作 `InnerException`**：采纳。只抛一种；原异常留着，排查时看得到 System.Text.Json 的说明。
23. **序列化器抛出的其他异常照原样**：采纳。框架异常的错误要按 RFC-0011 规则 5 放进 `inner`；`NotSupportedException`（不支持的类型）与 `InvalidOperationException`（元数据冲突）是编程错误，不是「写不成合法的 JSON 文本」。XGI 入口照旧一并按未处理的异常结束。
24. **维持的三处边角写进 XML 文档**（`EncodeResponse` 的 `<remarks>`）：采纳，照所有者的决定，行为不变（ADR-0318「修订」第 5 条）。

其他：

25. **加向量**：否决，理由同 ADR-0312 备选 13：孤立的代理项与非法的字节写进 JSON 向量，各语言读出来的输入不同，由各实现的单元测试核对。
26. **规范的档位是 MINOR（写明）**：采纳。
    - Result、错误细节与 Schema 校验三条以前明说不在规则里；合规的实现写出的本来就是合法的 JSON，较早 MINOR 的对端读新写出的文本照常读入，新的写出一侧只是不再交出读不进的文本。
    - stdio 的一条是对 .NET 行为的修订：合规的 MCP 客户端按 UTF-8 写 stdin，不受影响；`RunAsync` 的签名是 .NET 的公开 API，不是规范的内容。
    - RFC-0011 只在有 Schema 时编码：少了一种失败，不让任何互通失败。

## 决策

1. **RFC-0001「写出与转交」**：
   - Result 与 Error 的线上编码：字符串有孤立的代理项（或以字节存放字符串的语言里不是合法的 UTF-8）即写不出，整段文本还要过共同检查（层数从结果的根算起）；写不出时写出的一方按它报编程错误的方式失败、不交出文本，.NET 抛出 `ArgumentException`。
   - 插件协议的 `Error.details`：写不出即这个错误不带细节，其余照写（`plugin/invocation.md` §3 同步）。
   - 为 Schema 校验编出的类型化请求：编不成即 `KERNEL_VALIDATION_001`，`reason` 为 `$`（RFC-0011「校验」第 1 步同步，并改为只在描述带 Schema 时编码）。
   - stdio 的 MCP 消息挪到「以字节收到、要先解码成字符串再解析的」一条（`protocol/mcp.md` 同步）。
   - 末段改为范围说明：只管列出的几处；诊断输出、类型化存储的序列化、写给外部服务的文本与读进来的第三方或应用自己的文本照写出器与读取器的缺省，是定下的范围。
2. **.NET**：
   - 内核新增 internal 的 `StrictJson`：`Serialize`（类型化的值，`EncodeResponse` 与 `ResultJson` 共用）、`HasReplacedCharacter`（`ValidationMiddleware` 也用）、`Write`（无类型的错误对象与细节，`ResultJson` 与 `PluginJson` 共用），做法照 ADR-0318「修订」第 2 条。
   - `ResultJson.Write`、`WriteMaybe`、`WriteError` 写不出即抛出 `ArgumentException`。
   - `OperationDescriptor<…>.EncodeResponse` 写不出只抛 `ArgumentException`，`JsonException` 放进 `InnerException`；XGI 入口照旧按未处理的异常结束、不带 `inner`。
   - `ValidationMiddleware` 只在描述带 Schema 时编码类型化请求，写出器换出了 U+FFFD 即 `KERNEL_VALIDATION_001`、`reason` 为 `$`。
   - `PluginJson.SerializeDetails` 写不出时返回空串，`PluginErrorMapper.ToProto` 照常写出这个错误。
   - `McpStdioTransport.RunAsync` 收 `Stream`，按字节分行、逐行严格解码；`OpenInput` 删去；`mcp` 命令（应用的与命令行工具的）把 stdin 的字节流直接交给它。
3. **维持**：诊断输出、类型化存储的序列化、写给外部服务的文本、`pack` / `migrate`、`Http` 活动的应答正文与 Nacos 的配置内容；`EncodeResponse` 的三处边角写进 XML 文档。

## 理由

- 与 ADR-0312、0315、0318 同一个理由：换成 U+FFFD 得到的仍是合法的文本，收的一方看不出交来的东西被改过。三处都是交给对方读的线上内容，读不进或读成另一个值都是互通的缺陷。
- 失败语义各照那一处已有的路径：`ResultJson` 本来对编程错误抛出 `ArgumentException`；细节在读入一侧本来就是「读不进即没有」；Validate 阶段本来对不合法的载荷报 `KERNEL_VALIDATION_001`。不新造失败的种类。
- 范围写成明确的一句，之后的轮次不必再把这几类当作遗漏重新提出。
- MCP 的 stdin 只能在字节上分行：分行之后每一行可以单独判定、单独答复，一行坏了不连累整个会话。

## 影响

- **公开 API**：
  - `Protocols.Mcp`（`Preview`）：删去 `McpStdioTransport.OpenInput(Stream)`；`RunAsync` 的两个重载的 `input` 参数由 `TextReader` 改为 `Stream`。迁移：`RunAsync(…, McpStdioTransport.OpenInput(stream), …)` 改成 `RunAsync(…, stream, …)`；以前传 `TextReader`（如测试里的 `StringReader`）的改传字节流（`new MemoryStream(Encoding.UTF8.GetBytes(text))`）；流由调用方释放，`RunAsync` 不关它。
  - `Kernel`（`Stable`，签名不变）：`OperationDescriptor.EncodeResponse` 超过写出深度时抛 `ArgumentException`（以前 `JsonException`）；`ResultJson.Write`、`WriteMaybe`、`WriteError` 在字符串有孤立的代理项、整段文本没过共同检查时抛 `ArgumentException`，超过序列化上下文的深度上限时也是 `ArgumentException`（以前 `JsonException`）。
  - 新增的 `StrictJson` 与 `OperationDescriptor.InvalidPayload` 是 internal；没有新的错误码、消息键与配置键。
- **对已有部署与代码的行为变化（升级须知）**：
  - 直接调 `EncodeResponse` 并接 `JsonException` 的代码改接 `ArgumentException`（原异常在 `InnerException`）。经 XGI 入口的调用结果不变。
  - `ResultJson` 写有孤立代理项的错误或值、写超过 64 层（从结果的根算起）或带重复成员的值时抛 `ArgumentException`；以前写成转义的 U+FFFD，或照写对方读不进的文本。
  - 宿主写给插件的错误（回调与发布的结果）细节里有孤立的代理项时，这个错误不带细节；以前插件读到 U+FFFD。
  - 带 Schema 的操作经类型化入口调用、请求里的字符串有孤立的代理项时，结果是 `KERNEL_VALIDATION_001`（HTTP 400 一类），不进 Handle；以前 Schema 校验 U+FFFD 之后照常执行。没有 Schema 的类型化调用不再编码请求，以前请求写不出（NaN）时以 `KERNEL_UNEXPECTED_001` 失败，现在照常执行。
  - `mcp` 命令的 stdin 一行里有非法的 UTF-8 时答复解析错误（-32700），这一行不执行，接着读下一行；以前换成 U+FFFD 照常执行。
- **规范**（规范工作区已提交）：RFC-0001「写出与转交」、RFC-0011「校验」第 1 步、`plugin/invocation.md` §3、`protocol/mcp.md`「生命周期与传输」与三个状态行，MINOR。
- **Go 一致性模块**：不变。
- **六个 SDK**（规则在 RFC-0001「写出与转交」与 `plugin/invocation.md` §3「写出 `Error`」；Schema 校验一条只在 .NET，SDK 的类型化调用跳过 Validate 的解码，不编码请求）：
  - 错误细节（插件写给宿主）：Go 的 `ToProtoError` 经 `jsontext.AppendString` 把编码成 WTF-8 的代理项写成 `\ud800`、其余非法字节写成 U+FFFD，要改成写不出即留空（写完之后过 `jsontext.Check`，再用 `jsoncodec.CheckStrings` 拦被换成 U+FFFD 的）；TypeScript 的 `toProtoError` 用缺省的 `writeJson`、Python 的 `to_proto_error` 用非严格的 `dumps`，都写成 `\ud800`，要改成写不出即留空（TypeScript 用 `loneSurrogates`、`nesting`、`duplicateMembers` 为 `reject` 的 `writeJson` 并保留 `nonFinite: 'string'`，Python 写完之后过 `_json.parse`，非有限的浮点仍按值模型写成字符串，不能直接用 `strict=True`）；Java 的 `Wire.details` 经 Jackson 写出再读回，结果没有核对，要补严格的核对（`JsonText.check`）；PHP 已经在 `JsonException` 时留空，补 `JsonText::check`（深度与重复成员）；Rust 的字符串总是合法的 UTF-8、细节是 `BTreeMap`，补 `json::check`（深度）即可。宿主读入时本来就把这样的细节当作没有，SDK 不跟只是两边多一层不一致的写法，互通结果不变（Go 的 U+FFFD 除外：宿主读到的是另一份细节）。
  - Result 与 Error 的线上编码：Go 的 `Result[T].MarshalJSON` / `(*Error).MarshalJSON`、TypeScript 的 `writeJson`（缺省）、Python 的 `to_json`、Java 的 `PrimitivesModule` 都照写 `\ud800` 或 U+FFFD，要改成写不出即报错（各自的编程错误方式）并过共同检查；PHP 的 `toJson` 遇非法 UTF-8 已经抛出 `JsonException`，补深度与重复成员；Rust 的 `String` 遇不到，`raw_value` 原样写进去的文本要过 `json::check`。
- **测试**：
  - `ResultJsonTests`：错误的文案、细节的值与成员名、列表元素、嵌套字典的键、内部错误里的孤立代理项，`Write`（无值、有值、Maybe）与 `WriteError` 都抛出 `ArgumentException`；值里字典键的孤立代理项同样抛出；本来就是 U+FFFD 的字符与代理对读回不变；成功值 63 层照常写出读回、64 层（结果 65 层）与带重复成员的 `JsonElement` 写不出，细节里带重复成员的 `JsonElement` 同样写不出。改之前 3 条失败。
  - `ResponseTextCheckTests`：`Fails` 只认 `ArgumentException`；新增 65 层与 101 层只抛 `ArgumentException`、101 层的 `InnerException` 是 `JsonException`。改之前 3 条失败（2 条是既有用例在 `Fails` 收紧之后）。
  - `ValidationMiddlewareTests`：带 Schema 的类型化调用请求里有孤立的代理项时是 `KERNEL_VALIDATION_001`、`reason` 为 `$`，与原始调用里同样的请求相同，代理对与 U+FFFD 照常；没有 Schema 的类型化调用照常把带孤立代理项的请求交给操作。改之前 1 条失败。
  - `PluginJsonTests`：值、成员名、列表元素与嵌套字典的键里的孤立代理项，`ToProto` 写出的错误与内部错误都不带细节、其余照写；U+FFFD 与代理对照常写出读回；带重复成员的 `JsonElement` 写不出。改之前 2 条失败。
  - `McpStdioTests`：经 `mcp` 命令，编码成 UTF-8 的代理项、单独的 `0xFF`、截断的序列与过长编码四种都答复解析错误、下一行照常、`tools/call` 没有执行（改之前失败）；LF、CR、CR LF 分行与只有空白（含 U+00A0）的行跳过；开头的 UTF-8 BOM 去掉，UTF-16 的 BOM 答复解析错误；原有用例改为交字节流。`Cli.Tests` 的 `McpCommandTests` 改为交字节流。
- **文档**：`Kernel`、`Protocols.Mcp`、`Plugins.OutOfProcess`、`Cli`、`Workflow.Abstractions`、`Configuration.Nacos` 的 README 已同步；文档站与更新日志由整合时补。
- **体积**：内核的三处完全相同的「载荷不合法」错误构造（`OperationDescriptor` 的 `InvalidResponse`、`OperationDescriptor<…>` 的 `InvalidPayload`、`ValidationMiddleware` 的同名方法）合成 `OperationDescriptor.InvalidPayload` 一处，行为不变；`EncodeResponse` 的消息常量挪进 `StrictJson`。内核 Release 程序集仍是 408,064 B，原语 103,424 B 不变。
- **发现而没改的**（报告另列）：
  - 类型化请求里浮点成员是 NaN、或 64 层最里层有值时，Validate 阶段按未处理的异常结束；原始调用里同样内容的请求是 `KERNEL_VALIDATION_001` 或读得进（备选 13、15）。
  - stdio 一行的长度不设上限（备选 20）。
  - protobuf 的字符串字段（`Error.text`、`message_key`）仍由 Google.Protobuf 宽松编码，与 ADR-0312 已列的那一条相同。

## 修订（2026-11-27）

ADR-0331 改了备选 20：stdio 的一行设可配置的字节上限 `XiHan:Protocols:Mcp:MaxStdioLineBytes`（缺省 4 MiB，与 HTTP 入口的请求体缺省上限相同；行尾与开头的 BOM 不算），超过的一行读到行尾为止丢弃、不缓冲，答复与非法的 UTF-8 同一个解析错误（-32700），接着读下一行。另外两种传输交来的每条消息都先整条过 RFC-0001「JSON 文本」的共同检查，没过同样是这个解析错误。本 ADR 其余不变。
