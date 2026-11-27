# ADR-0339 写进插件协议的错误文案把编不成 UTF-8 的部分换成 U+FFFD

## 状态

已采纳（规范 `plugin/invocation.md` §3 新增「写出 `Error` 的文案」一条，RFC-0001「写出与转交」的范围说明与状态行、RFC-0017 的状态行已改；本仓库 `Plugins.Protocol` 的 `PluginErrorMapper.ToProto`（internal）实现，两种载体共用；公开面、错误码、消息键与配置键不变；向量不变）。

## 日期

2026-11-27

## 背景

- `Error` 的自由文本（`Error.Literal` 的文案，插件协议里的 `text`）可能带孤立的代理项（以 UTF-16 存放字符串的语言），或者在以字节存放字符串的语言里不是合法的 UTF-8。RFC-0001「孤立的代理项」把它划在标识之外：自由文本不拿来查找、比较或散列；「写出与转交」（ADR-0312～0320）只管交给对方按 JSON 文本读的内容，插件协议里 `Error.details` 写不出时不带细节（ADR-0320），文案没有规定。
- 各实现照 protobuf 库与写出器的缺省，结果各不相同：Go 照写非法的 UTF-8，protobuf-go 拒绝编码整条消息，应答在 gRPC 层失败；Python 的 protobuf 抛出 `UnicodeEncodeError`，PHP 的 `setText` 抛出异常，整个错误写不出；Java 的 protobuf 写成 `?`。
- 仓库所有者拍板：统一换成 U+FFFD。`code`、`message_key`、`details` 等照写，不让整个应答或回调失败；替换的粒度照 .NET 能做到、各语言标准库容易做到的定，写进规范。

## 实测（.NET，改之前）

- **gRPC 载体（Google.Protobuf 3.36.2 的二进制编码）**：宿主写给插件的 `text` 里每个孤立的代理项编成 `EF BF BD`（U+FFFD），一个码元换一个，颠倒的一对、末尾的高代理项都一样，成对的代理项照常编成四个字节，不抛出。`PluginErrorMapper.ToProto` 原样把 `Error.Text` 交给它，所以 .NET 的 gRPC 一侧本来就是 U+FFFD。
- **Wasm 载体（Google.Protobuf 的 `JsonFormatter`）**：遇到孤立的代理项抛出 `ArgumentException`（`String contains low surrogate not followed by high surrogate` 一类）。端到端：
  - 插件经 `xihan.invoke` 回调一个以这样的自由文本错误失败的宿主操作：宿主组装应答时抛出，导入函数抛出即陷阱，调用方拿到 `PLUGINS_UNEXPECTED_002`，插件按崩溃重建。
  - 插件贡献的中间件在 `after` 阶段收下游的结果：组装请求时抛出，异常出了中间件，调用以 `KERNEL_UNEXPECTED_001` 结束，下游原本的失败丢了。
  - `xihan.publish` 的逐项结果同样经 `ToStatus` 写出、经同一个写出器编码，发布器交回这样的错误时同上（按代码推断，没有单独的用例）。
- **读入一侧**：Google.Protobuf 读到 `string` 字段里不合法的 UTF-8（`ED A0 80`、`FF`、截断的序列）抛出 `InvalidProtocolBufferException`（`String is invalid UTF-8.`），整条消息读不进；Wasm 载体按 `wasm.md` §2 严格解码整条消息（ADR-0312）。规范对读入已有规定或交给 proto3，照旧。
- **替换的粒度**（各语言实测，`61 … 62` 是 `a … b`）：

  | 输入 | .NET `Encoding.UTF8.GetString` | Python `decode('utf-8','replace')` | PHP 8.3 / 8.4 `mb_scrub`（替换字符设为 U+FFFD） | Go `strings.ToValidUTF8` | Go `for range` |
  | --- | --- | --- | --- | --- | --- |
  | `ED A0 80`（编成 UTF-8 的孤立代理项） | 3 | 3 | 3 | 1 | 3 |
  | `F0 9F 98`（截断的四字节序列） | 1 | 1 | 1 | 1 | 3 |
  | `E2 82` | 1 | 1 | 1 | 1 | 2 |
  | `C0 AF` | 2 | 2 | 2 | 1 | 2 |
  | `F4 90 80 80`（超出 U+10FFFF） | 4 | 4 | 4 | 1 | 4 |
  | `ED A0 80 ED A0 80` | 6 | 6 | 6 | 1 | 6 |

  前三列就是 Unicode 标准 §3.9「U+FFFD Substitution of Maximal Subparts」（WHATWG Encoding 的 UTF-8 解码器同此）。UTF-16 一侧：.NET 的 `Encoding.UTF8` 与 Google.Protobuf、JavaScript 的 `String.prototype.toWellFormed` 与 `TextEncoder`（TypeScript SDK 的 `@bufbuild/protobuf` 二进制写出器经它）都是一个孤立的代理项换一个，即 UTF-16 里的最大子部分。

## 备选

1. **按 Unicode 的「最大子部分」换**：采纳。UTF-16 里每个孤立的代理项是一段（.NET、Java、JavaScript），Python 的 `str` 里每个代理项码点是一段，字节串（Go、PHP）按最大子部分；.NET 能算出全部（UTF-16 用 `Encoding.UTF8` 的替换，字节用 `Encoding.UTF8.GetString`），Python、PHP 的标准库直接就是，JavaScript 有 `toWellFormed`；只有 Go 的标准库没有现成的，十来行（`utf8.FullRuneInString` 找出最长的合法前缀）。
2. **字节串里连着的一串换一个**（Go 的 `strings.ToValidUTF8`）：否决。只有 Go 一行就能做到，.NET、Python、PHP 都要另写；与 Unicode 推荐的做法不同，同一段文本在宿主与插件里换出的个数不同。
3. **字节串里每个字节换一个**（Go 的 `for range`）：否决。理由同上，截断的合法前缀（`F0 9F 98`）换出三个。
4. **文案写不出就不带文案（像 ADR-0320 的细节）**：否决（拍板）。细节不带只是少了参数，宿主仍能按码与键处理；自由文本错误没有键，不带文案读入一侧只能取 `code`，丢的是唯一的说明。文案不拿来比较，换成 U+FFFD 不会让读的一方读成别人的条目。
5. **在 `Error.Literal` 里就拒收孤立的代理项**：否决。原语层只增不改，自由文本不在 RFC-0001 拒收孤立代理项的那张表里；进程内的 `Error` 照旧保留原文（日志、HTTP Problem Details 等诊断输出照各自写出器的缺省，RFC-0001 范围之外），只在写进插件协议时换。
6. **.NET 改在哪一层**：`PluginErrorMapper.ToProto`（采纳）。两种载体的回调与发布的结果、中间件的 `outcome` 都经它写出 `Error`；改在 `PluginJsonCodec` 只修得了 Wasm，而且那里是严格编码（ADR-0312），不该在编码器里替换。
7. **加一致性向量**：这次不加，留给所有者决定（见「影响」）。

## 决策

1. **规范**：
   - `plugin/invocation.md` §3 新增「写出 `Error` 的文案」（MINOR，写明以前不作规定的行为）：`text`（含内部错误的）编不成合法 UTF-8 的部分按最大子部分换成 U+FFFD 写出，其余字段照上一条，不因文案让回调、发布、应答或钩子写不出，不换成 `?`；分 UTF-16、Python `str`、字节串三种写法写清楚哪一段算一段，Rust 不用替换；读入一侧不变。上一条「写出 `Error`」里指向「读入 `Error`」的「下一条」改成「下面「读入 `Error`」」。
   - RFC-0001「写出与转交」的范围说明补上这一类：不按那几条规则拒绝写出，也不照写出器的缺省，统一换成 U+FFFD，指向 `invocation.md`；引言改成「除最后一类外照写出器与读取器的缺省」。状态行追加同日第三次写明，对应 ADR 加 0339。
   - RFC-0017 状态行追加同日第四次写明，对应 ADR 加 0339。
2. **实现**（`Plugins.Protocol`）：`PluginErrorMapper.ToProto` 的 `Text` 经新增的私有函数 `WellFormed`：没有孤立代理项（`UnicodeText.HasLoneSurrogate`）原样交回，否则逐个码元复制，成对的代理项照抄，其余的代理项各换成一个 U+FFFD（长度不变，与 `Encoding.UTF8` 的替换相同）。内部错误递归经同一处。
3. **测试**：
   - `Plugins.OutOfProcess.Tests` 新增 `PluginErrorTextTests`：八种文案（单独的高、低代理项，颠倒的一对，开头与末尾的，连着的两个，成对之后多一个低代理项，多一个高代理项之后成对）各换成对应个数的 U+FFFD，错误码、消息键、细节与内部错误照写；`ToStatus` 写出的状态经 protobuf 二进制与 Wasm 的 JSON 都写得出、读回的是换过的文案（改之前 JSON 抛出）；合法的文案（含成对的代理项与 U+FFFD 本身）与带键的错误原样；Google.Protobuf 的二进制编码本身也换成 U+FFFD（钉住）。
   - `Plugins.Wasm.Tests` 新增 `WasmErrorTextTests`（夹具 `LiteralFailureOperation`）：插件回调以这样的错误失败的宿主操作，调用方读到的是换过的文案与内部错误、细节照常、插件没有陷阱（改之前 `PLUGINS_UNEXPECTED_002`）；中间件的 `after` 阶段收这样的结果，调用方拿到的仍是宿主操作自己的失败（改之前 `KERNEL_UNEXPECTED_001`）。
4. 进程外与 Wasm 的包 README 补一句。

## 影响

- **行为**（`Plugins.Wasm`，Experimental）：回调、发布的结果与中间件的 `outcome` 里自由文本错误的文案有孤立的代理项时，宿主换成 U+FFFD 照常写给插件；以前回调的插件陷阱、按崩溃重建，`after` 阶段的调用以 `KERNEL_UNEXPECTED_001` 结束。进程外（`Plugins.OutOfProcess`）写出的字节不变（protobuf 本来就换成 U+FFFD），只是换在了映射这一层。进程内的 `Error` 不变，调用方与日志看到的仍是原文。
- **公开面**：不变；没有新的错误码、消息键与配置键。
- **规范**：`plugin/invocation.md` §3（MINOR）、RFC-0001 范围说明（MINOR）、RFC-0017 与 RFC-0001 状态行。向量不变。
- **六个 SDK**：要跟（`plugin` 档里插件一侧写出 `Error` 的规则），逐家见报告；Rust 不用改。
- **向量**：没有加。若要加（例如 `plugin/error-text-writing.json`：UTF-16 的输入以 JSON 转义给出、字节串的输入以十六进制给出，期望值由 .NET 的 `Encoding.UTF8` 算出），它属于 SDK 认领的 `plugin` 档，放进去即让六个 SDK 的覆盖测试失败，直到各自回放；.NET 的 `SpecVectorCoverageTests` 不认领 `plugin` 档，不受影响。由所有者决定加不加、与 SDK 的跟进同一轮还是之后。

## 理由

- 文案是给人看的说明，不是标识：换成 U+FFFD 丢的只是那几个本来就显示不出来的码元，整个错误、乃至整个回调与应答写不出丢的是全部；读的一方也不会因此读成另一个错误，错误码与消息键照写。
- 最大子部分是 Unicode 推荐、WHATWG 采用的做法，.NET、Python、PHP、JavaScript 的标准解码器都已经这样换，同一段坏文本在宿主与插件里换出相同的结果；只有 Go 要多写十来行。
- 换在写出的一方：读的一方的 protobuf 库按 proto3 可能拒收整条消息（.NET、Go），Wasm 载体又是严格解码，读的一方没有机会再换。
