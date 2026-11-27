# ADR-0332 CloudEvents 头值照 1.0.2 编解码、空的 subject 不合法与配置登记数据模式

## 状态

已采纳。

- 规范：
  - `protocol/cloudevents.md`：信封映射写明空的 `subject` 与 `time` 在三种模式里都不合法、插件协议的空串仍是没有；入站 binary 一行、「信封不合法」一条与「binary 的头值」（先解开双引号、再一轮百分号解码，新增坏的 quoted-string 一类）已改；「出站」写明头值编码空格与双引号。
  - RFC-0009：「信封」第 2 条补 `subject`，「各传输上的 `dataschema`」补 `subject` 与 `time` 的同一个区别，「框架事件的数据模式」第 1 条补配置节，「一致性」的向量表补一句；状态行与「对应 ADR」已改。
  - 向量 `messaging/cloudevents-envelopes.json` 的 `invalid` 新增「subject 为空」；Go 一致性模块的 `rules.DecodeCloudEvent` 同步拒绝它。
  - 错误码与消息键不变。
- .NET：
  - `Protocols.CloudEvents`（`Preview`）：`CloudEventsHttpBinding.EncodeHeaderValue` 编码空格与双引号，`DecodeHeaderValue` 先解开双引号，`ReadBinary` 把空的 `ce-subject`、`ce-time` 头交给同一条核对。
  - `Messaging.Abstractions`：`MessageEnvelope.Subject`（`Stable`）的 `init` 拒绝空串；`MessagingOptions`（`Stable`）新增 `DataSchemas`，从 `XiHan:Messaging:DataSchemas` 读；`MessagingRegistration.Configure` 把它们登记进 `MessageDataSchemas`。
  - 命令行工具的配置索引补上两个新键。
- 修订 ADR-0323 的决定 17 与备选 23，末尾加一节。Kernel 与 Primitives 没有改动。

## 日期

2026-11-27

## 背景

ADR-0323「没有解决的」四条，仓库所有者都定为做：

1. **出站 binary 的头值不编空格与双引号**：CloudEvents HTTP 绑定 1.0.2 §3.1.3.2 要求发送方把 U+0020、U+0022、U+0025 与 U+0021–U+007E 之外的字符都百分号编码，理由是「每个空格与双引号都已编码，就不需要引号转义」。`EncodeHeaderValue` 只编 `%` 与可打印 ASCII（含空格）之外的字符，`ce-subject: order/42 a` 这样的头原样发出；值里的 `"` 照写，接收方按同一节先解开 quoted-string，读到的是另一个值。
2. **入站不解开双引号括起的头值**：§3.1.3.2 要求接收方先按 RFC 7230 §3.2.6 解开 quoted-string（含 `\` 的 quoted-pair），再做一轮百分号解码，并说明这是为了兼容较早版本那些不编空格与双引号的发送方。`DecodeHeaderValue` 只做百分号解码，`"order 42"` 读成带引号的 `"order 42"`。
3. **空的 `subject` 与 `time`**：CloudEvents 1.0 规定 `subject` 出现时必须是非空字符串。结构化 JSON 的 `"subject": ""` 读成空串，binary 的空 `ce-subject`、`ce-time` 头与没有这个头相同；`"time": ""` 本来就不合文法。ADR-0323 决定 15 让空的 `ce-dataschema` 头不合法，`subject` 与 `time` 留了下来（决定 17）。
4. **数据模式只能在代码里登记**（ADR-0323 备选 23）：`AddMessageDataSchema(type, dataSchema)` 的 `type` 带点号（`orders.placed`），写成配置字典的键时环境变量写不出（`XiHan__Messaging__DataSchemas__orders.placed`），容器部署只能改代码。

## 备选

出站编码：

1. **照 §3.1.3.2 编码空格与双引号**：采纳，照所有者的决定。
   - 判定改成 U+0021–U+007E 之中除 `%` 与 `"` 以外的照原样，其余（含空格、U+007F 与控制字符）按 UTF-8 字节写成 `%XX`，十六进制大写，照旧。
   - 编出的值不含空格与双引号，不用再括引号；接收方不论按 1.0 还是 1.0.2 读，读到的都是原值。
   - 这是出站行为的变化：Webhook 接收方看到的头值从 `order 42` 变成 `order%2042`。按 CloudEvents 解码的接收方读到的值不变，把头值当原文用的接收方会看到百分号序列。
2. **只编空格，双引号照写**：否决。§3.1.3.2 两个都要求；接收方先解 quoted-string，原样的 `"` 会被当成引号。

入站解开双引号：

3. **照 RFC 7230 §3.2.6 在整个头值里解开 quoted-string，再做一轮百分号解码**：采纳。
   - 从左到右，引号之外的字符照原样；`"` 开始一个 quoted-string，到下一个没有转义的 `"` 为止，两个引号不算内容；里面 `\` 与紧跟的一个字符是这个字符本身。
   - 括起的部分与没括起的部分可以相连（`a"b c"d` 读成 `ab cd`）：§3.1.3.2 说「任何头值都先按双引号字符串解开」，不限于整个值括起来的情形；RFC 7230 的字段值本来就可以在其中出现 quoted-string。
   - 引号之外的 `\` 是普通字符：quoted-pair 只在 quoted-string 里有意义（RFC 7230），以前读得出的 `a\b` 照旧读成 `a\b`。
   - 百分号解码只做一轮：引号里的 `%2541` 读成 `%41`，不读成 `A`（§3.1.3.2：「然后做一轮百分号解码」）。
   - 没有引号的值不另建字符串，与以前完全相同。
4. **只在整个值被一对引号括起时才解开**：否决。§3.1.3.2 不作这样的限制；`a"b"` 这样的值在这种读法下照原样留着引号，与按 RFC 7230 读的接收方读成两个值。
5. **坏的 quoted-string 是读不出的头（`header_invalid`）**：采纳，照所有者的决定，与 ADR-0323 的百分号解码同一条路（400 `MESSAGING_VALIDATION_002`，`protocols.cloudevents.header_invalid`，参数是小写的头名）。坏的情形：
   - 引号不配对：开始的 `"` 之后再没有结束的 `"`（`a"b`、`"abc`、`"a\"`）。以前 `a"b` 读成 `a"b`；按 1.0.2 写出的发送方不会发出原样的 `"`，按 1.0 写的发送方发出它时，接收方按 1.0.2 读也读不出同一个值，照原样留着就是一个值两种读法（同 ADR-0323 备选 11 对没编码的 `%` 的理由）。
   - quoted-string 里有 RFC 7230 的 qdtext 不收的字符：水平制表符之外的控制字符（U+0000–U+001F）与 U+007F。
   - `\` 在 quoted-string 的末尾、后面没有字符，或后面是这样的控制字符（quoted-pair 只收 HTAB、SP、VCHAR 与 obs-text）。
6. **U+0080 及以上的字符按 obs-text 照收**：采纳。.NET 的头值是字符串，服务器怎样把头的字节变成字符各有设置；RFC 7230 的 obs-text 是 0x80–0xFF 的字节，这里把字符串里 U+0080 及以上的字符都当作它，交给百分号解码那一步按 UTF-8 编码（与以前不在引号里的非 ASCII 字符相同），孤立的代理项在那一步拒绝。
7. **解开放在 `DecodeHeaderValue` 里**：采纳。它是公开的辅助，文档写的就是「CloudEvents 绑定的解码」，§3.1.3.2 的解码是这两步；`ReadBinary` 对每个 `ce-*` 头调用它，规范属性与扩展属性同一条路。签名不变（`Preview`，行为变化记进更新日志）。

空的 `subject` 与 `time`：

8. **`subject`：信封的 `init` 拒绝空串，三种模式都走它**：采纳，照所有者的决定与 ADR-0323 决定 15 的做法。
   - `MessageEnvelope.Subject` 给了空串即抛出 `ArgumentException`；`CloudEventsFormat.Decode` 与 `ReadBinary` 已经把构造信封的参数异常转成 `MESSAGING_VALIDATION_002` + `messaging.envelope.invalid`（`reason` 是异常消息），binary 的空 `ce-subject` 头与 structured 的 `"subject": ""` 是同一个错误、同一个 `reason`。
   - 在构造时拒而不只是在读入时拒：RFC-0009「信封」要求写出的文档读得回来，只拒读入的话 `Encode` 会写出读不回来的 `"subject": ""`；构造时拒，写出一侧就不会遇到它。
   - `MessageEnvelope` 是 `Stable`：签名不变，以前能构造的空 `subject` 现在抛出，记进升级须知。框架自己构造的信封里，调度与工作流的 `subject` 是标识与非空的事件名，不受影响；领域事件的 `subject` 选择器返回空串时，派发时构造信封抛出（以前带着空的 `subject` 发出）。
9. **只有空白的 `subject` 照收**：采纳。CloudEvents 只要求非空；`dataschema` 拒绝只有空白是 .NET 跟着 `source` 的做法（ADR-0305），规范把它列为不作规定的写法。所有者的决定是「空的不合法」，`subject` 不再多收窄一层。
10. **`time`：空的 `ce-time` 头按文法读，与 `"time": ""` 同一个错误**：采纳。
    - 有这个头就交给 `TimestampText.TryParse`，空串不合文法，结果是 `MESSAGING_VALIDATION_002` + `messaging.envelope.invalid_time`（参数 `time` 为空串），与 structured 的 `"time": ""` 逐项相同。
    - 任务说明把三者都写成 `messaging.envelope.invalid`；`time` 不是字符串属性，信封的 `init` 收的是时刻，没有「交给 `init` 拒绝」这条路，而 structured 的 `"time": ""` 一直是 `invalid_time`。ADR-0323 决定 15 的要点是「binary 的空值与 structured 的空串同一条路」，照这一点，空的 `ce-time` 头应当与 `"time": ""` 同一个结果，不另给一个键。
11. **插件协议不改**：`Envelope.subject` 与 `Envelope.time` 的空串仍表示没有，理由同 ADR-0323 决定 16（proto3 分不出空串与没有）；规范在与 `dataschema` 同一处写明。
12. **向量**：`cloudevents-envelopes.json` 的 `invalid` 加「subject 为空」：采纳。期望值由 .NET 的 `CloudEventsFormat.Decode` 算出（改之前读得出，改之后以 `MESSAGING_VALIDATION_002` 拒绝），Go 一致性模块的 `rules.DecodeCloudEvent` 同步改，回放通过。binary 的空头与头值的编解码没有向量，理由同 ADR-0323 备选 9（binary 绑定在 `host` 档，只有 .NET 实现）。

从配置登记数据模式：

13. **数组形式的配置节 `XiHan:Messaging:DataSchemas`，每项 `Type` 与 `DataSchema`**：采纳，照所有者的决定。
    - 节名照 Messaging 已有选项的写法（`XiHan:Messaging:Outbox`、`XiHan:Messaging:InstanceId`），成员名照 `AddMessageDataSchema` 的参数名；环境变量写成 `XiHan__Messaging__DataSchemas__0__Type` 与 `XiHan__Messaging__DataSchemas__0__DataSchema`。
    - 数组的下标只用来分项，不表示先后；`type` 不进键名，点号写得进去。
14. **读进 `MessagingOptions.DataSchemas`，由 `MessagingRegistration.Configure` 登记进同一张表**：采纳。
    - `MessagingOptions` 是消息子系统的配置节 `XiHan:Messaging` 的选项，已经由每个消息 Provider 的特性经 `MessagingRegistration.Configure` 读一次；在这里登记，配置的模式与代码登记的在装配期进同一张 `MessageDataSchemas`，三个发布点照旧只查这张表。
    - ADR-0323 备选 19 否决的是「把表搬进 `MessagingOptions`、三处改读选项」，以及字典形式的键写不进环境变量；这里选项只承载配置读到的值，表仍是 `MessageDataSchemas`，配置是数组。
    - `MessagingOptions` 是 `Stable`，新增成员是只增；缺省是共享的空字典，没有配置时选项照旧与缺省相等（`FromConfiguration` 的既有用例比较选项相等）。
    - 没有配置时不建表：容器里没有 `MessageDataSchemas` 时三类信封与以前完全相同（ADR-0323）。
15. **另起一个选项类型（`ConfigurationSection = "XiHan:Messaging:DataSchemas"`）**：否决。多一个公开类型、多一处读配置的入口，而它读的是 `XiHan:Messaging` 下的一个节，与这个节的选项分开读没有好处。
16. **值的规则、重复与冲突照 `AddMessageDataSchema`**：采纳，校验抽成 `MessageDataSchemas.Validate` 一处，代码登记、选项的 `init` 与读配置共用：
    - 每项的 `Type` 与 `DataSchema` 都要有，缺一个（含写成单个值而不是两个成员）、为空或只有空白、有孤立的代理项都是装配错误。
    - 配置里两项是同一个 `type` 同一个值只记一次，值不同即失败，消息指出两项的配置键。
    - 配置与代码登记了同一个 `type`：值相同只记一次，值不同即失败，不论谁先登记（Provider 装配时代码已登记的，`Configure` 抛出；Provider 装配之后代码再登记的，`AddMessageDataSchema` 抛出），不取先登记的、也不取后登记的，配置不悄悄顶掉代码。
    - 容量照旧 10000 个 `type`，配置的与代码的合计。
17. **失败的异常是 `ArgumentException`，消息带配置键**：采纳。
    - 任务说明写的是「`FrameworkException`」，又说照 `AddMessageDataSchema` 现有的重复与校验规则；现有的规则是 `ArgumentException`（ADR-0323 备选 25：装配期的编程错误，照 `AddAuditDetail` 抛参数异常，不新增错误码），`MessagingOptions.FromConfiguration` 对不合法的 `InstanceId` 也是 `ArgumentException`。两处现有做法一致，这里照它们。
    - 两种都是装配期抛出、启动即失败，没有静默覆盖；要改成 `FrameworkException` 就得给它一个错误码，`MESSAGING_VALIDATION_004` 是领域事件登记专属的，新增一个码只为区分配置来源没有收益。
    - 与代码登记冲突时，`Configure` 把 `Add` 的 `ArgumentException` 包一层，消息开头写明是 `XiHan:Messaging:DataSchemas` 里的哪个 `type`，原异常作 `InnerException`。

其他：

18. **规范的档位**：出站编码只让写出的值多编几个字符，按 CloudEvents 解码的接收方读到的值不变；配置登记只增。空的 `subject` 不合法、坏的 quoted-string 读不出是收窄输入，记 MAJOR（`v1.0.0` 之前允许），同 ADR-0323。

## 决策

1. **规范**：照「状态」。
2. **`CloudEventsHttpBinding`**：
   - `EncodeHeaderValue`：U+0021–U+007E 之中除 `%` 与 `"` 以外的原样，其余按 UTF-8 字节百分号编码。
   - `DecodeHeaderValue`：先 `UnquoteOf`（private）解开 quoted-string，坏的即无值；再做原有的一轮百分号解码。
   - `ReadBinary`：`ce-subject` 有这个头就交给信封，`ce-time` 有这个头就按文法读。
3. **`MessageEnvelope.Subject`**：`init` 拒绝空串（`ArgumentException`），只有空白的照收。
4. **数据模式的配置**：`MessagingOptions.DataSchemas`（`IReadOnlyDictionary<string, string>`，缺省空），`FromConfiguration` 读 `DataSchemas` 数组；`MessagingRegistration.Configure` 逐项 `MessageDataSchemas.Add`；校验抽成 internal 的 `MessageDataSchemas.Validate`。

## 理由

- 头值的编解码照 CloudEvents 自己的规定，写出的一方编得完整、读入的一方解得完整，两端不论哪个版本都读到同一个值；读不出就是发送方写错，不悄悄留下引号或换一种读法。
- 空的 `subject` 在信封上就构造不出来，读、写与各种内容模式只有一条规则；`time` 的空值与 structured 的空串同一个结果，不为同一个写错的值造两个错误。
- 配置的数据模式进同一张表、同一套规则，代码与配置分歧即失败，部署只改配置也能带上模式，又不会让配置悄悄改掉代码里的登记。

## 影响

- **公开 API**（`PublicAPI.Unshipped.txt` 已同步）：新增 `MessagingOptions.DataSchemas`（get / init）。`MessageEnvelope.Subject`、`EncodeHeaderValue`、`DecodeHeaderValue` 签名不变，行为见下。`MessageDataSchemas.Validate` 与 `CloudEventsHttpBinding.UnquoteOf` 不公开。没有新的错误码与消息键。
- **配置键**：新增 `XiHan:Messaging:DataSchemas:{index}:Type` 与 `XiHan:Messaging:DataSchemas:{index}:DataSchema`；`Cli/Knowledge/configuration.json` 已补，`Update-CliKnowledge.ps1` 已跑（`catalog.json` 不变）。
- **行为变化（更新日志，升级须知）**：
  - CloudEvents Webhook 以 binary（缺省）发布时，头值里的空格写成 `%20`、双引号写成 `%22`；以前原样写出。把头值当原文、不做百分号解码的接收方要改成按 CloudEvents 解码。
  - CloudEvents 入站 binary：双引号括起的头值先解开（`"order 42"` 读成 `order 42`）；以前引号读成值的一部分。引号不配对（如 `a"b`）、引号里有控制字符或反斜杠在末尾的头值以 400 `MESSAGING_VALIDATION_002`（`protocols.cloudevents.header_invalid`）拒绝；以前照原样读出。
  - CloudEvents 入站：binary 的空 `ce-subject` 头与 structured / batch 的 `"subject": ""` 以 400 `MESSAGING_VALIDATION_002` 拒绝；以前当作没有或读成空串。binary 的空 `ce-time` 头以 400 `MESSAGING_VALIDATION_002`（`messaging.envelope.invalid_time`）拒绝；以前当作没有。
  - `MessageEnvelope { Subject = "" }` 抛出 `ArgumentException`；以前照收。领域事件的 `subject` 选择器返回空串的改为返回 `null`。各代理与发件箱里以前写进去的 `"subject": ""` 的消息读不出，按「无法解码的消息」了结（竞争订阅进死信、广播订阅丢弃，RFC-0009）。
  - 新能力：配置节 `XiHan:Messaging:DataSchemas` 登记框架事件的数据模式；与代码登记的同一个 `type` 值不同时启动失败。
- **规范**（规范工作区已提交）：见「状态」，MAJOR（`v1.0.0` 之前允许）。
- **Go 一致性模块**：`rules.DecodeCloudEvent` 拒绝空的 `subject`，回放新增的向量。
- **六个 SDK**：不用跟。SDK 不实现 CloudEvents 的 HTTP 绑定与结构化 JSON，事件经插件协议的 `Envelope`（proto3）收发，`subject` 与 `time` 的空串仍是没有；各 SDK 也不回放 `messaging/cloudevents-envelopes.json`（只有 .NET 与 Go 一致性模块回放）。Go SDK 升规范指针时把 conformance 模块升到同一提交即可，SDK 本身不用改。
- **测试**：
  - `CloudEventsHttpBindingTests`：出站头值的空格写成 `%20`（`ce-subject`、`ce-dataschema`、代理对），`EncodeHeaderValue` 的六组（含 CloudEvents 规范自己的 `Euro € 😀` 例子、双引号、U+007F 与制表符、不编码的可打印 ASCII 全集），编出的值不含空格与双引号、解码回原值；空的 `ce-subject` 与 `ce-time` 头与 structured 的空串逐项同一个错误；只有空白的 `subject` 照常读出；信封不收空的 `subject`。原来锁定「空格原样」的三处断言改了。
  - `CloudEventsBinaryHeaderTests`：解开引号的十四组（整个值括起、空的引号、转义的引号与反斜杠、相连的几段、引号之外的反斜杠、引号里的百分号只解一轮、制表符、非 ASCII）；坏的 quoted-string 九组都无值；引号里的孤立代理项无值；入站读出引号括起的 `ce-subject` 与扩展属性，坏的是 `header_invalid`、参数 `ce-subject`。
  - `CloudEventsEnvelopeVectorTests`：回放新增的「subject 为空」。
  - `MessageDataSchemaConfigurationTests`（新）：读出数组的每一项；没有这个节时为空、选项与缺省相等；缺成员、写成单个值、为空或只有空白、孤立的代理项都抛出、消息带配置键；同值重复只记一次、异值抛出并指出两项；`init` 同样核对。
  - `DataSchemaConfigurationTests`（新，`Messaging.InMemory.Tests`）：配置的与代码的进同一张表；都没有时容器里没有这张表；代码先登记另一个值时启动失败，Provider 装配之后代码再登记另一个值时抛出；配置项缺成员时启动失败、消息带配置键。
  - 改实现之前这些用例都失败（编不过的新公开面除外），读得出、照常的几条除外。
- **文档**：`Protocols.CloudEvents` 与 `Messaging.Abstractions` 的 README 已同步；文档站与更新日志待改。
