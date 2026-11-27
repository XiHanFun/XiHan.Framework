# ADR-0349 六个 SDK 跟进错误文案向量与回调元数据核对，Go 补上已关闭状态

## 状态

已采纳（只改六个 SDK 与它们的 `CHANGELOG.md`；规范与框架代码不改。六家的 `spec/` 升到规范 `2bf33b1`，回放 `plugin/error-text-writing.json`（ADR-0346）；Go、TypeScript、Java、Python、PHP 回调宿主前核对写进元数据的 Baggage（ADR-0347），Rust 只加注释；Go 记下自己已关闭，之后的回调 `reason` 与另四家相同）。

## 日期

2026-11-27

## 背景

第十一轮宿主一侧改了规范，六个 SDK 要跟进三件事：

- **ADR-0346** 新增向量 `plugin/error-text-writing.json`，进 `plugin` 档。六个 SDK 都认领 `plugin` 档，覆盖测试要求回放；第十轮（ADR-0339）各家为「最大子部分换 U+FFFD」手写的用例与向量部分重叠。
- **ADR-0347** 让 `plugin/invocation.md` §5 新增一项发出之前的核对：要写进 `metadata.baggage` 的值有孤立的代理项（字节串的实现里是不合法的 UTF-8）即不发请求，结果是 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`，`reason` 与宿主一侧相同，排在截止时间与取消之后、载荷与 `data` 的编码之前，已关闭之前。各家以前怎样没核对过。
- **Go 关闭之后的 `reason`**（ADR-0340「没有解决的」）：Go 没有自己的「已关闭」状态，`Serve` 退出时关掉连接，之后的回调由 grpc-go 报 `Cancelled: grpc: the client connection is closing`；另四家是 `Unavailable: the host client is closed`。所有者定：Go 补上已关闭状态。

## 备选

1. **手写用例删到哪里**：
   - 规则被向量覆盖就删：否决。向量只列了典型的几种输入，规则相同、字节不同的用例（Go 的 `E0 80`、`ED A0`、`F0 9F 41`）删了就没有地方钉住。
   - 输入与向量逐字节（逐码元、逐码点）相同的删，其余留下（采纳），另留一个结构用例核对向量表达不了的：其余字段照写、内部错误也替换、带键的错误不写 `text`、protobuf 往返。
2. **各家回放哪一组**：按语言存放字符串的方式（采纳）——Go、PHP 是字节串，回放 `bytes`；TypeScript、Java 是 UTF-16，回放 `utf16`；Python 的 `str` 是码点序列，回放 `codePoints`，另按 RFC-0012 把 `utf16` 组成 `str` 回放；Rust 的 `String` 总是合法，经 `String::from_utf8_lossy` / `from_utf16_lossy` 把 `bytes` 与 `utf16` 换成 `String` 再经 `to_proto_error` 比对，实测两个函数的粒度都与最大子部分一致（`codePoints` 放不进 `char`，不回放）。
3. **元数据核对的判定**：
   - 用完整的 Baggage 值判定（长度、控制字符等）：否决，幂等键不受这些约束（ADR-0204 的例外），会误拒。
   - 只看编码合法性（采纳）：UTF-16 逐个码元，Python 的 `str` 里任何代理项码点都算（成对的两个也算），字节串看是不是合法的 UTF-8；Rust 的 `&str` 总是合法，不核对，在 `metadata_of` 写明为什么。
4. **几个键都有问题时点名哪一个**：规范不作规定。Go、TypeScript 按键名排序取第一个，Java、Python、PHP 取迭代遇到的第一个（Python 的 dict 与 PHP 的数组是插入顺序，Java 取决于 `Context.baggage()` 返回的 Map）。不统一：结果的码、键、`phase` 相同，只有 `reason` 里点名的键可能不同。
5. **Go 的已关闭状态**：
   - 照旧由 grpc-go 报出，只把 `reason` 改写：否决，文字来自 grpc-go，`Serve` 退出之后的回调与途中被切断的回调都要识别。
   - `Host` 记下已关闭（采纳）：`closed atomic.Bool`，`Close` 先置位再关连接，保证被切断的回调一定看得到；发出之前的核对在元数据之后看已关闭；途中失败时已关闭且 grpc 报 `Canceled` 或 `Unavailable` 的，按同一个错误报（截止时间已到或调用方已取消的照映射表相应的行）；`Close` 改为可重复调用、返回 nil。

## 决策

1. 六家 `spec/` 升到 `2bf33b12a1601eaa95e6cbbe51fb2bee7d3d0ad7`；Go 的一致性模块升到 `v1.0.0-rc.3.0.20261127091450-2bf33b12a160`。
2. 六家回放 `error-text-writing.json`（各自的组见备选 2），覆盖测试加上它；与向量逐一相同的手写用例删掉，其余留下。
3. Go、TypeScript、Java、Python、PHP 在回调前核对元数据：不合法即不发请求，`PLUGINS_UNEXPECTED_001` + `plugins.process.failed`，`phase` 为操作 ID 或 `publish`（`Host.Publish` 每一项同一个错误），`reason` 为 `baggage '{key}' has a lone surrogate and cannot be encoded as UTF-8`；各家测试把它与 `plugin-messages.json` 对上。`reason` 常量都不进公开面。
4. Go 补已关闭状态，`reason` 为 `Unavailable: the host client is closed`，结果仍是 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable`。

## 理由

- 认领了 `plugin` 档就要回放它的全部向量（RFC-0024）；向量的期望值由 .NET 算出，一份数据对六家，比六份手写用例可靠。
- 元数据写不成 UTF-8 时各家原来的结果五花八门：Go 以 grpc 的编码错误结束、TypeScript 换成 U+FFFD 照发、Java 换成 `?` 照发（宿主收到的是另一个幂等键，重复的调用会执行两次）、Python 与 PHP 的异常从回调里漏出来；在本地以同一个结果结束，与宿主一侧对称。
- `reason` 是排障时看到的文字，五家同一种情形说同一句话（ADR-0340）。

## 影响

改前（实测）→ 改后：

| SDK | 元数据写不成 UTF-8 时 | 改后 |
| --- | --- | --- |
| Go | 已是 `plugins.process.failed`，`reason` 为 `Internal: grpc: error while marshaling: string field contains invalid UTF-8`，请求没发出 | `reason` 为规范那一句 |
| TypeScript | `TextEncoder` 换成 U+FFFD 照发，宿主收到另一个幂等键 | 本地失败（升级须知） |
| Rust | 不适用 | 只加注释 |
| Java | protobuf-java 换成 `?` 照发（`k\uD8001` → `k?1`） | 本地失败（升级须知） |
| Python | upb 在 `to_metadata` 抛 `UnicodeEncodeError`，漏出 `Host.invoke` / `publish` | 本地失败（修复） |
| PHP | `setBaggage` 抛 `Expect utf-8 encoding.`，漏出回调，外层以 `KERNEL_UNEXPECTED_001` 结束 | 本地失败（修复） |

- **Go**：`Host` 关闭之后与被关闭切断的回调，`reason` 由 `Cancelled: grpc: the client connection is closing` 变为 `Unavailable: the host client is closed`；`Host.Close` 第二次调用返回 nil（以前返回 grpc 的错误）；已关闭之后宿主恰好回 `Unavailable` 的也报成已关闭。`-race` 在本机已有的 `golang:1.27` 一次性容器里跑过。
- **TypeScript、Java**：从「照发」变为本地失败，各自的 `CHANGELOG.md` 写进升级须知；Python、PHP 记为修复。
- **测试**：ADR-0346 只改测试，各家 `CHANGELOG.md` 抬头写明测试不列入，只改了「规范指针」一行（Go、TypeScript、Rust）或没有条目（Java、Python、PHP）。
- **文档**：框架文档站 `docs/sdks/*.md` 已改。

## 没有解决的

- Go 的 `scrubText` 与一致性模块新增的 `rules.WriteErrorText` 是两份实现，向量对两份都独立验证；要不要让 SDK 直接用 `rules` 的那份没有定。
- Rust 的 `Host` 没有关闭操作，§5「已关闭」一项在 Rust 里不出现；运行时收尾之后再回调会怎样没有查。
- 几个键都有问题时点名哪一个，各家不同（备选 4）。
