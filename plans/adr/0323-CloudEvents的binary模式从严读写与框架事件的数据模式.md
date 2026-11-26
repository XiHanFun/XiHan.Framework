# ADR-0323 CloudEvents 的 binary 模式从严读写与框架事件的数据模式

## 状态

已采纳。

- 规范：
  - `protocol/cloudevents.md`：信封映射写明三种模式里空的 `dataschema` 都不合法、插件协议的空串是另一种约定；入站 binary 一行与「信封不合法」一条已改，新增「binary 的头值」与「binary 声明为 JSON 的数据」两条；「出站」补数据的核对。
  - RFC-0009：「信封」的写出与「各传输上的 `dataschema`」已改，新增「框架事件的数据模式」一节，「领域事件」第 2 条补 `dataschema`。
  - RFC-0013「引擎」的事件、RFC-0021「实例与执行」的事件补 `dataschema`。
  - RFC-0009、RFC-0013、RFC-0021 的状态行已改。
  - 向量、错误码与配置键不变。
- .NET：
  - `Messaging.Abstractions`：`CloudEventsFormat.Encode` 拒收 JSON 数据字符串里不合法的 UTF-8；新增 `MessageDataSchemas` 与 `AddMessageDataSchema`（`Preview`）。
  - `Protocols.CloudEvents`：`CloudEventsHttpBinding` 的 binary 两个方向核对声明为 JSON 的数据，头值严格解码，`DecodeHeaderValue` 改返回 `Maybe<string>`（`Preview` 的破坏，ADR-0131），空的 `ce-dataschema` 头不合法；新增消息键 `protocols.cloudevents.header_invalid`。
  - 领域事件的派发器、调度的事件发布与工作流的事件发布（含 `PublishEvent` 活动）带上登记的数据模式。
- 修订 ADR-0305 备选 4 与 ADR-0318 备选 5，两份末尾各加一节。

## 日期

2026-11-27

## 背景

ADR-0305 与 ADR-0318 留下四件，仓库所有者已拍板，都做：

1. **binary 的数据体不过 JSON 文本的检查**（ADR-0318 备选 5）：
   - 出站：`CloudEventsHttpBinding.Write` 的 binary 把数据原样作请求体，声明为 JSON 的数据有重复成员、嵌套过深或干脆不是 JSON 照发；structured 经 `CloudEventsFormat.Encode` 拒绝。Webhook 缺省就是 binary。
   - 入站：`ReadBinary` 同样原样收下。派发模式交给处理器，处理器拿到的是 structured 读不进的数据；发布模式交给代理，RabbitMQ、Kafka、Redis 与发件箱写结构化 JSON 时 `Encode` 抛出。
   - 这一轮实测 `Encode` 本身还有一处缺口：JSON 数据的字符串与成员名里有不合法的 UTF-8 字节（编码成 UTF-8 的代理项 `ED A0 80`、截断的 `C3`、`FF`、过长编码 `C0 A0`）时不报错，写出器把它们换成转义的 U+FFFD，文档过得了写出之后的检查，处理器读到的是另一份数据。`JsonDocument.Parse` 不核对字符串里的字节，转码发生在写出时。不成对的代理项转义 `Encode` 照常拒绝（写出之后的检查）。`Encode` 的 XML 文档只列了后者。
2. **binary 入站宽松地读百分号序列**（ADR-0318「发现而没改的」）：`DecodeHeaderValue` 用 `Uri.UnescapeDataString`，实测 `%ED%A0%80`、结尾截断的 `%C3`、`%zz` 与单独的 `%` 都原样留成百分号文本，照常读出。CloudEvents 1.0.2 HTTP 协议绑定 §3.1.3.2 要求解码出的字节不是合法的 UTF-8 时拒绝。
3. **空的 `dataschema` 两种读法不对称**（ADR-0305 备选 4）：结构化 JSON 的 `"dataschema": ""` 不合法，binary 的空 `ce-dataschema` 头等于没有。插件协议 `Envelope.data_schema` 的空串表示没有，是 proto3 的约定，不在这次改动之内。
4. **框架事件设不了 `dataschema`**（ADR-0305「没有解决的」第 1 条）。三处发布框架事件：
   - 领域事件：`DomainEventDispatcher` 经 `DomainEventRegistration.ToEnvelope` 构造信封，每种 CLR 事件类型登记一个 CloudEvents `type`。
   - 调度：`JobSchedulerEngine` 每次在新作用域里建 `SchedulingEventPublisher`，9 个主题共用 `SchedulingEventData`，`type` 就是主题。
   - 工作流：引擎、任务服务与外部任务服务各建一个 `WorkflowEventPublisher`，`PublishEvent` 活动自己构造 `workflow.custom` 的信封；26 个主题共用 `WorkflowEventData`，`type` 就是主题。

## 备选

binary 的数据：

1. **出站照 `Encode` 写一遍、结果丢掉**：采纳。
   - 属性与数据的核对、先后与异常消息都与 structured 逐字相同，只有一份实现。
   - 代价是声明为 JSON 的数据多序列化一遍；一次 Webhook 发布是一次 HTTP 往返，相比之下可以忽略。
   - 不是 JSON 的数据不写这一遍，只查属性里的孤立代理项（ADR-0318 的做法）。
2. **出站另写一份检查**（数据包进 `{"data":…}` 过 `JsonText.FindProblem`）：否决。异常消息要与 `Encode` 对齐就得照抄它的三句，两份会走样。
3. **入站照 structured 读同一个事件**：采纳。
   - 数据包进 `{"data":…}` 再过 `JsonText.FindProblem`：层数（数据最多 63 层）、语法错误优先于重复成员、重复成员的路径（`$.data.order.id`）都与 structured 读同一个事件时相同。
   - 结果是 `MESSAGING_VALIDATION_002` + `messaging.envelope.invalid_json`，`reason` 是位置，与 `CloudEventsFormat.Decode` 相同；端点照旧写成 400 的 Problem Details。
   - 包进去之后合法、单独却不是一个 JSON 值的文本（`1,"other":2`）另用读取器读一遍数据，按语法错误给 `$`。
   - 只在声明为 JSON（`MessageEnvelope.IsJsonData`，`Content-Type` 缺省时是 `application/json`）且请求体不空时查；包装用的缓冲区从 `ArrayPool` 租。
4. **入站调 `Encode` 判定**：否决。失败是异常、消息是英文句子，与 structured 读的错误（`invalid_json` + 位置）对不上，还要多写一遍。
5. **入站不查，只做出站**：否决。structured 读写都查；binary 只查一个方向，收下的数据在发布模式下远程代理写不出，在派发模式下处理器拿到 structured 读不进的数据。
6. **只查数据本身，不包一层**：否决。64 层的数据单独看合法，嵌进文档是 65 层；重复成员的路径少了 `.data`；嵌套与重复成员同时出现时先报哪个也与 structured 不同。
7. **`Encode` 补查不合法的 UTF-8**：采纳。
   - 写出的文本里有转义的 U+FFFD 才回头用 `JsonText.FindProblem` 查原始数据，只认 `InvalidUtf8` 一种；本来就是 U+FFFD 的字符多查一遍、照常写出。
   - 依据 ADR-0318「修订（2026-11-26）」第 2 条实测的事实：内置编码器换出来的 U+FFFD 总写成转义。属性已经在开头严格核对过，换出来的只可能在数据里。
   - 不补的话，binary 出站照 `Encode` 核对也拦不住：binary 原样发出接收方会拒绝的字节，structured 发出另一份数据。
8. **`Encode` 每次都先查原始数据**：否决。每次发布多读一遍数据，只为一种少见的输入。
9. **补向量**：否决。binary 绑定没有向量：它在 `host` 档，只有 .NET 实现，SDK 不实现 CloudEvents 的 HTTP 绑定。`Encode` 拒收不合法的字节是写出一侧的核对，`cloudevents-envelopes.json` 只收读取与往返，不合法的 UTF-8 也写不进 JSON 向量。

头值的解码：

10. **照 CloudEvents HTTP 协议绑定的百分号解码**：采纳。
    - 每个 `ce-*` 头的值逐个字符换成字节，`%` 与紧跟的两位十六进制数字（大小写都收）是一个字节，其余字符是它的 UTF-8 编码，再按 UTF-8 严格解码（`Utf8.IsValid`）。
    - 不必要的编码照收（`%41` 是 `A`）。
    - 值里直接有孤立的代理项（经测试宿主或放开了请求头编码的服务器才进得来）编不成 UTF-8，同样不合法。
11. **`%` 后不是两位十六进制数字时照原样留下，只拒不合法的 UTF-8**：否决。CloudEvents 没写这一种，但它要求发送方把 `%` 编成 `%25`；留下就是同一个值有两种读法（`%25` 与 `%`），没编的 `%` 就是写错的值。
12. **失败是信封不合法，新消息键**：采纳。
    - 错误码 `MESSAGING_VALIDATION_002`，与 binary 其余的不合法一样，端点照旧 400。
    - 消息键 `protocols.cloudevents.header_invalid`，参数 `header` 是头名的小写，10 个区域都补。
    - 备选：沿用 `messaging.envelope.invalid`。否决：它的 `reason` 只能是一句写在代码里的英文。
13. **`DecodeHeaderValue` 改返回 `Maybe<string>`**：采纳。
    - 它是公开的辅助，解码的是网络上来的值，可恢复的失败不抛异常；与同一个类里的 `ModeOf` 一样用 `Maybe`。
    - `CloudEventsHttpBinding` 是 `Preview`，照 ADR-0131：5.0.0 之前没有包验证基线，`PublicAPI.Unshipped.txt` 直接改，迁移方式进更新日志。
    - 备选：签名不变、不合法时抛 `FormatException`。否决：调用方读的是网络输入，每处都得 `try`。
    - 备选：保留宽松的旧方法、另加严格的。否决：推测性的兼容别名。
14. **头值的先后**：`ce-specversion`、`ce-id`、`ce-source`、`ce-type`、`ce-time`、`ce-subject`、`ce-dataschema`、`ce-datacontenttype`，再按请求里的顺序读扩展属性，第一个读不出的报错。规范只要求报错，不规定先后。

空的 `dataschema`：

15. **binary 的空 `ce-dataschema` 头不合法**：采纳，照所有者的决定。空值交给信封的 `init` 抛出，转成 `MESSAGING_VALIDATION_002` + `messaging.envelope.invalid`，与 structured 的空串同一条路（ADR-0305 备选 5）；只有空白的照旧这样拒绝。
16. **插件协议的空串不改**：proto3 的字符串字段分不出空串与没有，与 `subject`、`time` 同一个约定；HTTP 的头与 JSON 的成员分得出有没有，空值就是写错的值。规范在 RFC-0009「各传输上的 `dataschema`」与 `protocol/cloudevents.md` 的信封映射里写明两者的区别。
17. **`ce-subject`、`ce-time` 的空头**：不在这次决定之内，仍与没有这个头相同（见「没有解决的」）。

框架事件的数据模式：

18. **事件类型上的元数据：按 CloudEvents `type` 登记的一张表**：采纳。
    - `services.AddMessageDataSchema(type, dataSchema)` 填进 `MessageDataSchemas`（以实例注册为单例），三处发布点构造信封时按自己的 `type` 查它。
    - 一个公开面管三处：`type` 是三处共有的键。领域事件的 `type` 是 `AddDomainEvent` 登记的，调度与工作流的 `type` 是主题常量。
    - 粒度是一个 `type` 一个模式；共用一个数据类型的主题登记同一个值。
    - 只增：不改 `AddDomainEvent`（`Stable`）的签名，没有登记任何模式时 DI 里没有这张表，三处与以前完全相同。
19. **发布选项上的一个成员**：否决。
    - 三处没有共同的发布选项：领域事件没有选项，调度是 `SchedulerOptions`，工作流是 `WorkflowOptions`。每处加一个 `EventDataSchema` 就是三个成员、三个配置键，粒度还粗到整个子系统一个模式。
    - 放进 `MessagingOptions`（`Stable`）的一个字典，等于把同一张表搬进选项：调度与工作流要多解析一次选项；从配置读时 `type` 里的点号在环境变量的键里写不出（`XiHan__Messaging__DataSchemas__scheduling.run.failed`）。
20. **`AddDomainEvent` 加 `dataSchema` 参数**：否决。只管领域事件，调度与工作流仍没有。
21. **由发布者（`DirectPublisher`、`OutboxPublisher`）按 `type` 补**：否决。它们只在调用内补扩展，调度在后台循环里发布；而且会作用到应用自己构造的信封，改的是 RFC-0009「发布侧补齐」的范围。
22. **事件 CLR 类型上的特性**：否决。要扫描与反射；调度与工作流的事件没有各自的 CLR 类型。
23. **从配置读**：这次不做，公开面取最小的；以后要加是只增。
24. **值的核对**：沿用 RFC-0009「信封」已有的 `dataschema` 规则（非空字符串，不核对 URI 语法；.NET 照 `MessageEnvelope.DataSchema` 也拒只有空白的），另拒孤立的代理项：采纳。
    - 任务要求「过 RFC-0009 已有的 URI 文法核对」。RFC-0009 没有 URI 文法：ADR-0305 备选 2 否决了按 RFC 3986 核对，「信封」写明不核对 URI 语法、绝对 URI 由写出的一方负责。已有的只有这一条非空规则，这里照它，不另起一套。
    - 孤立的代理项写不进结构化 JSON 与 binary 的头，登记时就拒，免得每次发布都失败、只记一条日志。
    - `type` 照 `MessageEnvelope.Type`：不能为空或只有空白，另拒孤立的代理项。
25. **重复与容量**：采纳。
    - 同一个 `type` 再登记同一个值只记一次；登记另一个值抛出 `ArgumentException`，不取先登记的也不取后登记的。
    - 10000 个 `type` 的上限（同 `DomainEventRegistry`），满了再登记新的 `type` 抛出 `InvalidOperationException`。
    - 不新增错误码：这是装配期的编程错误，照 `AddAuditDetail` 抛参数异常；`MESSAGING_VALIDATION_004` 是领域事件登记专属的码。
26. **查表的时机**：构造每个信封时查一次（`ConcurrentDictionary`，按序数比较）：采纳。登记在装配期写入，发布在运行时并发读取。

## 决策

1. **规范**：
   - `protocol/cloudevents.md`：binary 的头值按 CloudEvents 的百分号解码严格读，`%` 后不是两位十六进制数字、字节不是合法的 UTF-8、值里有孤立的代理项都是信封不合法（`protocols.cloudevents.header_invalid`）；声明为 JSON 的数据照 structured 读同一个事件核对；空的 `ce-dataschema` 头不合法；出站 binary 的数据照结构化 JSON 的写出核对。
   - RFC-0009：写出补「binary 的数据两个方向都照这里」；「各传输上的 `dataschema`」写明 binary 的空头不合法、插件协议的空串为何不同；新增「框架事件的数据模式」。
   - RFC-0013、RFC-0021 的事件写明 `dataschema`。
2. **`CloudEventsFormat.Encode`**：写出的文本有转义的 U+FFFD 时查原始数据，有不合法的 UTF-8 即抛出，消息同嵌套过深那一句。
3. **`CloudEventsHttpBinding`**：
   - `Write` 的 binary：声明为 JSON 且不空的数据先 `CloudEventsFormat.Encode` 一遍，其余照旧只查属性。
   - `ReadBinary`：头值经 `DecodeHeaderValue`，读不出返回 `header_invalid`；`ce-dataschema` 有这个头就交给信封，空值由 `init` 拒绝；声明为 JSON 的数据过 `DataProblemOf`。
   - `DecodeHeaderValue(string) → Maybe<string>`。
4. **框架事件**：`MessageDataSchemas`（`Find(type)`、`Count`）与 `AddMessageDataSchema`；`DomainEventDispatcher`、`SchedulingEventPublisher`、`WorkflowEventPublisher` 与 `PublishEventActivity` 从容器里取这张表（没有为 `null`），构造信封时按 `type` 查。

## 理由

- binary 与 structured 是同一个事件的两种写法，读写的规则应当只有一套；一个方向放过、另一个方向拒绝，问题只是被推到下一跳。
- 头值的解码照 CloudEvents 自己的规定，读不出就是发送方写错，不悄悄留成百分号文本。
- 框架事件的数据模式按 `type` 登记，是三处共有的键，公开面只有一张表和一个登记方法。

## 影响

- **公开 API**（`PublicAPI.Unshipped.txt` 已同步）：
  - 新增 `XiHan.Framework.Messaging.Abstractions.Envelopes.MessageDataSchemas`（`Preview`）：`Find(string)`、`Count`；`MessagingServiceCollectionExtensions.AddMessageDataSchema(services, type, dataSchema)`（`Preview`）。
  - `CloudEventsHttpBinding.DecodeHeaderValue` 由 `string` 改返回 `Maybe<string>`（`Preview` 的破坏，ADR-0131）。
  - 新增 `CloudEventsMessages.HeaderInvalid`（`protocols.cloudevents.header_invalid`，参数 `header`），10 个区域。
  - 没有新的错误码与配置键。Kernel 与 Primitives 没有改动。
- **行为变化（更新日志，升级须知）**：
  - CloudEvents 入站 binary：头值里不合法的 UTF-8 百分号序列（`%ED%A0%80`、结尾的 `%C3`、`%C0%A0`）、`%` 后不是两位十六进制数字、值里的孤立代理项，都以 400 `MESSAGING_VALIDATION_002` 拒绝；以前原样留成百分号文本。
  - CloudEvents 入站 binary：空的 `ce-dataschema` 头以 400 `MESSAGING_VALIDATION_002` 拒绝；以前当作没有。
  - CloudEvents 入站 binary：声明为 JSON（含没有 `Content-Type`）的数据有重复成员、超过 63 层、不是 JSON 或字符串不是合法的 Unicode 时 400 `MESSAGING_VALIDATION_002`；以前照收。发送方要么发合法的 JSON，要么给出不是 JSON 的 `Content-Type`。
  - CloudEvents Webhook 以 binary（缺省）发布时，这样的数据在发出之前抛出 `MESSAGING_VALIDATION_002`，与 structured 相同；以前照发。
  - `CloudEventsFormat.Encode`（四个代理、发件箱、structured 与 batch）：JSON 数据的字符串里有不合法的 UTF-8 字节时抛出 `MESSAGING_VALIDATION_002`；以前写成 U+FFFD。
  - 迁移：`CloudEventsHttpBinding.DecodeHeaderValue(value)` 的调用方改用返回的 `Maybe<string>`（`TryGetValue`），没有值即这个头读不出。
  - 新能力：`services.AddMessageDataSchema(type, dataSchema)` 让领域事件、调度与工作流发布的信封带上 `dataschema`；不登记与以前相同。
- **规范**（规范工作区已提交，指针待更新）：见「状态」。CloudEvents 绑定与 `Encode` 的几处是收窄读写的输入，记 MAJOR（`v1.0.0` 之前允许）；框架事件的数据模式只增，记 MINOR。
- **六个 SDK**：不用跟。SDK 不实现 CloudEvents 的 HTTP 绑定，也不发布框架事件；插件协议的 `data_schema` 不变。
- **测试**：
  - `CloudEventsBinaryDataTests`（新）：出站五种写不出的数据都抛出、异常与 `Encode` 的相同、一个头也不写；63 层、不是 JSON 的内容类型与空数据照发；入站五种数据的错误码、消息键与 `reason` 都与 structured 读同一个事件相同，重复成员的路径是 `$.data.order.id`；带参数的 JSON 内容类型、`+json` 与缺省的内容类型都查；不合法的 UTF-8 两个方向都拦下；`1,"other":2` 被拒；63 层、不是 JSON 的内容类型与空请求体读得出，读出的都写得成结构化 JSON。
  - `MessageContractTests`：`Encode` 拒收四种不合法的字节，本来就是 U+FFFD 的字符往返不变，不是 JSON 的数据照原样。
  - `CloudEventsBinaryHeaderTests`（新）：`DecodeHeaderValue` 读不出的十种值与读得出的六种；入站每一类头（规范属性与扩展属性）读不出时是 `header_invalid`、参数是小写的头名。
  - `CloudEventsHttpBindingTests`：空的 `ce-dataschema` 头与 structured 的空串同一个错误，只有空白的照旧被拒。
  - `CloudEventsProtocolTests`：入站两种模式下数据不合法、头值读不出、空的 `ce-dataschema` 头都是 400 的 Problem Details（`MESSAGING_VALIDATION_002`），处理器不运行；binary 的 Webhook 发布拒绝重复成员的数据、不发请求。
  - `MessageDataSchemasTests`（新）与 `DomainEventDispatcherTests`、`SchedulingEventDataSchemaTests`（新）、`WorkflowEventDataSchemaTests`（新）：登记的核对、重复与容量；三类事件带上登记的模式，没登记的 `type` 与没有登记表时不带。
  - 改实现之前这些用例都失败，读得出的、照发的几条除外。
- **文档**：`Protocols.CloudEvents`、`Messaging.Abstractions`、`Scheduling.Abstractions`、`Workflow.Abstractions` 的 README 已同步；文档站与更新日志待改。

## 没有解决的

- 出站 binary 的头值不编空格与双引号：CloudEvents 1.0.2 §3.1.3.2 要求发送方把 U+0020、U+0022 也百分号编码，`EncodeHeaderValue` 只编 `%` 与可打印 ASCII 之外的字符。
- 入站不解开双引号括起的头值：CloudEvents 要求接收方先按 RFC 7230 §3.2.6 解开，.NET 把引号读成值的一部分。
- `ce-subject`、`ce-time` 的空头仍与没有这个头相同；structured 的 `"subject": ""` 读成空串，`"time": ""` 不合法。
- 数据模式只能在代码里登记，不能从配置读。
