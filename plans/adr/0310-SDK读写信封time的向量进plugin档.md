# ADR-0310 SDK 读写信封 time 的向量进 plugin 档

## 状态

已采纳（规范新增向量 `conformance/vectors/plugin/sdk-envelope-times.json`（档 `plugin`），`plugin/envelope-times.json` 也进 `plugin` 档；`plugin/invocation.md` §4 的向量一条与 §7、RFC-0017 状态行与「一致性」、`conformance/README.md` 与 `envelope-times.json` 的 `$comment` 已改；Go 一致性模块新增类型化入口 `conformance.SDKEnvelopeTimes()` 与规则 `rules.TruncateEnvelopeTime`、`rules.FormatSDKEnvelopeTime`；框架代码不改，六个 SDK 待跟）。

## 日期

2026-11-26

## 背景

- ADR-0306 把六个 SDK 的信封 `time` 改成各语言的时间类型，写出与读入的规则写进了 `plugin/invocation.md` §4「SDK 读宿主写来的」「SDK 写给宿主的」，但没有加向量（ADR-0306 决策 1、「没有解决的」第 2 条）。规范里与 `time` 有关的向量只有 `plugin/envelope-times.json`，它在 `host` 档：`valid` / `invalid` 是宿主读插件写来的文本，`written` 是宿主写给插件的文本（七位小数、`+00:00`）。SDK 写出的样子（`Z`、去掉小数末尾的 0、最多写到类型的精度）与按精度截断的读法只在各 SDK 的单元测试里，用例由 Go SDK 先定、其余五个照着写（ADR-0306 决策 3）。
- 六个 SDK 用到四种精度：纳秒（Go 的 `time.Time`、Java 的 `Instant`、Linux 等平台上 Rust 的 `SystemTime`）、100 纳秒（Windows 上 Rust 的 `SystemTime`）、微秒（Python 的 `datetime`、PHP 的 `DateTimeImmutable`）、毫秒（TypeScript 的 `Date`）。同一个时刻在四种精度下写出的文本、读出的时刻都不同；一条用例只给一个期望值时，SDK 得自己按精度推算期望值，推算的正是要核对的那条规则。
- 六个 SDK 都已把 `envelope-times.json` 列进各自的回放名单（ADR-0306「影响」的测试一条），但它不在 `plugin` 档，回放与否不受各自覆盖测试的约束。
- 仓库所有者已拍板：SDK 写出与读入 `time` 的向量收进规范，放在 SDK 认领的 `plugin` 档；`envelope-times.json` 是宿主的写法，与 SDK 的写法不同，不混在一起；越界年份规范不作规定，不收。

## 备选

1. **在 `envelope-times.json` 里加一节 SDK 的写出与读入，把整个文件放进 `plugin` 档**：否决。同一个文件里就有两种写法（宿主的七位小数加 `+00:00`，SDK 的 `Z` 与去掉末尾的 0），`written` 与新的一节容易被当成同一回事读反；`host` 档的实现（.NET）要回放整份文件，SDK 的写法却没有 .NET 的实现可以回放。
2. **新开文件，只进 `plugin` 档**（采纳）：文件名 `sdk-envelope-times.json`，与 `envelope-times.json` 同一个主题，前缀标明是 SDK 一侧；文件名全仓唯一（一致性模块的 `TestVectorFileNamesAreUniqueAcrossDirectories`）。
3. **时刻的表示**：
   - 拆成年、月、日、时、分、秒、偏移与纳秒几个数：否决。JSON 冗长，六个 SDK 还得各写一段拼装，拼装本身又是一处可能出错的代码。
   - 纪元以来的纳秒整数：否决。超过 2^53，JavaScript 与 PHP 的 JSON 读法会丢精度；纪元之前是负数，截断的方向要另作约定。
   - 合 RFC-0003 规则 8 文法的文本，精确到纳秒（采纳）：六个 SDK 都已有按这条文法读文本的实现（投递时读宿主写来的 `time` 用的就是它），读入时比精度更细的小数截去，得到的正是「按自己的精度截断」的时刻；原文带偏移，时间类型带偏移的 SDK（Go、Python、PHP）可以照原文的偏移构造，核对写出时换算成 UTC 那一步。
4. **精度的写法**：
   - 按精度分成四组用例：否决。同一个时刻在四种精度下的差别是要核对的重点，分在四处不好对照，用例也要写四遍。
   - 只给纳秒的期望值，由 SDK 自己截断：否决，理由见背景第 2 条。
   - 每条用例给四种精度各自的期望值，键是 `nanoseconds`、`hundredNanoseconds`、`microseconds`、`milliseconds`，文件顶上 `precisions` 写各精度写出时最多的小数位数（采纳）：SDK 取自己那一项。键用名字、不用位数（`"9"`、`"7"`），各语言按字段读更直接；位数另给，SDK 可以据此核对写出的小数不超过精度。
5. **读入的用例**：
   - 把 `envelope-times.json` 的 `valid` 抄一遍、补上四种精度：否决，两处重复，改一处容易漏另一处。
   - 只在新文件的 `$comment` 里说 `envelope-times.json` 对 SDK「同样适用」：否决。档表才是实现必须回放什么的事实源，各实现的覆盖测试读档表、不读 `$comment`。
   - `envelope-times.json` 同时进 `plugin` 档，新文件只收它没有的（采纳）：`valid`（时刻截到精度后比较）与 `invalid` 对 SDK 的读入同样适用；新文件的 `reading` 只收截断起作用的文本，`unreadable` 只收 `invalid` 没有的几类；一致性模块的测试核对两个文件不重复。`written` 是宿主的写法，两个文件的 `$comment` 与 §4 都写明它不是 SDK 写出的样子。
6. **越界年份**：不收（所有者拍板；§4 写明这样的时刻写成什么不作规定）。
7. **期望值的来源**：.NET 没有插件一侧的 SDK，SDK 的写法 .NET 不实现，照规范「期望值由 .NET 算出」的惯例做不到。采纳：由 Go 一致性模块算出（`rules.ReadEnvelopeTime` 读成纳秒的 UTC 时刻，再按精度截断、写出），并逐条交叉核对：100 纳秒一列由 .NET 参考实现的 `DeadlineText.TryParse`（宿主读 `time` 的同一个解析，100 纳秒、截断）与自定义格式 `FFFFFFF`（去掉末尾的 0，全为 0 时连小数点也不写）核对，`unreadable` 由 `DeadlineText.TryParse` 核对；四列由 Java 的 `Instant` 核对，微秒与毫秒两列又分别由 Python 的 `datetime` 与 JavaScript 的 `Date` 核对。核对的程序放在临时目录，不提交。
8. **规则包加不加函数**：加 `rules.TruncateEnvelopeTime` 与 `rules.FormatSDKEnvelopeTime`（采纳）。规则包的约定是一条规范规则一个函数、由向量验证，SDK 一侧的截断与写法是 §4 的两条规则；Go SDK 也可以直接用它们写出。精度的参数用 `time.Duration`，不用位数：这是 Go 的惯用写法，位数与 `Duration` 的对应由测试按向量的 `precisions` 核对。
9. **.NET 回放新文件**：不回放。架构测试 `SpecVectorCoverageTests` 认领 `plugin` 之外的全部档（`_unclaimed = ["plugin"]`：.NET 是宿主，示例插件不经插件 SDK），新文件只在 `plugin` 档；`envelope-times.json` 本来就在 `host` 档、由 `ProtocolVectorTests` 回放，多进一个 `plugin` 档不改变 .NET 的义务。

## 决策

1. **新增向量** `conformance/vectors/plugin/sdk-envelope-times.json`，档 `plugin`：
   - `precisions`：`{"nanoseconds": 9, "hundredNanoseconds": 7, "microseconds": 6, "milliseconds": 3}`。
   - `writing`（25 条）：`{name, instant, text}`。`instant` 是作者给的时刻，写成合规则 8 文法的文本、精确到纳秒，`null` 是没有给时间；`text` 是四种精度下 SDK 写给宿主的文本，没有时间为空串。覆盖整秒、一位小数、小数末尾的 0 去掉、开头的 0 保留、九位与七位小数按精度截断、截断之后末尾的 0 去掉、截断之后全为 0 不写小数点、截断不进位、宿主写来的文本读出再写出、正偏移与负偏移的换算（含换算到前一日、后一日、前一年与闰日，偏移带分钟）、纪元与纪元之前、`0001-01-01T00:00:00Z` 与换算成它、`9999-12-31T23:59:59` 带九位与三位小数与换算成它、没有时间。
   - `reading`（13 条）：`{name, text, utc}`。`text` 是宿主写来的文本，`utc` 是四种精度下读出的 UTC 时刻（九位小数加 `Z`，与 `envelope-times.json` 的 `utc` 同一写法）。覆盖宿主写的七位小数、九位小数截断不进位、不进到后一日、最晚的时刻不进到 10000 年、最早的时刻之后 100 纳秒、纪元之前截断不晚于原文与换算到纪元之前（只截秒的小数、向下取，不朝零取）、小写的 `t` 与 `z`（含带偏移）、`-00:00`、换算到后一日与前一年之后再截断、四百年的闰日。
   - `unreadable`（18 条）：`{name, text}`，`envelope-times.json` 的 `invalid` 之外 SDK 同样必须读不出的文本：小数点之后没有数字，时为 24、分为 60、月为 13 与 00、日为 00、这个月没有 31 日、百年不闰（宽松的解析会把它们进位成别的日子），时只有一位，基本格式，偏移只有时、偏移的时只有一位、偏移用 U+2212 减号，`UTC` 字样，`Z` 之后又有偏移，年份带正号，开头带 BOM，末尾带 NUL。
   - 时间类型表示不了的时刻（Windows 上 Rust 的 `SystemTime` 表示不了 1601 年之前）向量不另标：写出的用例跳过，读入的用例按读不出核对（§4「SDK 读宿主写来的」）。
2. **`envelope-times.json` 进 `plugin` 档**，同时留在 `host` 档；只改 `$comment`（写明它在两档、`valid` 与 `invalid` 对 SDK 的读入同样适用、`written` 不是 SDK 的写法），期望值不动。
3. **规范正文**：`plugin/invocation.md` §4 的向量一条改成两个文件各一条（「2026-11-26 补充，ADR-0310」，以前 `envelope-times.json` 只在 `host` 档、SDK 写给宿主的文本没有收向量），§7 写明 SDK 的测试回放这两个文件；RFC-0017 状态行追加「2026-11-26 修订」、对应 ADR 加 0310，「一致性」的数据向量加上新文件；`conformance/README.md` 的向量表加一行、规则一节加两个函数。
4. **Go 一致性模块**：类型 `SDKEnvelopeTimeVectors`、`SDKEnvelopeTimeWriting`、`SDKEnvelopeTimeReading`、`SDKEnvelopeTimeUnreadable`、`EnvelopeTimePrecisionDigits`、`EnvelopeTimeTexts`，入口 `conformance.SDKEnvelopeTimes()`，向量文件表加一行；规则 `rules.TruncateEnvelopeTime(instant, precision)`（换算成 UTC、按距零时刻的整倍数向下截断）与 `rules.FormatSDKEnvelopeTime(instant, precision)`（截断后按 `time.RFC3339Nano` 写出）。测试 `rules.TestSDKEnvelopeTimesFollowTheVectors` 回放四种精度：写出的文本、读回截断后的时刻、小数位数不超过 `precisions`、大写的 `T` 与 `Z`、末尾没有 0；读入截断后不晚于原文、相差不到一个精度；`unreadable` 读不出；`reading` 与 `unreadable` 不与 `envelope-times.json` 重复。`vectors_test.go` 的非空核对要求名字在全文件唯一、作者给了时刻时四种精度都有文本。
5. **规范级别**：MINOR。新向量文件、把向量加进已有的档都是 MINOR（RFC-0024「版本」第 3 条、「一致性档」第 4 条）；期望值全部按 ADR-0306 已写进 §4 的规则算出，没有改已经写明的行为。
6. **规范指针**：框架的指针在整合时统一升；`SpecVectorCoverageTests` 不认领 `plugin` 档，升指针不需要新的回放。六个 SDK 升到这个提交后，各自的覆盖测试会因为 `plugin` 档多了 `sdk-envelope-times.json` 失败，要回放它（Go 另把一致性模块升到同一提交）。

## 理由

- 同一个时刻在不同精度下写出与读出的结果不同，是 SDK 一侧最容易各自走样的地方；把四种精度并排写进同一条用例，SDK 只取自己那一项，不必在测试里再推算一遍规则。
- 时刻用文法文本表示，复用了每个 SDK 都已有、而且本来就要核对的读法，不为向量另造一种输入格式；精度以内的截断就发生在构造时刻的那一步，与投递时读宿主文本是同一条路径。
- 宿主的写法与 SDK 的写法分在两个文件，`host` 档的实现只回放宿主的那份，`plugin` 档的实现两份都回放、只按自己的精度比较；把 `envelope-times.json` 放进 `plugin` 档，让 SDK 读入的义务由档表约束，而不是只靠各 SDK 自愿列进回放名单。
- 期望值没有 .NET 的 SDK 实现可算，就用一致性模块的规则算，再用参考实现的解析与四种语言的标准时间类型交叉核对，任何一处算错都会在核对里对不上。

## 影响

- **规范**（规范工作区已提交）：新增 `conformance/vectors/plugin/sdk-envelope-times.json`；`conformance/profiles.json` 的 `plugin` 档加上它与 `plugin/envelope-times.json`；`plugin/invocation.md` §4、§7；RFC-0017 状态行与「一致性」；`conformance/README.md`；`envelope-times.json` 的 `$comment`；`conformance/vectors.go`、`vectors_test.go`、`rules/envelopetime.go`、`rules/plugin_test.go`。`plugin.proto`、运行器用例与错误码不变。MINOR。
- **框架**：代码不改。`SpecVectorCoverageTests` 不认领 `plugin` 档，不回放新文件；`envelope-times.json` 只改了 `$comment`，`ProtocolVectorTests` 照样通过。
- **六个 SDK**：升规范指针后要回放 `sdk-envelope-times.json`，按各自时间类型的精度取那一项：Go 纳秒，TypeScript 毫秒，Rust 在 Windows 上 100 纳秒、在 Linux 等平台上纳秒，Java 纳秒，Python 与 PHP 微秒。`writing` 经 SDK 自己读文本的实现构造时刻（带偏移的类型宜保留原文的偏移），经发布的写出路径写成文本比较；`reading` 经投递的读入路径读出、与那一项的时刻比较；`unreadable` 的每一条都必须读不出（投递以 gRPC `InvalidArgument` 拒绝）。Windows 上的 Rust 遇到 1601 年之前的时刻：写出跳过、读入按读不出核对。`envelope-times.json` 已在各自的回放名单里，现在是 `plugin` 档要求的，名单里写着「host 档」的注释要改。
- **文档**：文档站 `docs/appendix/spec.md` 的向量一行与 RFC 表 0017 一行、更新日志待改；`docs/sdks/` 下各页不用改（SDK 的行为不变）。

## 没有解决的

- ADR-0306「没有解决的」里「规范没收 SDK 写出的向量」一条由本 ADR 解决：SDK 写出与读入的用例收进 `plugin` 档的 `sdk-envelope-times.json`，`envelope-times.json` 也进 `plugin` 档。
- 越界年份的写法仍不作规定、不收用例，六个 SDK 的写法照旧不一（ADR-0306「没有解决的」第 1 条）。
- 运行器与 `XiHan.Framework.Plugins.Conformance` 仍不发读不出的 `time`：`unreadable` 给出了文本，SDK 以 `InvalidArgument` 拒绝那次投递、排在找订阅之前这一步，仍只由各 SDK 的单元测试核对（ADR-0306「没有解决的」第 3 条）。
- 这份向量的期望值不是由 .NET 算出的（备选 7）：插件一侧没有 .NET 实现，100 纳秒一列与 `unreadable` 由 .NET 的解析核对，其余由一致性模块的规则算出、多种语言的标准库核对。规范仓库「期望值由 .NET 算出」的约定没有写这类例外，要不要补写，交给仓库所有者。
- Rust 是否引入时间库仍未定（ADR-0306「没有解决的」第 4 条）；引入之后 Windows 上的精度与范围变了，按向量取的那一项也随之改变。
