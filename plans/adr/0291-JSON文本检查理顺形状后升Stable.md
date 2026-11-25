# ADR-0291 JSON 文本检查理顺形状后升 Stable

## 状态

已采纳（内核的 `JsonText` 与 `JsonTextProblem` 升 `Stable`，新增 `JsonTextProblemKind`；`Check` 改名 `FindProblem`、返回 `Maybe`，问题改成具名成员的记录、种类是枚举；调用处跟着改，对外行为不变；规范只改 RFC-0001 里 .NET 映射的记法）。

## 日期

2026-11-25

## 背景

- ADR-0236 定下 RFC-0001「JSON 文本」的共同检查，.NET 的实现是内核内部的 `JsonText.Check`。ADR-0283 为了让 `Messaging.Abstractions` 的 `CloudEventsFormat` 共用同一份实现，把 `JsonText`（`Check`、`MaxDepth`）与 `JsonTextProblem` 由 internal 改为公开、`Preview`。公开时照搬了内部的形状，没有按长期契约设计：
  - `JsonTextProblem(bool IsSyntax, string Path)` 是位置记录结构体（ADR-0288「没有做的」里点到它是之后新增的 `Preview` 位置记录）。
  - `bool IsSyntax` 只分两类。RFC-0001 列了四条规则（RFC 8259 的文法、嵌套不超过 64 层、字符串与成员名是合法的 Unicode、任何一层没有重复成员），实现在不同的地方查出它们，却都折成 `true`；调用方要给出具体的原因（如消息信封为什么写不出）只能笼统地列出几种可能。
  - 结构体总有 `default` 实例：`IsSyntax` 为假、`Path` 为 `null`，即「路径为空的重复成员」，非可空的注解拦不住。
  - `Check` 通过时返回 `null`：名字不说明「没有问题」怎么表示。仓库里公开的 `Check` 有返回 `Result` 的（`QueryRevision.Check`）与返回状态列表的（健康检查），RFC-0001「查找的命名」要求方法名决定返回的形状；内核里做同一件事的内部方法都叫 `FindProblem`（`ManifestShape`、`JsonRequestShape`）。
- 这道检查是内核、消息与插件协议共用的，操作载荷、清单、Result 的线上编码、消息信封与插件的错误细节都先过它。仓库所有者定下理顺形状后升 `Stable`。
- 调用处 7 个：内核 4 处（`ManifestReader`、`ResultJson`、`OperationDescriptor<TOperation, TRequest, TResponse>`、`ValidationMiddleware`）只用路径；`CloudEventsFormat` 2 处（`Encode` 按 `IsSyntax` 选异常消息，`Decode` 只用路径）；`Plugins.Protocol` 的 `PluginJson` 1 处只看有没有问题。

## 备选

1. **保留 `bool`，只把位置记录改成具名成员**：否决。升 `Stable` 之后再细分就是改语义（原来为真的一部分换成新的种类），只能等下一个大版本；四条规则本来就分得开。
2. **种类只分两种（`Syntax`、`DuplicateMember`）**：否决，理由同上。规范的报告只分这两类（向量只有 `syntax` 与 `duplicate`），够跨语言核对，但 .NET 的诊断与日志要知道是哪一条规则；以后再细分同样是语义变化。实现本来就在不同的地方查出它们，代价只是嵌套过深要由检查自己数层。
3. **按 RFC-0001 的四条规则各一个种类（Unicode 那一条合成 `InvalidUnicode`）**：否决。那一条里实现分得开的是两种出处：原始字节不是合法的 UTF-8（含过长编码、编码成 UTF-8 的代理项、截断的多字节字符，与转义无关）与不成对的代理项转义（还原转义之后才有的问题），各自说明要改的是什么，分开报。同一条里的「原始的控制字符」与「RFC 8259 之外的转义」由读取器作为文法错误报出，与其余文法错误分不开，也没有分开的必要，归 `Syntax`。
4. **把细分的种类写进规范与向量（向量加 `kind`）**：这次不做。这次只改 .NET 的记法；各语言的检查器（Go 一致性模块的 `rules.CheckJSONText` 等）只分语法错误与重复成员，要它们跟着报种类是规范的新规则，几种语法错误之间的先后也要写死，改不改由仓库所有者另定。
5. **保留 `Check` 的名字，返回可空的引用类型 `JsonTextProblem?`**：否决。名字不说明「没有」怎么表示。照 RFC-0001「查找的命名」改成 `FindProblem` 返回 `Maybe<JsonTextProblem>`：没有问题是正常结果，是无值。
6. **返回 `Result`**：否决。检查不知道调用方的错误码与消息键：同一个问题在操作载荷里是 `KERNEL_VALIDATION_001`、在清单里是 `KERNEL_CAPABILITY_006`、在 Result 的线上编码里是 `PRIMITIVES_VALIDATION_001`、在消息信封里是 `MESSAGING_VALIDATION_002`，由调用方各自包成错误。
7. **加 `string` 的重载**（Java SDK 的 `JsonText.check(String)` 就有）：这次不做。规范的检查对的是 UTF-8 文本；.NET 字符串里孤立的代理项字符怎么处理（严格编码、编不出当语法错误，还是替换成 U+FFFD）是调用方的选择，`ResultJson.Read(string)` 是前者。以后要加，加重载不破坏已有的调用。
8. **加选项（层数、是否查重复成员）**：否决。规则由规范定死，选项只会让同一道检查对同一段文本给出不同的结果。
9. **`MaxDepth` 改成 `static readonly`**：否决。值由规范定死，常量能直接用在 `JsonDocumentOptions.MaxDepth`、`JsonSerializerOptions.MaxDepth` 与特性参数里；规范改这个值是大版本的事。
10. **`JsonTextProblem` 保持结构体，成员标 `required`**：否决。结构体的 `default`、数组元素与未赋值的字段都绕过 `required`，得到种类为 0、路径为 `null` 的实例。改成密封的记录类，只在失败时分配一个小对象，通过的路径不分配。
11. **初始化器里校验路径（不为 `null`、以 `$` 开头）**：这次不做，同 ADR-0267 备选 5：原先不校验，值都由检查自己建出；加校验是行为变化，要另定哪些值不合法。

## 决策

1. 新增枚举 `JsonTextProblemKind`（`Stable`），按 RFC-0001 的规则与实现查出问题的地方分：

   | 成员 | 值 | 是什么 | 由哪里查出 |
   | --- | --- | --- | --- |
   | `Syntax` | 0 | 不合 RFC 8259 的文法：空文本与只有空白、BOM、注释、尾逗号、单引号、`NaN` / `Infinity`、前导零、正号、多个根值、未闭合的值、字符串里原始的控制字符、RFC 8259 之外的转义、字符串之外的非法字节 | `Utf8JsonReader` 抛出 `JsonException` |
   | `NestingTooDeep` | 1 | 嵌套超过 64 层：第一个落在第 65 层的对象或数组 | 检查自己数层（读取器的上限设成 65，第 65 层由检查报出） |
   | `InvalidUtf8` | 2 | 字符串或成员名的原始字节不是合法的 UTF-8 | `Utf8.IsValid` |
   | `LoneSurrogate` | 3 | 字符串或成员名里有不成对的代理项转义（U+D800–U+DBFF 的转义后没有紧跟 U+DC00–U+DFFF 的转义，或单独的低代理项转义） | 原始字节合法、还原转义时 `GetString` 抛出 `InvalidOperationException` |
   | `DuplicateMember` | 4 | 某一层有重复的成员名（按还原转义后的名字逐个 UTF-16 码元比较） | 每个对象一个按序数比较的名字集合 |

   前四种是 RFC-0001 所说的语法错误（向量里的 `syntax`）。以后规范加检查时往后加成员，已有成员的名字与值不变。
2. `JsonTextProblem` 改成密封的记录类（`Stable`）：`Kind` 与 `Path` 都是 `required` 的 `init` 成员，没有位置构造与 `Deconstruct`。`Path` 在 `DuplicateMember` 时是第一处重复（第二次出现的成员）的路径，写法同 RFC-0011 规则 2；其余种类一律是 `$`。
3. `JsonText` 升 `Stable`。`Check(ReadOnlySpan<byte> utf8) -> JsonTextProblem?` 改成 `FindProblem(ReadOnlySpan<byte> utf8) -> Maybe<JsonTextProblem>`：通过时无值，否则是第一个问题。参数照旧只收 UTF-8 字节，没有选项；`MaxDepth = 64` 照旧是常量。
4. 报哪一个问题：
   - 文本里第一处语法错误立即返回，不论它是哪一种：几种语法错误之间按在文本里出现的先后。
   - 语法错误不论出现在哪里都优先于重复成员：重复成员只记第一处、读到末尾没有语法错误才报出。
   - 与原先相比，每段文本是否通过、报出的路径都不变；原先报 `IsSyntax = true` 的现在是前四种之一，原先报 `IsSyntax = false` 的现在是 `DuplicateMember`。
5. 调用处机械改写，对外行为不变：
   - 内核 4 处与 `CloudEventsFormat.Decode`：`JsonText.FindProblem(…).TryGetValue(out var problem)`，只用 `Path`。错误码、消息键与参数 `reason`（`$` 或重复成员的路径）都不变，`primitives/json-text.json`、`primitives/wire-encoding.json`、`operations/payload-reasons.json`、`manifest/manifests.json` 与 `messaging/cloudevents-envelopes.json` 的回放照旧通过。
   - `CloudEventsFormat.Encode`：`Kind` 是 `DuplicateMember` 时照旧是「成员会出现两次」的异常消息，其余种类照旧是「嵌套过深或不是合法的 Unicode」的异常消息，错误码 `MESSAGING_VALIDATION_002` 不变。
   - `PluginJson.ParseDetails`：`HasValue` 时照旧当作没有细节。

## 理由

- 它是几个包共用的检查，读 XiHan JSON 文本的应用与 Provider 也会直接用它；升 `Stable` 之后形状要守到下一个大版本，所以在 5.0.0 之前一次理顺：名字说明返回的形状，问题带着规范里的分类，数据类型照 RFC-0001「数据类型的形状」。
- 种类细到实现确实分得开的出处为止：每一种都对应规范的一条规则或一条规则里的一个子句，也都由一处代码查出，不靠解析异常消息；分不开的（读取器报的各种文法错误）不硬分。
- 细分不改报告的规则：语法错误仍然优先于重复成员、位置仍然只写 `$`，规范与各语言的实现不受影响。
- 在 5.0.0 之前改，`Preview` 的破坏不需要豁免登记（尚无基线，ADR-0131）。

## 影响

- **公开 API 与迁移**（`PublicAPI.Unshipped.txt` 已同步；尚无基线，不需要 `CompatibilitySuppressions.xml`）：

  | 原先 | 改成 |
  | --- | --- |
  | `if (JsonText.Check(utf8) is { } problem)` | `if (JsonText.FindProblem(utf8).TryGetValue(out var problem))` |
  | `JsonText.Check(utf8) is null` / `is not null` | `!JsonText.FindProblem(utf8).HasValue` / `JsonText.FindProblem(utf8).HasValue` |
  | `problem.IsSyntax` | `problem.Kind != JsonTextProblemKind.DuplicateMember` |
  | `!problem.IsSyntax` | `problem.Kind == JsonTextProblemKind.DuplicateMember` |
  | `new JsonTextProblem(isSyntax, path)` | `new JsonTextProblem { Kind = kind, Path = path }` |
  | `var (isSyntax, path) = problem` | 读 `problem.Kind`、`problem.Path` |

  - `JsonTextProblem` 由结构体变成引用类型：`JsonTextProblem?` 是 `Nullable<JsonTextProblem>` 的写法（`.Value`、`.HasValue`）改用 `Maybe<JsonTextProblem>` 的 `TryGetValue`、`HasValue`、`ValueOrThrow()`。
  - 按种类分支时给其余的种类留缺省分支。
  - 二进制不兼容：按旧签名编译的程序集要重新编译。
- **行为不变**：对每一段文本，检查是否通过与报出的路径都与原先相同（向量既直接经 `FindProblem` 回放，也经操作的请求解码回放）；新多出的只是种类。字符串现在都先核对一次原始字节再按需还原转义（原先带转义的只还原），通过的路径多一遍线性的 UTF-8 核对。
- **序列化**：框架不序列化 `JsonTextProblem`，没有哪个 `JsonSerializerContext` 带着它。
- **规范**：RFC-0001「JSON 文本」的 .NET 映射一句由 `JsonText.Check` 改成 `JsonText.FindProblem`，写明返回 `Maybe`、问题是 `JsonTextProblem { Kind, Path }` 与五个种类，并注明种类是 .NET 的诊断信息、规则与报告的位置照旧；状态行与「对应 ADR」补上。规则与向量不变。
- **其他语言的 SDK**：不用跟。各自的检查器照旧只分语法错误与重复成员（Go 的 `JSONTextProblem.Syntax`）；要报种类是规范的新规则（备选 4）。
- **没有做的**：细分的种类进规范与向量（备选 4）；`string` 的重载（备选 7）；路径的校验（备选 11）。
- **测试**：
  - `JsonTextVectorTests` 新增直接经 `FindProblem` 回放 `json-text.json`（含经请求解码时跳过的空文本），前四种都算向量的 `syntax`、路径是 `$`。
  - `JsonTextTests`：每一种的典型输入（含成员名里的问题、带转义的字符串里的非法字节报 `InvalidUtf8`、字符串之外的非法字节报 `Syntax`、第 64 层通过而第 65 层报 `NestingTooDeep`）；重复成员的路径（转义还原后同名、数组项里、要写成方括号的名字、只报第一处）；语法错误优先于在它之前的重复成员；几种语法错误之间按出现的先后；`JsonTextProblem` 只有无参构造、没有 `Deconstruct`、两个成员都是 `required`、是引用类型；`JsonTextProblemKind` 五个成员的名字与值。
- **文档**：内核 README 已更新。文档站 `packages/kernel` 的命名空间表与类型表、`packages/messaging-abstractions` 里 `JsonText.Check` 的一句、`appendix/spec` 的 RFC-0001 修订记录与更新日志待改。
