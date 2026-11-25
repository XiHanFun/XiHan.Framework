# ADR-0306 SDK 里信封的 time 改用时间类型与 Python 的 JSON 日志遮绑定属性

## 状态

已采纳（规范 `plugin/invocation.md` §4 的 `Envelope.time` 与「插件一侧处理 `HandleEvent`」第 2 步、§7、RFC-0017 状态行与 `plugin/envelope-times.json` 的 `$comment` 已改，不加向量；六个 SDK 各自的 `dev` 上落地（本地提交，未推送）：Go `*time.Time`、TypeScript `Date`、Rust `SystemTime`、Java `Instant`、Python 带时区的 `datetime`、PHP `DateTimeInterface` / `DateTimeImmutable`，Python 的 `JsonLogHandler` 遇到 `RedactedDict` 只写属性名；框架代码不改）。

## 日期

2026-11-25

## 背景

- ADR-0293 把插件协议的 `Envelope.time` 收紧到 RFC-0003 规则 8 的文法，宿主读插件写来的不合文法即那一项 `MESSAGING_VALIDATION_002` + `messaging.envelope.invalid_time`。SDK 一侧它没有动：六个 SDK 交给作者的 `time` 都是字符串，发布时原样写出、不在本地核对（ADR-0293 备选 5、决策 4）；投递时宿主写来的文本原样交给作者，规范写着「SDK 原样交给作者，不另解析、不改写」。另留了一句：SDK 另提供以时刻类型写入 `time` 的接口时，写出的文本必须合这条文法，宜与宿主的写法相同。
- 字符串的代价落在作者身上：
  - 发布：各语言最顺手的写法有不少不合这条文法（ADR-0293「影响」列过：Python 不带时区的 `isoformat()`、Go 的 `time.Time.String()`、Java `OffsetDateTime.toString()` 在整分钟时、PHP 的 `DATE_ISO8601`），作者要到宿主逐项的结果里才知道写错了，那一项已经不发布。
  - 投递：作者拿到的是 .NET `O` 格式的文本（七位小数、`+00:00`），要自己解析；各语言标准库的解析对七位小数、`+00:00` 与小写的 `t` / `z` 宽严不一，同一个插件换一种语言写，读出的时刻可能不同，也可能读不出。
- 仓库所有者已拍板：六个 SDK 的信封 `time` 改用各语言的时间类型，由 SDK 写成 RFC 3339、读成时间类型，共用一套写出与读入的规则（Go SDK 先定，其余五个对齐）。
- 另一件是 ADR-0299「待定」留下的 Python `JsonLogHandler`：`Binding.properties` 是 `RedactedDict`（禁写的 `dict` 子类，ADR-0299 决策 2），`json.dumps` 把 `dict` 子类当作对象直接写出、不经 `default`，作者把 `binding.properties` 本身放进日志的 `extra` 时，属性值（可能是口令、连接串）就写进了 stderr 的 JSON 日志行。所有者已拍板：处理器遇到 `RedactedDict` 时遮值。

## 备选

1. **维持字符串**（现状）：否决。文法的负担留在作者身上，写错了要到发布结果里才知道；投递时作者还得各自解析宿主的文本，六种语言读出的时刻未必一样。
2. **仍收字符串，SDK 在发出前按文法核对**：否决。本地先判会改变与宿主逐项核对的先后（宿主没有消息发布器时本该每一项都是 `plugins.callback.messaging_unavailable`，ADR-0293 备选 5）；它只是把错误提早报出，作者仍然要自己写对文本，投递一侧也不解决。
3. **改收时间类型，SDK 负责写出与读入**（采纳）：作者给的是一个时刻，SDK 写出的文本在范围之内总合文法；投递时作者拿到的也是时刻，读法与宿主同一条。代价是六个 SDK 的签名都有破坏性的变化（都还在 0.x，按各仓库的迁移说明处理），精度受各语言时间类型的限制（见「影响」）。
4. **把 `plugin.proto` 的字段改成 `google.protobuf.Timestamp`**：否决，理由同 ADR-0293 备选 3：线上不兼容，要新增协议版本，Wasm 载体的 proto3 JSON 里它本来也写成 RFC 3339 文本。这次只改 SDK 的公开面，线上仍是字符串，协议版本不变。
5. **SDK 写出的文本与宿主的相同**（UTC、七位小数、`+00:00`）：否决。七位小数是 .NET 的 100 纳秒刻度：Go 与 Java 是纳秒，要截掉两位；Python 与 PHP 是微秒、TypeScript 是毫秒，要补零写出并不存在的精度。以 `Z` 结尾、去掉小数末尾 0 的写法与类型的精度无关，Go 的 `time.RFC3339Nano` 就是这个样子。两种写法都合文法，宿主照样读得出；宿主一侧的写法不变（ADR-0293 备选 7）。
6. **SDK 在本地核对年份范围**（换算成 UTC 之后不在 0001–9999 年就在本地报错）：否决，理由同备选 2。越界的时刻照写，由宿主以 `messaging.envelope.invalid_time` 拒绝那一项。Java 与 Python 起初在本地核对，已由 `a22801c`、`5d98d88` 改掉。
7. **越界的时刻夹到范围之内，或写成空串**：否决。前者改了事件的时刻，后者让事件悄悄变成没有时间（同 ADR-0283 备选 2），都是静默兜底。
8. **宿主写来的文本读不出时当作没有时间，或原样交给作者**：否决。前者让处理器拿到没有发生时间的事件（同 ADR-0283 备选 2），后者在时间类型的字段里放不下。照「插件一侧处理 `HandleEvent`」第 2 步「宿主写来的内容不合法」以 gRPC `InvalidArgument` 拒绝：它与没有 `envelope`、`mode` 不合法同属请求本身不合法，排在找订阅之前，结果不取决于插件订阅了什么。按规范写出的宿主一律写合文法的文本，这一步实际只防不合规范的宿主，以及时间类型表示不了的时刻（Windows 上 Rust 的 `SystemTime`）。
9. **Go 用零值的 `time.Time` 表示没有时间**：否决。零值就是 `0001-01-01T00:00:00Z`，恰好是文法允许的最早时刻（`envelope-times.json` 的 `valid` 收了它）：用它表示没有，这个时刻就发不出去，投递时也分不出宿主写来的是它还是空串。采纳 `*time.Time`，nil 是没有。代价是写法成了 `Time: new(time.Now())` 或 `&t`，`%v` 打印信封时 `Time` 是指针地址、看不到值。
10. **Rust 引入时间库**（`time` 或 `chrono`）：这次不做，只用标准库的 `std::time::SystemTime`，不新增依赖。代价是两处平台差异（见「影响」），留在「没有解决的」。
11. **Python 的 `JsonLogHandler` 照写 `RedactedDict` 的值，只靠文档提醒**：否决，理由同 ADR-0299 备选 2：作者很难意识到把 `binding.properties` 交给日志会带出凭据。
12. **让 `RedactedDict` 不再是 `dict` 子类，好让 `json.dumps` 经 `default=str` 走它不带值的 `repr`**：否决。`dataclasses.asdict` 遇到不是 `dict` 的 Mapping 时整个 `deepcopy`，得到的还是只读类型、写不成 JSON（ADR-0299 备选 6）；遮值只能在处理器写出之前做。

## 决策

1. **规范**（`plugin/invocation.md` §4 的 `Envelope.time`：宿主一侧的两条不变，SDK 一侧另写两条）：
   - **SDK 读宿主写来的**：按 RFC-0003 规则 8 的文法读成该语言的时间类型交给作者，空串是没有时间；比类型精度更细的小数截去、不进位，得到的时刻不晚于原文（纪元之前也一样）。读不出（不合文法、越界，或者合文法却超出该语言时间类型的范围）时以 gRPC `InvalidArgument` 拒绝这次投递、不交给处理器，归在「插件一侧处理 `HandleEvent`」第 2 步「宿主写来的内容不合法」，排在找订阅之前。
   - **SDK 写给宿主的**：作者给该语言的时间类型，或者不给；SDK 换算成 UTC，写成 `YYYY-MM-DDTHH:MM:SS`、可选的秒的小数与 `Z`（`T`、`Z` 大写），小数去掉末尾的 0、全为 0 时不写小数点，最多写到类型的精度；不给写空串。不在本地核对年份范围：越界的照写（不改成范围之内的时刻，也不写成空串），由宿主以 `messaging.envelope.invalid_time` 拒绝那一项；越界时写成什么文本不作规定，时间类型里不是有效时刻的值（JavaScript 的 `Invalid Date`）同样照写。
   - §7 写明 SDK 读写 `time` 由 SDK 实现的测试覆盖；向量那一条写明 SDK 写出的文本没有收向量；`envelope-times.json` 的 `$comment` 里「SDK 原样交给作者」随之改写，期望值不动。RFC-0017 状态行追加同日第四次修订。
   - 这是规范的 MAJOR（`v1.0.0` 之前允许）。「SDK 原样交给作者，不另解析、不改写」与「SDK 不在本地核对，原样写出作者给的文本」都是已经写明的行为，改它们是 RFC-0024「版本」第 3 条的「改已经写明的行为」；SDK 作为读取一侧，现在也拒绝上一个规范标签（`v1.0.0-rc.3`）的宿主可能写出的形式（那时 `time` 只注明「RFC 3339」，闰秒、九位以上的小数都算），读取一侧不照收同样是 MAJOR。线上的字段、协议版本与宿主一侧的读写都不变，按本规范实现的宿主与 SDK 之间不会因此互通失败。
2. **六个 SDK 的公开面**：

   | SDK | 信封的 `time` | 读入 | 写出最多几位小数 |
   | --- | --- | --- | --- |
   | Go | `plugin.Envelope.Time` 由 `string` 改为 `*time.Time`（nil 是没有）；`plugin.FromProtoEnvelope` 改为返回 `(Envelope, bool)`，`time` 读不出时为假 | 一致性模块的 `rules.ReadEnvelopeTime`，纳秒，不截断 | 九位（`UTC().Format(time.RFC3339Nano)`） |
   | TypeScript | `Envelope.time` 由 `?: string` 改为 `?: Date \| undefined`（省略或 `undefined` 是没有）；`DeliveredEnvelope.time` 由 `string`（空串是没有）改为 `Date \| undefined`，键总在 | 与截止时间同一个读法（`parseDeadline`），毫秒，第三位之后截断 | 三位（`toISOString()` 去掉小数末尾的 0） |
   | Rust | `plugin::Envelope.time` 由 `Option<String>` 改为 `Option<SystemTime>`，新增 `Envelope::with_time(SystemTime)` | SDK 内部的读法，比本平台 `SystemTime` 更细的截断；纪元之前的时刻先退整秒、再加截断后的小数 | Linux 等九位，Windows 七位 |
   | Java | `Envelope.time()` 由 `Optional<String>` 改为 `Optional<Instant>`；`Envelope.Builder.time(String)` 换成 `time(Instant)`（不收 `null`，不调用即没有） | 纳秒，不截断 | 九位 |
   | Python | `Envelope.time` 由 `str \| None` 改为带时区的 `datetime \| None`；不带时区的构造时抛 `FrameworkError`（`ValueError` 的子类），给字符串抛 `TypeError` | 读成 UTC 的 `datetime`，微秒，第七位起截断 | 六位 |
   | PHP | `Envelope::$time` 由 `string` 改为 `?\DateTimeImmutable`；构造参数改为 `?\DateTimeInterface $time = null`，复制成 `DateTimeImmutable` | 读成 UTC 的 `DateTimeImmutable`，微秒，第七位起截断 | 六位 |

3. **共用的写出与读入**（Go SDK 先定，六个 SDK 的测试按这些用例核对）：

   | 时刻（作者给的，或宿主写来的文本读成的） | SDK 写出 |
   | --- | --- |
   | `2026-11-25T03:25:00Z` | `2026-11-25T03:25:00Z` |
   | `2026-11-25T03:25:00.5Z` | `2026-11-25T03:25:00.5Z` |
   | `2026-11-25T03:25:00.120Z` | `2026-11-25T03:25:00.12Z` |
   | `2026-11-25T03:25:00.001Z` | `2026-11-25T03:25:00.001Z` |
   | `2026-11-25T03:25:00.123456789Z` | 按类型的精度：纳秒 `….123456789Z`，Windows 的 `SystemTime` `….1234567Z`，微秒 `….123456Z`，毫秒 `….123Z` |
   | `2026-11-25T11:25:00.25+08:00` | `2026-11-25T03:25:00.25Z` |
   | `2026-11-24T22:25:00-05:00` | `2026-11-25T03:25:00Z` |
   | `2026-11-26T00:00:00+14:00` | `2026-11-25T10:00:00Z` |
   | `0001-01-01T00:00:00Z` | 原样（Windows 上的 Rust 读不进来，见「影响」） |
   | `9999-12-31T23:59:59.999Z` | 原样 |
   | 没有时间 | 空串 |
   | 换算成 UTC 之后在 10000 年 | 照写，写法各 SDK 不一（见「没有解决的」），宿主拒绝那一项 |

4. **Python 的 `JsonLogHandler`**：写 `extra` 时，任何一层（对象、列表、元组）里的 `RedactedDict`——也就是 `Binding.properties`——换成按升序排列的属性名数组（`sorted(…, key=str)`），与绑定写成 JSON 时的 `propertyNames` 同一口径（ADR-0299 决策 1）；作者自己取出的值（`dict(binding.properties)`、逐项取值）照样写出。只改处理器，`RedactedDict` 本身不变。
5. **规范指针**：这次规范只改了正文、RFC 状态行与一个向量文件的 `$comment`，期望值与档表不变，框架与六个 SDK 的规范指针不必为它再升一次，下次升指针时自然带上。

## 理由

- 时刻是信封的一个值，不是一段文本。让 SDK 负责文本，作者就不必记得那条文法，六种语言写出的文本在精度以内是同一个样子，宿主一侧的读法与写法都不用改。
- 读不出时拒绝投递，而不是悄悄丢掉时间，与宿主拒绝插件写来的不合文法的 `time` 对称；排在找订阅之前，与同一步里别的不合法的请求一致。
- 年份越界照写、交给宿主判，错误的先后与宿主逐项核对一致，与 `xihantenant` 同一个做法；越界的写法是各语言格式化的边角，宿主都拒绝，规范不必统一它。
- Python 的遮值放在处理器写出之前，是唯一拦得住 `json.dumps` 直接写 `dict` 子类的地方；口径与六个 SDK 写绑定的 JSON 相同。

## 影响

- **破坏性改动与迁移**（六个 SDK 都在 0.x；各仓库没有 `CHANGELOG.md`，迁移写在文档站 `docs/sdks/<语言>.md` 的「破坏性改动与迁移」）：
  - Go：`plugin.Envelope.Time` 由 `string` 改为 `*time.Time`，`plugin.FromProtoEnvelope` 改为返回 `(Envelope, bool)` → 发布处把 `Time: t.UTC().Format(time.RFC3339Nano)` 改成 `Time: new(t)`（或 `&t`）；读 `envelope.Time` 的先判 nil；调用 `FromProtoEnvelope` 的接住第二个返回值。
  - TypeScript：`Envelope.time` 由 `string` 改为 `Date`，`DeliveredEnvelope.time` 由 `string`（空串是没有）改为 `Date | undefined` → 发布处把 `time: date.toISOString()` 改成 `time: date`；读 `envelope.time` 的按 `Date` 读，原先判空串的改判 `undefined`。
  - Rust：`plugin::Envelope.time` 由 `Option<String>` 改为 `Option<SystemTime>`，新增 `with_time` → 发布处把 `time = Some(t.to_rfc3339())` 改成 `.with_time(t.into())`（chrono 的 `DateTime` 可以 `into()` 成 `SystemTime`）；读 `envelope.time` 的按 `SystemTime` 读。
  - Java：`Envelope.time()` 改为 `Optional<Instant>`，`Builder.time(String)` 换成 `time(Instant)` → 传 `Instant`（`Instant.now()`、`OffsetDateTime.parse(text).toInstant()`），要文本时自己格式化。
  - Python：`Envelope.time` 由 `str | None` 改为带时区的 `datetime | None`，不带时区的抛 `FrameworkError`、给字符串抛 `TypeError` → 改传 `datetime`（`datetime.now(UTC)`、`datetime.fromisoformat(text)`），要文本时自己 `isoformat()`。`JsonLogHandler` 写 `extra` 里的 `Binding.properties` 时只写属性名，以前写出值 → 确实要值的自己取 `dict(binding.properties)`。
  - PHP：`Envelope::$time` 改为 `?\DateTimeImmutable`，构造参数改为 `?\DateTimeInterface`（缺省 `null`） → 传 `new \DateTimeImmutable(...)`；原先传 `''` 的改为不传或传 `null`；要文本时自己 `format()`。
  - 六个 SDK 共同：宿主写来的 `time` 读不出的投递以 gRPC `InvalidArgument` 拒绝，以前原样交给处理器 → 不用改，按规范写出的宿主不会写来这样的文本。
- **各语言的限制**：
  - TypeScript 的 `Date` 只到毫秒：宿主写来的七位小数读进来只剩三位，读出再写出不是原来的时刻；`Invalid Date` 写成文本 `Invalid Date`，由宿主拒绝那一项。
  - Rust：Windows 上的 `SystemTime` 是 100 纳秒刻度，表示不了 1601 年之前的时刻，这类合文法的文本（如 `0001-01-01T00:00:00Z`）在 Windows 的插件上以 `InvalidArgument` 拒绝那次投递，Linux 等平台读得出；写出的小数 Windows 最多七位、Linux 等最多九位，同一个插件在两个平台上对同一个时刻写出的文本可能不同。
  - Python 的 `datetime` 与 PHP 的 `DateTimeImmutable` 到微秒：宿主写来的第七位小数截掉，写出最多六位。
  - Go 的 `time.Time` 与 Java 的 `Instant` 到纳秒：读入不截断，写出最多九位。
- **规范**（规范工作区已提交）：`plugin/invocation.md` §4（`Envelope.time` 的四条与 `HandleEvent` 第 2 步）、§7，RFC-0017 状态行，`conformance/vectors/plugin/envelope-times.json` 的 `$comment`。MAJOR（`v1.0.0` 之前允许，决策 1）。不加向量，不改档表、`plugin.proto` 与 Go 一致性模块，运行器用例不变。
- **框架**：代码不改，宿主一侧（`PluginEnvelopeMapper`）的读写不变；`framework/samples/EchoPlugin` 直接实现 gRPC 服务、不读 `time`，不受影响。
- **测试**：六个 SDK 都把 `host` 档的 `plugin/envelope-times.json` 拿来自测（`valid` 读成时刻、`invalid` 拒绝、`written` 读得进来，并核对读出再写出的往返），列进各自向量覆盖测试的回放名单；写出按决策 3 的用例核对。Python 另测 `JsonLogHandler` 在 `extra` 的各层遇到 `RedactedDict` 时只写属性名。
- **提交与 ADR 编号**：下面这些提交属于本 ADR，提交信息里的编号不对：Go `553cce2`（写的是 ADR-0293）、TypeScript `3de828a`（ADR-0293）、Rust `d3633d1`（ADR-0293）、Java `099b2b7`（没写编号）、Python `4ba6684`（没写编号）、Python `743630c`（写的是 ADR-0299，`JsonLogHandler` 的遮值）。编号写对的是 Java `a22801c`、Python `5d98d88`、PHP `9585be7`。Go、TypeScript、Rust 源码与测试注释里讲时间类型的几处也引用 ADR-0293。这些提交都还没推送。
- **文档**：文档站 `docs/sdks/` 下六个 SDK 页的能力说明与「破坏性改动与迁移」、`appendix/spec`（RFC-0017 一行）与更新日志待改。

## 没有解决的

- 越界年份的写法六家不一：TypeScript 写 `+010000-…` / `-000001-…`（`toISOString` 的扩展年份），Go 与 Rust 写 `10000-…`，Java 写 `10000-…` 与 `-0001-…`（`Instant.MIN` / `MAX` 也写得出），Python 只会写出 `0000-…` 与 `10000-…`，PHP 写 `0000`、`10000` 与 `-0001`。宿主都以 `messaging.envelope.invalid_time` 拒绝那一项，规范不作规定。
- 规范没收 SDK 写出的向量：`envelope-times.json` 在 `host` 档，`written` 是宿主的写法，决策 3 的写出用例只在各 SDK 的测试里。收进向量要新增文件或用例并放进 `plugin` 档，六个 SDK 都得再升一次规范指针（Go 还要升一致性模块），这次不做。
- Go 运行器与 `XiHan.Framework.Plugins.Conformance` 都不发读不出的 `time`，SDK 以 `InvalidArgument` 拒绝这一步只有各 SDK 的单元测试核对。
- Rust 要不要引入时间库（`time` 或 `chrono`），或公开 SDK 内部的 UTC 时刻类型：只用 `SystemTime` 时，Windows 上收不到 1601 年之前的时刻，两个平台写出的精度也不同。
- TypeScript 的 `Host.invoke` 与 Java 的 `OperationRequest.of(String, String)` 在写出路径上用宽松的 UTF-8：字符串载荷里孤立的代理项分别变成 U+FFFD（`TextEncoder`）与 `?`（`getBytes(UTF_8)`），与 .NET CLI 的 `invoke --json` 同一类。RFC-0001 只管读，写出要不要也严格另议。
- Go 的指针形态：`%v` 打印信封时 `Time` 是指针地址，看不到值。
