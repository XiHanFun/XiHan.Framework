# ADR-0305 信封承载 CloudEvents 的 dataschema

## 状态

已采纳（规范 RFC-0009「信封」、`protocol/cloudevents.md`、`plugin/plugin.proto`（`Envelope.data_schema`，字段 11）与 `plugin/invocation.md` §4 已修订，`messaging/cloudevents-envelopes.json` 已补，Go 一致性模块的 `DecodeCloudEvent` 读出 `DataSchema`，`conformance/internal/pluginv1` 已重新生成；`MessageEnvelope` 新增 `DataSchema`，结构化 JSON、CloudEvents 协议绑定与插件协议的映射双向承载它；六个 SDK 待跟）。

## 日期

2026-11-25

## 背景

- CloudEvents 1.0 的规范属性里有一个可选的 `dataschema`：数据遵循的模式，取值是 URI。框架的信封一直没有这个属性。ADR-0298 把它列进扩展属性的保留名时记下了这一处（备选 3「这次不做」、「没有解决的」）：要承载它得同时改 `MessageEnvelope`、`plugin.proto` 的 `Envelope` 与六个 SDK。
- ADR-0298 之后带 `dataschema` 的标准事件读不出：它按 RFC-0009「信封」第 5 条是扩展属性，名字是保留名，整个信封不合法。CloudEvents 入站端点返回 400 `MESSAGING_VALIDATION_002`，远程代理上按无法解码的消息了结（竞争订阅进死信，广播订阅丢弃）。在那之前 .NET 把它读成名为 `dataschema` 的扩展属性、写出时照原样写回：线上的写法碰巧对称，信封却把一个规范属性当扩展属性。
- 仓库所有者已拍板：信封承载 `dataschema`，标准事件能原样收发，规范记 MINOR；取值的文法跟规范里已有的 URI 类属性（`source`）对齐，不另起一套。
- 规范对 `source` 的要求只有「必填、不为空字符串」（「信封」第 2 条）。.NET 的 `MessageEnvelope.Source` 拒绝 `null`、空串与只有空白（`IsNullOrWhiteSpace`），`CloudEventsFormat.Decode` 与 binary 模式同样只核对非空，Go 一致性模块的 `required` 只核对非空串；哪一处都不核对 URI 语法，只有空白的 `source` 列在「向量不收」的单子上。
- 承载信封的地方：
  - 结构化 JSON：`CloudEventDocument`（`internal`）与 `CloudEventsFormat`。RabbitMQ 的消息体、Kafka 的消息值、Redis 条目的 `envelope` 字段、发件箱行的 `Payload` 与 CloudEvents 协议绑定的 structured / batch 都是它，三家远程代理的消息头里只有内容类型与尝试次数，没有 `ce-*` 头。
  - 内存代理传递信封对象本身。
  - CloudEvents 协议绑定的 binary 模式：`CloudEventsHttpBinding` 读写 `ce-*` 头，`_coreAttributes` 之外的 `ce-*` 都当扩展属性。
  - 插件协议：`PluginEnvelopeMapper`（`Host.Publish`、Wasm 的 `publish` 导入与投递共用），`plugin.proto` 的 `Envelope` 是字段 1 到 10，Wasm 载体是它的 proto3 JSON。

## 备选

1. **`DataSchema` 的类型用 `Uri?`**：否决。`Source` 是字符串；`Uri` 要先解析，绝对与相对引用要选 `UriKind`，`ToString()` 还会改写原文（反转义、补斜杠、主机名小写），读进来再写出去就不是原样了，与「不另起一套」相悖。
2. **按 RFC 3986 核对 URI 语法，或照 CloudEvents 的字面要求绝对 URI**：否决。`source` 不核对，各语言的 URI 解析器宽严不一（.NET 的 `System.Uri`、Go 的 `net/url` 收的写法不同），向量只能收它们都认的那一部分；读取一侧拒绝发送方写错的模式地址，只是让一条本来能处理的事件进死信。绝对 URI 由写出的一方负责，框架不按它取模式。
3. **只有空白的值写成不合法**：不写。`source` 只有空白不作规定（「向量不收」的单子上），`dataschema` 与它对齐，一并列进那张单子；.NET 照 `Source` 的做法在 `init` 里拒绝。
4. **空串怎么处理**：结构化 JSON 的 `"dataschema":""` 不合法，与 `source` 为空相同；binary 模式的 `ce-dataschema` 头值为空与没有这个头相同，与 .NET 对 `ce-subject`、`ce-time` 的读法相同；插件协议的空串是没有，proto3 的字符串分不出空串与没有，与 `subject`、`time` 相同。三处都不会把空串交给信封。
5. **空串用新的消息键（如 `messaging.envelope.invalid_dataschema`）**：否决。`datacontenttype` 为空走的是信封 `init` 抛出、`Decode` 转成 `messaging.envelope.invalid`（参数 `reason`）这条路，可选属性的空串照它；向量只比错误码。代价是 `reason` 是一句只有 .NET 写得出的英文，与 `datacontenttype` 为空时一样。
6. **把 `dataschema` 从保留名里拿掉**：否决。它现在是规范属性，再让它当扩展属性名，写出来就是两个同名的成员；保留名是规范属性名与结构化 JSON 成员名的清单，不随信封承载哪些属性变化（ADR-0298 决策 1）。
7. **`plugin.proto` 用 `optional string data_schema`**：否决。空串不是合法的值，不需要区分「空串」与「没有」；`optional` 让各语言生成出 `has_` 一类的访问器，SDK 的映射写法与 `subject`、`time` 不一样。
8. **新的协议版本**：否决。新增字段在线上兼容（`buf breaking` 通过），RFC-0024「版本」第 4 条：同一协议版本里的增补走规范的 MINOR，不升版本、不在握手里协商。代价：按较早的规范实现的宿主不认这个字段，插件发布到那样的宿主上的信封丢掉 `dataschema`、其余照常；较早的插件收到的信封没有它。两种都不让互通失败，`dataschema` 是元数据。
9. **规范记 MAJOR**：否决。相对 ADR-0298（还在「未发布」里）这是撤回一次拒绝；相对上一个规范标签，带 `dataschema` 的文档那时读得出（读成扩展属性，线上写法与现在相同），现在照收，读取一侧收得下较早 MINOR 写出的每一种形式，按 RFC-0024 第 3 条是 MINOR。
10. **按 `dataschema` 取模式、校验数据**：不做。超出信封的职责，应用需要时在处理器里自己做。
11. **Kafka、RabbitMQ 另写 `ce_dataschema` 一类的消息头**（CloudEvents 各传输绑定的 binary 模式）：否决。框架的三家代理只用结构化 JSON，别的属性都不进头，只给 `dataschema` 开一个头没有意义。

## 决策

1. **规范**（RFC-0009「信封」）：`dataschema` 是可选的规范属性。出现时必须是字符串、不能是空字符串；与 `source` 一样不核对 URI 语法、不要求绝对 URI，读出的值照原样，不规范化、不解析。带它的文档不再不合法：第 2 条写明空串与不是字符串的不合法，第 5 条写明它不落进扩展属性；写出时有则写成同名的字符串成员、没有则不写。另写明各传输上的映射（见下）与「保留名不随承载变化」。
2. **各传输**：内存代理传递信封本身；RabbitMQ、Kafka、Redis 与发件箱都经 `CloudEventsFormat`，`dataschema` 是结构化 JSON 的顶层成员，代理的消息头里不另写，三家代理的代码不用改；CloudEvents 协议绑定 structured 与 batch 同样是这个成员，binary 模式是 `ce-dataschema` 头（入站按百分号解码，值为空同没有；出站有则写、按规范百分号编码）；插件协议是 `Envelope.data_schema`。
3. **`MessageEnvelope.DataSchema`**（`string?`，`init`）：`null` 是没有；空串与只有空白抛出 `ArgumentException`（`The data schema must not be empty.`），与 `Source` 同一种写法。
4. **`CloudEventsFormat`**：`Encode` 有则写 `dataschema` 成员；`Decode` 读成 `DataSchema`。空串经 `init` 转成 `MESSAGING_VALIDATION_002` + `messaging.envelope.invalid`（备选 5），不是字符串的与别的规范属性类型不对一样是 `messaging.envelope.invalid_json`（`reason` 为 `$.dataschema`）。
5. **`CloudEventsHttpBinding`**：`dataschema` 进 `_coreAttributes`，`ce-dataschema` 不再当扩展属性；`ce-data` 仍因保留名不合法。
6. **插件协议**：`plugin.proto` 的 `Envelope` 新增 `string data_schema = 11;`，不复用、不改已有字段，协议版本仍是 `1`。`PluginEnvelopeMapper.ToProto` 有则照原样写、没有写空串；`FromProto` 空串是没有、其余照原样，只有空白的经 `init` 以 `messaging.envelope.invalid` 让那一项不发布（与 `source` 只有空白时相同，`invocation.md` §5「信封能否转换」那一步）。Wasm 载体的 JSON 字段名是 `dataSchema`。SDK 不在本地核对，原样写出与交出。
7. **保留名不变**：`EnvelopeExtensions.ReservedNames` 仍含 `dataschema`，`IsValidName("dataschema")` 仍为假，`WithExtension("dataschema", …)` 仍抛出；它只能作为规范属性出现。
8. **向量**（`messaging/cloudevents-envelopes.json`，期望值由 .NET 算出）：`valid` 的每个 `expected` 加 `dataschema`（`null` 为没有）；「全部属性与框架扩展」带上 `dataschema`；ADR-0298 加进 `invalid` 的那份带 `dataschema` 的文档移进 `valid`；另加 URN 与相对引用两份（后者写明不核对 URI 语法）；`invalid` 加空串与数字两份。写出方向沿用这个文件的约定：把 `expected` 编码，`dataschema` 有则是同名的字符串成员、没有则不写，再解码回同一个信封。不新增向量文件，插件协议的映射是恒等的（空串为没有），不另立向量。

## 理由

- 信封说自己是 CloudEvents 1.0 事件，就该收得下标准事件的全部规范属性；ADR-0298 让带 `dataschema` 的标准事件进死信，是为了不把规范属性当扩展属性，代价落在了发送方身上。承载它之后两头都对了。
- 与 `source` 同一条规则，各语言只需要「非空字符串」这一种判断，向量写得出、各实现读得一样；URI 语法的宽严留给写出方。
- 插件协议加字段而不升版本，较早的对端照常互通；丢掉的只是一个可选的元数据，不会让投递或发布失败。

## 影响

- **公开 API**（`PublicAPI.Unshipped.txt` 已同步）：新增 `MessageEnvelope.DataSchema`（`get` / `init`）。`MessageEnvelope` 是 `Stable`：给密封记录加一个可选的 `init` 成员不在 RFC-0001「什么算破坏」之列（没有删除或改签名，也不是给使用者要实现的接口加抽象成员），而且 5.0 还没有发布（`PublicAPI.Shipped.txt` 是空的）。记录生成的相等比较、`with` 与 `ToString` 随之带上它：只差 `DataSchema` 的两个信封不再相等。`CloudEventsFormat.Decode`（`Stable`）收下了 ADR-0298 时拒绝的文档。没有新的错误码与消息键。
- **行为变化（更新日志）**：
  - 带 `dataschema` 的 CloudEvents 事件（structured / batch 的成员、binary 的 `ce-dataschema` 头、插件发布的 `Envelope.data_schema`）又能收下，读成 `MessageEnvelope.DataSchema`；CloudEvents 入站端点不再为它返回 400，远程代理上不再按无法解码进死信。`ce-data` 头仍是 400。
  - `dataschema` 为空串或不是字符串的结构化 JSON 文档不合法（`MESSAGING_VALIDATION_002`）。
  - 出站：信封有 `DataSchema` 时，结构化 JSON 写 `dataschema` 成员，Webhook 的 binary 模式写 `ce-dataschema` 头，宿主写给插件的信封带 `data_schema`。
  - 迁移：ADR-0298 之前读过 `Extension("dataschema")` 的代码改读 `DataSchema`；照 ADR-0298 的建议把数据模式放进自己扩展属性的，可以改用 `DataSchema`（不改也照常工作）。
  - 升级前已在队列、流、主题或发件箱表里的消息：都是结构化 JSON，带 `dataschema` 的升级后照常投递。
- **规范**（规范工作区已提交，指针待更新）：RFC-0009「信封」（开头的属性表、`dataschema` 的规则一段、第 2 条、第 5 条、「写出」、新增「各传输上的 `dataschema`」一段、保留名一段、「一致性」的套件用例与向量表、「向量不收」的单子）与状态行；`protocol/cloudevents.md` 的信封映射、binary 入站、出站；`plugin/plugin.proto` 与 `plugin/invocation.md` §4；RFC-0017 状态行。MINOR（备选 9）。`buf lint`、`buf format`、`buf breaking --against origin/dev` 通过。
- **Go 一致性模块**：`rules.CloudEvent` 新增 `DataSchema *string`，`DecodeCloudEvent` 读出它并拒绝空串与非字符串；`conformance.CloudEventExpectation` 新增 `DataSchema`；`pluginv1.Envelope` 新增 `DataSchema` / `GetDataSchema()`。导出面只增。`vectors_subsystems_test.go` 核对向量里读得出的有带与不带 `dataschema` 的两种。运行器用例不变。
- **六个 SDK**（下一波）：`spec/` 升到这个规范提交；从新的 `plugin.proto` 重新生成协议代码（PHP 与 Rust 的 CI 核对生成代码与 `plugin.proto` 一致，不重新生成就失败）；SDK 的信封类型加 `dataschema` 成员（没有时为空），投递时把 `data_schema` 原样交给作者（空串为没有），发布时原样写出（没有写空串），不在本地核对；Go SDK 另把 conformance 模块升到同一提交。SDK 只认领 `core` 与 `plugin` 档，这次没有新的向量文件，向量覆盖测试不受影响。
- **测试**：
  - `CloudEventsEnvelopeVectorTests` 回放 `dataschema`（读出、编码时成员有无、往返），并核对向量里有带与不带、空串与非字符串几类。
  - `MessageContractTests`：`DataSchema` 可选、照原样、空串与只有空白抛出、仍不能当扩展名；`CloudEventsFormat` 写出与不写、读回不进扩展属性、空串与数字两种错误。
  - `CloudEventsHttpBindingTests`：binary 读 `ce-dataschema`（百分号解码、不进扩展属性）、空值是没有、只有空白不合法、`ce-data` 仍被拒；binary 写 `ce-dataschema`、没有时不写；structured 往返（`EnvelopeAssert` 比较 `DataSchema`）。
  - `PluginEnvelopeDataSchemaTests`（新）：`ToProto` / `FromProto` 双向、空串为没有、只有空白与 `source` 同样被拒、`PluginPublishBatches.Collect` 只让那一项失败、Wasm JSON 字段名 `dataSchema`。
  - `KafkaDataSchemaTests`（新，打桩的生产者与消费者，不需要 Kafka）：发布写进消息值、不进头；投递把自己写的与外部标准事件里的 `dataschema` 交给处理器（ADR-0298 时后者进死信）；死信保留它。
  - `MessageBrokerConformance` 用例 1 与用例 15 的信封带 `dataschema` 并核对：内存代理本机跑，RabbitMQ、Redis 由 CI 的真实服务跑，Kafka 的门控用例哪里都不跑，由上一条兜住。
- **文档**：`Messaging.Abstractions`、`Plugins.OutOfProcess`、`Plugins.Wasm` 的 README 已同步；文档站的 `messaging/pubsub`、`packages/messaging-abstractions`、`packages/protocols-cloudevents`、`protocols/cloudevents`、`plugins/out-of-process`、`plugins/wasm`、`appendix/spec` 与更新日志待改。

## 没有解决的

- 领域事件的登记（`AddDomainEvent`）没有给 `dataschema` 的选择器，调度与工作流发布的框架事件也不带它；要带的应用目前只能自己构造信封发布。
- 按较早的规范实现的宿主收到插件发布的 `data_schema` 时静默丢掉它（备选 8）：插件与宿主的规范版本不一致时，数据模式不会到达处理器，也没有日志。
- .NET 的 `Plugins.Conformance` 套件与 Go 运行器都不核对插件一侧有没有把 `data_schema` 交给作者：插件的处理器不回显信封，运行器看不到；SDK 的单元测试自己核对。
