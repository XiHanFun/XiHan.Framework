# ADR-0347 SDK 回调宿主前核对写进元数据的 Baggage

## 状态

已采纳（规范 `plugin/invocation.md` §5 新增一项发出之前的核对，§2 与 §8、`conventions/baggage-keys.md`、`conventions/error-codes.md`、RFC-0001「孤立的代理项」的表与状态行、RFC-0017 的状态行已改；向量 `plugin/plugin-messages.json` 登记这条固定的 `reason`；本仓库只加回放测试，代码、错误码、消息键与配置键不变）。

## 日期

2026-11-27

## 背景

- ADR-0334 只规定了宿主写给插件的元数据：已登记键的值有孤立的代理项即这一次不交给插件，`PLUGINS_UNEXPECTED_001` + `plugins.process.failed`，`reason` 为 `baggage '<键>' has a lone surrogate and cannot be encoded as UTF-8`。它的「影响」与 ADR-0340「没有解决的」都留着反方向：插件回调宿主时 SDK 把回调上下文的 Baggage 写进 `metadata`，值里有孤立的代理项（UTF-16 的语言）或不是合法的 UTF-8（字节串的语言）时怎样，各语言没核对，规范没写。
- 读了六个 SDK 的现状（只读）：
  - 六家的上下文都对幂等键（`xihan.idempotency-key`）例外，不按值的约束丢弃（ADR-0204），作者以程序内的 `WithBaggage` 给它一个有孤立代理项的值能留在上下文里；其余已登记的键进入上下文时都核对过；元数据只写已登记的键。
  - 写出时各照 protobuf 库的缺省：TypeScript 的 `@bufbuild/protobuf` 经 `TextEncoder` 换成 U+FFFD、Java 的 protobuf-java 换成 `?`，照常发出，宿主收到的是另一个幂等键；Go 的 protobuf-go 拒绝编码字符串字段里非法的 UTF-8，Python 的 upb 遇到代理项码点抛 `UnicodeEncodeError`，PHP 的 protobuf 赋值时抛出，回调写不出、各以不同的方式失败（后两者按库的行为推断，SDK 里没有针对映射的用例）；Rust 的 `&str` 总是合法的 UTF-8。
- 仓库所有者拍板：本地拒绝、不发出，规范写明 SDK 回调前核对，有这样的值就不发出、回调以写死的错误结束，与宿主一侧对称。

## 备选

核对的范围：

1. **写进 `metadata.baggage` 的全部值**（也就是上下文里已登记的键）：采纳。与宿主一侧 `PluginMetadata.Check` 的范围相同（已登记的键都核对，实际只有幂等键可能出问题）；未登记的键本来不写进元数据，不用核对；键名是已登记的 ASCII 名字，不用核对。
2. **只核对幂等键**：否决。今天只有它能带着这样的值进来，是因为别的键进上下文时核对过；写出的一侧不依赖那道核对，与宿主一侧、与 HTTP 载体写 `baggage` 头（ADR-0321）一样按「写进去的都核对」写。
3. **`trace_parent`、`deadline` 也核对**：否决。都是 SDK 自己按文法写出的 ASCII。

判定：照 RFC-0001「孤立的代理项」，UTF-16 的实现逐个码元，Python 的 `str` 里任何代理项码点都算（成对出现的两个也一样，与 RFC-0001「写出与转交」里 `str` 的代理项码点都写不出一致，`str.encode('utf-8')` 本来就拒绝），字节串的实现看是不是合法的 UTF-8，Rust 不用核对。各家第十轮都已有判定函数（报告逐家列出），不另写。

在本地核对里的位置（现有的顺序：端点 → 权限 → 租户 → 截止时间 / 取消 → `Host.Invoke` 载荷的编码 / `Host.Publish` 逐项 `data` 的编码 → 已关闭）：

4. **截止时间之后、载荷与 `data` 的编码之前**：采纳。权限、租户与截止时间是宿主会给出的错误，写不出请求是本地才有的，与载荷的编码同一个理由排在它们之后（ADR-0312 决策 3）；元数据是整个请求共用的，`Host.Publish` 里是整批的结果，先于逐项的 `data`，整批的错误不被某一项的编码错误盖掉；已关闭是发出这一步本身，照 ADR-0328 排在所有核对之后。
5. **排在载荷的编码之后**：否决。`Host.Publish` 里会出现「部分项是 `data` 的错误、其余项是元数据的错误」，元数据却是整批共用的；两个 RPC 的先后也就不一样。
6. **排在最前（端点之后、权限之前）**：否决。权限与租户的错误宿主也会给，插件在本地先判写不出，会让同一个错误的作者在宿主那里与本地看到不同的先后。

结果：

7. **`PLUGINS_UNEXPECTED_001` + `plugins.process.failed`，`reason` 与宿主一侧逐字相同**：采纳。所有者定了与宿主一侧对称；`plugins.process.failed` 是 SDK 与宿主共用的消息键（`invocation.md` §8），参数 `id`、`phase`、`reason` 现成，`phase` 照回调的其他失败（`Host.Invoke` 为操作 ID，`Host.Publish` 为 `publish`，即 `rules.CallbackPhase`）。不新登记码与键。
8. **字节串的语言另写一段 `reason`（如 `baggage '<键>' is not valid UTF-8`）**：否决。RFC-0001「孤立的代理项」把字节串里的非法 UTF-8 算作同一回事，一段文字让六家与宿主的 `reason` 一样，`plugin-messages.json` 只登记一条。
9. **`KERNEL_VALIDATION_001` 一类的校验错误**：否决。这不是调用方给错了载荷，是上下文写不出；宿主一侧同样按组装请求失败报 `PLUGINS_UNEXPECTED_001`（ADR-0334 备选 4）。
10. **丢掉这个成员照发**：否决，同 ADR-0334 备选 5：插件回调就没有幂等，是静默兜底。
11. **几个键都有时点名哪一个**：不作规定。只有幂等键可能出现，各语言映射的遍历顺序也不同。

向量：

12. **把这段 `reason` 登记进 `plugin/plugin-messages.json` 的 `reasons`**：采纳。SDK 的固定 `reason` 以它为准（`invocation.md` §8），宿主一侧（ADR-0334）以前没有登记，这次一并登记、`phase` 写明两个方向的取值；期望值就是 .NET `PluginMetadata.Check` 写出的文字，.NET 的回放测试核对。
13. **另做一份「回调元数据核对」的输入向量**（码元 / 字节 → 拒或不拒）：否决。判定与 `primitives/baggage-keys.json` 的 `unacceptableUtf16` 是同一条（各家已回放），输入向量只会重复它；本地核对的先后没有现成的向量（`callback-status.json` 只管 gRPC 状态的映射），各家由单元测试覆盖，与 ADR-0328 的「已关闭」一样。

## 决策

1. **规范**（MINOR，写明以前不作规定的行为；对按 TypeScript、Java 现在的做法写出的插件，结果从「换掉照发」变成本地失败）：
   - `plugin/invocation.md` §5：截止时间之后新增一项「核对回调的元数据」，写明范围、各语言的判定、错误（码、键、`id`、`phase`、`reason`）、`Host.Publish` 每一项都是这个错误、只有幂等键可能出现、几个键时不作规定、排在截止时间之后与载荷和 `data` 的编码之前；载荷一项改称「元数据之后还有一项」，`data` 一项的「整批的各项」加上元数据，「已关闭」一项写明元数据的核对也在它之前。§2 宿主写 `baggage` 的一段末尾指向 §5，§8 表里 `plugins.process.failed` 的场合补上两个方向的元数据。
   - `conventions/baggage-keys.md` 幂等键一段补上反方向；`conventions/error-codes.md` 的 `PLUGINS_UNEXPECTED_001` 补上插件一侧。
   - RFC-0001「孤立的代理项」表里缓存键与幂等键一行补上插件回调，状态行同日第六次写明，对应 ADR 加 0347；RFC-0017 状态行同日第六次写明，对应 ADR 加 0347。
   - 向量 `plugin/plugin-messages.json` 的 `reasons` 加一条：`baggage '{key}' has a lone surrogate and cannot be encoded as UTF-8`（`phase` 为 `<操作 ID> | publish | event | <中间件名>`）。
2. **.NET**：宿主一侧不改（ADR-0334 已是这个结果）；`ProtocolVectorTests` 新增一条，核对 `PluginMetadata.Check` 写出的 `reason` 就是登记的那一段（`{key}` 换成键名）、`id` 与 `phase` 照写。.NET 没有插件一侧的 SDK。示例插件 `EchoPlugin` 不经插件 SDK，回调时原样转发宿主写来的元数据（总是合法的 UTF-8，ADR-0334），只有 `echoes.relay` 按请求载荷改写租户（用来核对宿主拒绝越界的租户，ADR-0195），租户 ID 的文法只有 ASCII，宿主按 §5 只接受它随调用交给插件的那一个；示例不是 SDK，这次不跟进。

## 理由

- 两个方向写的是同一个 `InvocationMetadata`：宿主写给插件的已经不经宽松编码（ADR-0334），插件写给宿主的照各库的缺省，结果从「换掉照发」到「整个回调写不出」五花八门；规则只有一条，两个方向应当相同。
- 在本地失败而不是交给 protobuf 库：库的结果各不相同，有的悄悄换掉（另一个幂等键，宿主的 Idempotency 阶段把它当成另一次调用），有的抛出语言自己的异常（作者要另外捕获，与回调的其他失败对不上）。
- 用现有的码、键与宿主那段 `reason`：运营方在宿主与插件的日志里看到的是同一句话；固定的文字登记进向量，六家逐字一致。

## 影响

- **规范**：`plugin/invocation.md` §2、§5、§8，`conventions/baggage-keys.md`、`conventions/error-codes.md`，RFC-0001 的表与状态行，RFC-0017 状态行（都是 MINOR）；向量 `plugin-messages.json` 加一条固定的 `reason`（`plugin` 与 `host` 档）。
- **.NET**：只加测试；行为、公开面不变。
- **六个 SDK**：Go、TypeScript、Java、Python、PHP 要加这项核对（位置、判定函数与要动的测试逐家见报告）；Rust 的 `&str` 总是合法，不用核对，只需确认 `plugin-messages.json` 的新条目不让自己的测试失败。Go 的 `TestRuntimeMessagesFollowTheVectors` 要求每条固定 `reason` 在 SDK 里都有对应，加了核对才通过。
