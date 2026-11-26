# ADR-0318 写出与转交补齐 binary 头值、类型化应答与空应答载荷

## 状态

已采纳（规范 `protocol/cloudevents.md`「出站」、RFC-0009「信封」的写出、RFC-0001「写出与转交」（新增一条、改第三条与末句）、RFC-0011 规则 2 的「载荷的文本」与规则 10 的「编码」、`plugin/invocation.md` §1、RFC-0006「幂等」第 2 与第 4 条、`protocol/commandline.md` 的字段选项，以及 RFC-0001、RFC-0006、RFC-0009、RFC-0011、RFC-0017 的状态行已改；`Protocols.CloudEvents` 的 `CloudEventsHttpBinding` 在 binary 模式写头之前核对，`EncodeHeaderValue` 拒收孤立的代理项；内核新增 internal 的 `StrictJsonEncoder`，`OperationDescriptor<TOperation, TRequest, TResponse>.EncodeResponse` 经它写出；`Hosting.CommandLine` 新增消息键 `hosting.commandline.invoke_field_invalid`；`Caching.Abstractions` 的幂等记录记下没有响应体；ADR-0315「背景」第 1 条关于 TypeScript 的括注更正；Go、TypeScript、Python SDK 的同类改动由 SDK 一侧跟进；向量、错误码与配置键不变）。

## 日期

2026-11-26

## 背景

ADR-0315 之后的待办里留下几件，仓库所有者已拍板，都要做：

1. **CloudEvents HTTP 绑定的 binary 模式**：`CloudEventsHttpBinding.Write` 的 binary 模式不经 `CloudEventsFormat.Encode`。`EncodeHeaderValue` 用 `EnumerateRunes` 把孤立的代理项读成 U+FFFD，再编成 `%EF%BF%BD`。Webhook 的缺省模式就是 binary，单条发布照样写成 U+FFFD；structured 与 batch 却在 `Encode` 那里拒绝。
2. **类型化应答里的孤立代理项**：处理器返回类型化的对象、字符串里有孤立的代理项时，各家结果不一：
   - Go（`encoding/json`）与 .NET（System.Text.Json）写成 U+FFFD，调用成功，调用方拿到的是另一个值。
   - TypeScript（`writeJson`）与 Python（`_json.quote`）写成 `\ud800` 这样的转义，插件照常交回，再被宿主按 `plugin/invocation.md` §1 以 `PLUGINS_UNEXPECTED_001` 拒绝。
   - Java 在插件一侧就以 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception` 失败（Jackson 写出之后过「JSON 文本」的检查）。

   RFC-0001「写出与转交」的末句把类型化的响应对象明说在规则之外。所有者定为统一成 SDK 在本地失败。
3. **命令行字段选项的文案**：字段选项有孤立的代理项时沿用 `hosting.commandline.invoke_json_invalid`，模板是 "The value of --json must be a JSON object: {reason}"，用在字段选项上读来不对。
4. **空的成功载荷**：ADR-0315 备选 12 把「空的载荷算不算插件不合规」留作待定，所有者定为继续放行。同时有一个既有缺陷：开了幂等的插件操作交回空载荷时，`IdempotencyMiddleware.Complete` 用 `JsonDocument.Parse` 解析编出的载荷，空载荷让它抛出。插件的操作已经执行，首次调用却以 `KERNEL_UNEXPECTED_001` 结束，「处理中」的标记留到过期，这段时间里的重试得 `CACHING_CONFLICT_002`。
5. **一处注记写错了**：`plugin/invocation.md` §1 与 ADR-0315 说「以前 TypeScript 以 `TextEncoder` 编码处理器以字符串交回的应答」，孤立的代理项换成 U+FFFD 照发。实际上 TypeScript 的处理器没有以字符串交回应答的接口，应答总经调用器的 `writeJson` 写出，孤立的代理项写成转义；`TextEncoder` 编码的是已经转义过的文本，换不出 U+FFFD。

## 备选

binary 模式：

1. **写任何头之前核对，属性、先后与消息都与 `Encode` 相同**：采纳。
   - 核对 `id`、`source`、`type`、`subject`、`dataschema`、`datacontenttype`（binary 写进 `Content-Type`）与扩展属性的值；扩展属性名合文法，是 ASCII。
   - 有孤立的代理项即抛出携带 `MESSAGING_VALIDATION_002` 的 `FrameworkException`，消息与 `Encode` 的同一句、带属性名。同一个信封换一种内容模式，调用方拿到的异常相同。
   - 写之前核对：写到一半才发现，请求上就留下半份头。
2. **只让 `EncodeHeaderValue` 抛出**：否决。它不知道属性名，抛的是 `ArgumentException`，错误码对不上 structured；写到第几个头才抛也说不准。
3. **`EncodeHeaderValue` 也拒收孤立的代理项**：采纳，抛出 `ArgumentException`。它是公开的辅助，契约是「按 UTF-8 字节百分号编码」，孤立的代理项没有 UTF-8 字节；本来就是 U+FFFD 的字符照常编成 `%EF%BF%BD`。`Write` 先核对，到不了这里。
4. **复用 `CloudEventsFormat` 的私有核对**（改成 internal 再开 `InternalsVisibleTo`，或者公开）：否决，照 ADR-0315 备选 20：几行代码不加公开面，也不在抽象包上为一个协议包开内部可见。
5. **binary 的数据体也照 `Encode` 核对**（声明为 JSON 的数据有重复成员、超过 64 层）：这次不做，记为待定。任务只到孤立的代理项；binary 的数据原样作为请求体，不嵌进一份 JSON 文档。

类型化应答：

6. **规则写成「写不成合法的 JSON 文本即写不出」**：采纳。
   - 以 UTF-16 存放字符串的语言（.NET、Java、JavaScript）与 Python 的 `str`：字符串（成员名与值，含字典的键）有孤立的代理项。
   - 以字节存放字符串的语言（Go、PHP）：字符串不是合法的 UTF-8。Go 的 `string` 放不下孤立的代理项（`string(rune(0xD800))` 已经是 U+FFFD），坏的只会是字节，例如编码成 UTF-8 的代理项 `ED A0 80`；只写「孤立的代理项」，Go 就没有要改的。
   - Rust 的 `String` 总是合法的 UTF-8，遇不到。
7. **结果是 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception`，没有参数，不带 `inner`**：采纳。
   - RFC-0011 规则 10 的「编码」早已写明，响应写不出（浮点成员是 NaN）就是这个结果；孤立的代理项是同一件事。
   - 不带 `inner`：各家查出它的办法不同，有的经语言的异常，有的经写出之后的检查，写明不带，结果才一样。
8. **照 TypeScript 与 Python 的现状，写成转义交给宿主、由宿主拒绝**：否决。宿主报的是 `PLUGINS_UNEXPECTED_001`，把处理器写不出的应答归成插件写坏了协议内容；同一个处理器在 .NET 进程内跑又是另一个结果。
9. **.NET 的 XGI 入口也属于这一条**：采纳。
   - RFC-0011 规则 10 的「编码」是宿主与 SDK 共用的规则，.NET 的 `OperationInvoker` 就是它的参考实现（「以前 .NET 把序列化的异常抛给调用方」）；SDK 失败而 .NET 写成 U+FFFD，同一个操作在进程内与在插件里结果不同。
   - .NET 的协议适配器（HTTP、gRPC、MCP、命令行、AI 工具）与插件回调 `Host.Invoke` 都经 XGI 入口编码响应，改 `EncodeResponse` 一处，各处一致；宿主操作经回调交给插件的应答也不再是另一份文本。
10. **.NET 怎么查**：
    - a. **自定义的 `JavaScriptEncoder` 包住序列化上下文的编码器**：采纳。System.Text.Json 写每个以 UTF-16 交来的字符串（值、成员名、字典的键、`JsonNode` 的值与键）之前先问编码器 `FindFirstCharacterToEncode`，在这里核对，哪个字符串都绕不过去；转义原样交给被包住的编码器，实测写出的字节与 `SerializeToUtf8Bytes` 的相同（缺省选项与缩进都比过）。以 UTF-8 交来的（`FindFirstCharacterToEncodeUtf8`）同样核对是否合法。代价：
      - 覆写 `TextEncoder` 带指针参数的抽象成员只能在 unsafe 上下文里，内核开了 `AllowUnsafeBlocks`。源码只把指针包成 span 或原样转交，不做指针运算；这是仓库里第一处在源码里写 `unsafe` 的地方（以前开 `AllowUnsafeBlocks` 的几个包都只为 `LibraryImport` 的生成代码）。
      - `EncodeResponse` 改为自建写出器（`ArrayBufferWriter` + `Utf8JsonWriter`，缩进、换行、深度照序列化选项），不再用 `SerializeToUtf8Bytes` 的池化缓冲，返回的内存可能比载荷大。
    - b. **写出之后找 U+FFFD**：否决。本来就是 U+FFFD 的字符缺省也写成 `\uFFFD`，分不出来。
    - c. **出现 U+FFFD 时换宽松的编码器再写一遍分辨**（宽松编码器把真正的 U+FFFD 原样写出，替换出来的写成转义，实测如此）：否决。靠的是「替换出来的总被转义」这一实现细节，可疑时还要写两遍。
    - d. **按 `JsonTypeInfo` 的元数据遍历响应对象**：否决。集合、字典、自定义转换器与 `JsonNode` 都要分别处理，改不全。
11. **抛出的是 `ArgumentException`**：采纳。与 System.Text.Json 写不出 NaN 时相同；XGI 入口照旧接住全部异常（`OperationCanceledException` 除外），按未处理的异常结束。
12. **范围**：只管操作的成功响应编成载荷的那一处。类型化入口（`InvokeAsync<TRequest, TResponse>`）不经 JSON，交出处理器返回的对象，不受影响；类型化的请求（Validate 阶段为 Schema 校验编出的那一份）、消息与领域事件的数据、持久化、缓存与审计记录、Result 与 Error 的线上编码、Problem Details、MCP 的应答与日志仍不在规则里，RFC-0001 的末句改写成逐项列出。幂等记录里的响应就是这一处写出的载荷，跟着严格。
13. **加向量**：否决，理由同 ADR-0312 备选 13：孤立的代理项写进 JSON 向量，各语言读出来的输入不同，由各实现的单元测试核对。

命令行字段选项：

14. **另登记消息键 `hosting.commandline.invoke_field_invalid`**：采纳。参数 `reason`，en-US 模板 "The field options cannot be written as a JSON payload: {reason}"，10 个区域都补；错误码 `HOSTING_VALIDATION_001` 与退出码 64 不变。
15. **把 `invoke_json_invalid` 的 10 个模板改成中性的写法**：否决。那一句说的是「`--json` 的值必须是 JSON 对象」，对 `--json` 的其余原因（不是对象、语法错误、编码不成）都准确，改中性了反而说不清。

空的应答载荷：

16. **空的成功载荷表示没有响应体，不按 JSON 文本核对**：采纳，照所有者的决定。RFC-0001 说空文本不是 JSON，说的是要读成 JSON 值的文本；空载荷不是要读的 JSON。只有空白的载荷不为空，照样核对，没过即 `PLUGINS_UNEXPECTED_001`。规范写在 `plugin/invocation.md` §1 与 RFC-0001「写出与转交」的第三条。
17. **幂等记录记下「没有响应体」**：采纳。记录多一个成员 `emptyResponse`，只在为真时写，有响应体的记录与以前写下的逐字节相同。
18. **把空载荷记成某个 JSON 值**（`""`、`null`）：否决。它们都是合法的响应值，回放时分不出来。
19. **把记录里的响应改成字节（如 Base64）**：否决。动的是每一条记录的格式；共享的分布式存储里，别的版本的宿主读不回新写的记录。
20. **回放时按描述解码空载荷**：采纳。插件代理原样包回同样的空载荷，与首次调用交回的相同；类型化的描述解码失败，与记录里没有响应一样返回 `CACHING_CONFLICT_003`（`caching.idempotency.response_unavailable`），不返回空载荷。
21. **旧版本的宿主读到 `emptyResponse` 记录**：不另做处理。它忽略不认识的成员，看到的是没有响应，返回 `CACHING_CONFLICT_003`，不会回放出错的响应。

更正：

22. **ADR-0315 正文只改那一处括注，文末加「更正」**：采纳。决策与影响不变；规范 `plugin/invocation.md` §1 的同一句注记同日更正，写成事实。

## 决策

1. **binary 模式**（`protocol/cloudevents.md`「出站」，RFC-0009「信封」的写出）：写任何头之前核对与结构化写出相同的属性，有孤立的代理项即抛出携带 `MESSAGING_VALIDATION_002` 的异常、消息带属性名，请求不发出；`EncodeHeaderValue` 遇孤立的代理项抛出 `ArgumentException`。
2. **类型化应答**（RFC-0011 规则 2 与规则 10 的「编码」，RFC-0001「写出与转交」新增一条，`plugin/invocation.md` §1）：XGI 入口把响应写成载荷时，字符串有孤立的代理项（或以字节存放字符串的语言里不是合法的 UTF-8）即写不出，`KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception`，没有参数、不带 `inner`、没有载荷；宿主与 SDK 相同，插件一侧照这一条报给宿主。RFC-0001 的末句改为逐项列出仍不在规则里的写出。
3. **.NET**：`OperationDescriptor<…>.EncodeResponse` 经 `StrictJsonEncoder` 写出，写不出时抛出 `ArgumentException`，由 `OperationInvoker` 按未处理的异常结束。
4. **命令行**：字段选项的用法错误用 `hosting.commandline.invoke_field_invalid`（`protocol/commandline.md`）。
5. **空的应答载荷**：表示没有响应体，不核对（`plugin/invocation.md` §1、RFC-0001）；幂等记下没有响应体、回放时按描述解码空载荷（RFC-0006「幂等」第 2、4 条），`IdempotencyMiddleware` 不再把空载荷当 JSON 解析。
6. **更正**：`plugin/invocation.md` §1 与 ADR-0315 关于 TypeScript 的注记改成事实。

## 理由

- 与 ADR-0301、ADR-0312、ADR-0315 同一个理由：换成 U+FFFD 得到的仍是合法的文本，收的一方看不出交来的东西被改过。binary 是 Webhook 的缺省模式，只拦 structured 等于大部分发布没拦住。
- 处理器写不出的应答是处理器自己的失败，与 NaN 同一类：在写的那一方本地失败，错误照 RFC-0011 规则 10，宿主与各 SDK 一样，不让宿主替它背「插件不合规」。
- 空载荷在 .NET 一直表示没有响应体，各协议适配器与一致性套件也这样用；写成规则之后，幂等这一处的抛出就是缺陷，修成照常记录与回放。

## 影响

- **公开 API**：新增常量 `CommandLineMessages.InvokeFieldInvalid`（`Preview`）。`CloudEventsHttpBinding.Write` 与 `EncodeHeaderValue`（`Preview`）签名不变，多一种抛出的情形。`OperationDescriptor<…>.EncodeResponse`（`Stable`）签名不变，响应里的字符串有孤立的代理项时抛出 `ArgumentException`，以前写成转义的 U+FFFD。`StrictJsonEncoder` 与幂等记录是 internal。没有新的错误码与配置键；新消息键 `hosting.commandline.invoke_field_invalid`。
- **对已有部署与代码的行为变化（升级须知）**：
  - CloudEvents Webhook 以 binary（缺省）发布的信封，字符串属性有孤立的代理项时 `PublishAsync` 抛出 `MESSAGING_VALIDATION_002`、请求不发出；以前编成 `%EF%BF%BD` 照发。`EncodeHeaderValue` 遇孤立的代理项抛出 `ArgumentException`。
  - .NET 操作的响应里有孤立的代理项时，经 XGI 入口的调用（HTTP、gRPC、MCP、命令行 `invoke`、AI 工具、插件回调 `Host.Invoke`）以 `KERNEL_UNEXPECTED_001` 失败、没有载荷，HTTP 是 500；以前写成 U+FFFD 照常成功。类型化入口不变；开了幂等的调用在记录响应时同样写不出（与 NaN 相同）。
  - 命令行字段选项有孤立的代理项时，stderr 的 `detail` 是新的一句（en-US "The field options cannot be written as a JSON payload: …"），错误码与退出码不变。
  - 开了幂等的插件操作交回空载荷时照常成功并回放空载荷；以前首次调用以 `KERNEL_UNEXPECTED_001` 失败。
- **规范**（规范工作区已提交）：
  - binary 模式、类型化应答写不出、空的应答载荷与幂等记下没有响应体都是写明（MINOR）：以前不作规定，或者（类型化应答）明说不在规则里。合规的插件与宿主写出的本来就是合法的 JSON，不让较早 MINOR 的两个对端互通失败。
  - 字段选项的消息键是 .NET 映射的修订（PATCH）：错误码与退出码不变，规范里只换了键名。
  - 关于 TypeScript 的注记是更正（PATCH）。
- **Go 一致性模块**：不变。
- **六个 SDK**（规则在 RFC-0011 规则 10 的「编码」与 `plugin/invocation.md` §1）：
  - Go：`encodeResponse` 的 `encoding/json` 把不是合法 UTF-8 的字符串（含 map 的键）写成 U+FFFD，要改成写不出；`json.Marshaler` 交出的原文 `encoding/json` 只核对语法，字符串里的字节是否合法要在 SDK 一侧实测，不查就一并核对。
  - TypeScript：`encodeResponse` 用的 `writeJson` 把孤立的代理项写成 `\ud800`，要改成写不出（`TypeError` 被接住、不带 `inner`）；`quoteJsonString` 给 RFC-0010 模板等别处用的写法不变。
  - Python：`_encode_response` 的 `dumps(…, strict=True)` 经 `_json.quote` 写成转义，要改成写不出。
  - Java：Jackson 写出之后过「JSON 文本」的检查，已经是 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception`、不带 `inner`，行为不用改，补测试。
  - PHP：`json_encode` 带 `JSON_THROW_ON_ERROR`，非法的 UTF-8 已经抛出、按 `unhandledException` 结束，核对异常不放进 `inner`、补测试。
  - Rust：`String` 总是合法的 UTF-8，不用改。
- **发现而没改的**（报告第 8 条另列）：
  - `IdempotencyMiddleware` 记录响应时 `EncodeResponse` 抛出（NaN、孤立的代理项），异常在清除标记的 `try` 之外：操作已经执行，「处理中」的标记留到过期，这段时间里的重试得 `CACHING_CONFLICT_002`。以前 NaN 就是这样，这次多了孤立的代理项一种情形。
  - binary 模式的数据体不过 JSON 文本的检查，structured 会拒绝的数据（重复成员）binary 照发（备选 5）。
  - binary 入站的 `DecodeHeaderValue` 用 `Uri.UnescapeDataString`，非法的 UTF-8 百分号序列（`%ED%A0%80`、截断的 `%C3`）原样留成百分号文本，不报不合法（实测）。
- **测试**：
  - `CloudEventsHttpBindingTests`：binary 模式七个属性有孤立的代理项都抛出 `MESSAGING_VALIDATION_002`、消息与 `Encode` 的相同、一个头也没写；代理对与本来就是 U+FFFD 的字符照常编码；`EncodeHeaderValue` 拒收孤立的高、低代理项。`CloudEventsProtocolTests`：binary 与 structured 两种模式的 Webhook 发布都抛出、没有发出请求。改之前 3 条失败（structured 那一条本来就过）。
  - `ResponseEncodingTests`（新增，内核）：值里的高、低代理项与反过来的一对、字典的键、数组元素五种，XGI 入口都是 `KERNEL_UNEXPECTED_001`、不带 `inner`、没有参数与载荷；`EncodeResponse` 对五种都抛出 `ArgumentException`；合法的文本（代理对、U+FFFD、HTML 敏感字符、引号、反斜杠、控制字符）写出的字节与 `SerializeToUtf8Bytes` 的相同，缺省与缩进的选项都比过；类型化入口照常交出带孤立代理项的对象。改之前 10 条失败。
  - `CommandLineRunnerTests`：字段选项的三种情形与重复选项都是新的文案。
  - `IdempotentOperationReplayTests`：照插件代理写的操作交回空载荷，开了幂等的两次调用都成功、都是空载荷，操作只执行一次，记录里 `emptyResponse` 为真；有响应体的记录不写这个成员；记下没有响应体的记录交给类型化的操作是 `CACHING_CONFLICT_003`。改之前第一条失败（`KERNEL_UNEXPECTED_001`）。
  - `PluginResponsePayloadTests`：只有空白的载荷不为空，调用与回放都没过检查。
- **文档**：`Protocols.CloudEvents`、`Kernel`、`Hosting.CommandLine`、`Caching.Abstractions`、`Plugins.OutOfProcess`、`Plugins.Wasm` 的 README 已同步；文档站的 `protocols/cloudevents`、`protocols/command-line`、`plugins/out-of-process`、`plugins/wasm`、缓存的幂等一页、各 SDK 页、`appendix/spec` 与更新日志待改。
- **待定**：binary 的数据体的检查（备选 5）；幂等记录响应时写不出的处置；binary 入站对非法百分号序列的读法。
