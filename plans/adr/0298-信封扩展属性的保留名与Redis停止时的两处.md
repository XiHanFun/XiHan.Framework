# ADR-0298 信封扩展属性的保留名与 Redis 停止时的两处

## 状态

已采纳（规范 RFC-0009「信封」与 `protocol/cloudevents.md` 已修订，`messaging/envelope-extensions.json` 与 `cloudevents-envelopes.json` 已补，Go 一致性模块的规则已改并回放通过；`EnvelopeExtensions` 新增 `ReservedNames`，`IsValidName` 排除保留名；RFC-0009 的 Redis Provider 一节已修订，`Messaging.Redis` 的消费循环收到停止信号之后不再发起新的认领与读取，公开面不变）。

## 日期

2026-11-25

## 背景

### 扩展属性名收得下规范属性名

ADR-0283 收紧信封读取时记下了一处没有改的（「没有解决的」最后一条）：扩展属性名的文法 `^[a-z0-9]{1,20}$` 收得下 CloudEvents 的规范属性名。

- `MessageEnvelope` 接受名为 `id`、`source`、`type`、`time`、`subject`、`data`、`specversion`、`datacontenttype` 的扩展属性；`EnvelopeExtensions.IsValidName`（`Stable`）对它们返回真。
- 要到 `CloudEventsFormat.Encode` 写出时才被拒：扩展属性是顶层成员，与规范属性写成重复的成员，写出之后的 JSON 文本检查报重复（ADR-0283 决策 7）。RabbitMQ、Kafka、Redis 三家发布时经 `Encode`，拒绝；内存代理不写出，这样的信封照常投递，处理器读到的 `Extension("id")` 与 `Id` 是两个值。同一个信封在四个代理上一个能发、三个不能发。
- 仓库所有者已拍板：文法里就排除，清单照 CloudEvents 1.0 的规范属性与结构化 JSON 格式的成员名，构造信封、设扩展属性时就拒绝。

读代码时另看到一处：清单里的 `dataschema` 是 CloudEvents 1.0 的可选属性，但框架的信封没有这个属性，`CloudEventDocument` 也没有这个成员。带 `dataschema` 的文档现在被读成名为 `dataschema` 的扩展属性，写出时照原样写成顶层成员——读写碰巧是对称的，信封却把一个规范属性当成了扩展属性。把它列为保留名之后，这样的文档就要换一种处理。

### Redis 的消费循环停止之后又读了一批

`RedisStreamConsumer.RunAsync` 每一轮依次做：到了周期就维护（`Maintain` 脚本）、回收（`XAUTOCLAIM`）一批并逐条投递、`XREADGROUP` 读一批新条目并逐条投递。停止信号只在两处看：读到的一批里每派发一条之前（停止就退回剩下的投递次数、跳出），与 `while` 的条件。

- 回收途中收到停止信号：回收的那一批照 ADR-0284 退回、跳出，然后这一轮照样往下走，发出一次 `XREADGROUP`。读到的新条目一条也不派发，投递次数退回到 0，却已经挂在这个停下的消费者名下，进了组的待确认列表——别的实例的 `XREADGROUP >` 读不到它们，只能等它们空闲超过 `ClaimIdleTimeSeconds`（默认 60 秒）再回收。不读的话，它们留在流里，别的实例下一次读取（`PollIntervalSeconds`，默认 0.2 秒）就读到。ADR-0261 的「影响」与 ADR-0284 的「没有解决的」都记过这一条。
- 维护途中收到停止信号也一样：维护之后照样回收一批、读一批，认领与读取都让投递次数加一，再退回去。

## 备选

1. **保留名只拦写出，`IsValidName` 不变**：否决。这就是现状：内存代理不写出，同一个信封在不同代理上行为不同。
2. **保留名只列现在承载的规范属性，不列 `dataschema`**：否决。仓库所有者给的清单照 CloudEvents 1.0，`dataschema` 是其中之一；它不进保留名，信封就继续把一个规范属性当扩展属性读写，别的 CloudEvents 实现把它当 `dataschema` 读。
3. **给信封加 `DataSchema` 属性**，把 `dataschema` 当规范属性读写：这次不做。信封还经插件协议传给隔离运行的插件，`plugin.proto` 的 `Envelope` 没有这个字段，加属性就要改协议（新字段、`buf generate`、六个 SDK 的映射），不然经插件转一手就悄悄丢掉；这超出了这次的范围，记在「没有解决的」。
4. **带 `dataschema` 的文档照收、丢掉这个成员**：否决。静默丢弃，再写出时就没有了。
5. **带 `dataschema` 的文档不合法**：采纳。它按「信封」第 5 条是规范属性之外的顶层成员，即扩展属性，名字是保留名，于是不合法，原因与别的不合法扩展名相同（`messaging.envelope.invalid_extension`，参数 `name`）。不另造一条规则、一个消息键。
6. **保留名的原因与「不合文法」用同一句异常消息**：否决。`id` 完全合文法，「只能是小写字母与数字、不超过 20 个字符」说的不是它的问题；`MessageEnvelope` 对保留名另给一句（`… is reserved: it is a CloudEvents context attribute or a member of the JSON event format.`）。
7. **删掉 `Encode` 写出之后的 JSON 文本检查**（扩展属性与规范属性同名的信封已经构造不出来）：否决。那道检查覆盖整份文档，JSON 数据里的重复成员、超过 64 层的嵌套与不成对的代理项转义仍然要靠它；它也是双保险——信封以后多承载一个规范属性而保留名漏了更新，写出的重复成员照样在发布时拦下。只把文档与注释里「扩展属性与规范属性同名」这一种原因去掉。
8. **保留名不公开，只在 `IsValidName` 里用**：否决。清单是规范的一部分，回放向量要拿它比对；公开成只读列表 `EnvelopeExtensions.ReservedNames`，与 `BaggageMappings` 一样。
9. **只在 `XREADGROUP` 之前看一次停止信号**：否决。维护途中收到的停止信号同样会让回收白白认领一批再退回；在回收之前也看一次，两处都不发起新的命令。
10. **已经发出的 `XREADGROUP` 随停止信号取消**：做不到、也不需要。StackExchange.Redis 的命令不收取消令牌，发出去的读取 Redis 照样执行；拿到的条目照 ADR-0284 退回投递次数（第一条派发之前就看到停止信号）。

## 决策

1. **保留名**：`specversion`、`id`、`source`、`type`、`datacontenttype`、`dataschema`、`subject`、`time`、`data`、`data_base64`（CloudEvents 1.0 的规范属性名与结构化 JSON 格式的成员名；最后一个本来就不合文法，列进来是为了清单完整）。按整串比较，`ids`、`dataschemas` 这样只比保留名多一个字符的名字照常合法。
2. **`EnvelopeExtensions.IsValidName`** 对保留名返回假；新增 `EnvelopeExtensions.ReservedNames`（按序数升序的只读列表）。
3. **构造信封、设扩展属性时就拒绝**：`MessageEnvelope.Extensions` 的 `init` 与 `WithExtension` 对保留名抛出 `ArgumentException`（消息另一句，见备选 6）。四个代理一致：保留名的扩展属性根本到不了代理。
4. **读取**：结构化 JSON 里的规范属性都由信封承载，不会落进扩展属性；带 `dataschema` 的文档按第 5 条不合法（备选 5）。binary 模式（`protocol/cloudevents.md`）的 `ce-dataschema`、`ce-data` 头同样不合法。插件协议传来的信封带保留名的扩展属性，`PluginEnvelopeMapper.FromProto` 照现有做法返回 `MESSAGING_VALIDATION_002`。
5. **`CloudEventsFormat.Encode` 写出之后的检查保留**（备选 7）。

### Redis 停止时不再读取（RFC-0009「Redis」）

6. 消费循环在发起回收（`XAUTOCLAIM`）之前、回收之后发起读取（`XREADGROUP`）之前各看一次停止信号，收到即退出循环：不再发起新的认领与读取。已经发出的读取与认领拿到的条目照 ADR-0284 退回投递次数。

## 理由

- 扩展属性名合不合法应当在一处判定、在构造时判定：信封是四个代理共用的值，代理之间不该有「这一家能发」的差别。
- 保留名是规范的一部分，用清单写死比「写出时撞上重复成员」好理解，也好让别的语言照着做；Go 一致性模块按清单实现之后一次回放通过。
- 停下的实例不该再从流里拿走条目：拿走了也不处理，只是把它们从「谁都能读」变成「等回收」。

## 影响

- **公开 API**（`PublicAPI.Unshipped.txt` 已同步）：新增 `EnvelopeExtensions.ReservedNames`。`EnvelopeExtensions.IsValidName` 与 `MessageEnvelope`（都是 `Stable`）的行为收紧。
- **升级须知**：
  - 名为 `specversion`、`id`、`source`、`type`、`datacontenttype`、`dataschema`、`subject`、`time`、`data` 的扩展属性不再合法：`MessageEnvelope` 的 `Extensions` 与 `WithExtension` 抛出 `ArgumentException`。以前用 RabbitMQ、Kafka、Redis 时它们本来就发不出去（`Encode` 抛出）；用内存代理时照常投递，现在要改名（`xihan` 开头或别的不撞名的名字）。
  - 带 `dataschema` 的 CloudEvents 文档（structured 的成员、binary 的 `ce-dataschema` 头）与带 `ce-data` 头的 binary 请求不再读得出：以前读成同名的扩展属性，现在信封不合法——CloudEvents 入站端点返回 400 `MESSAGING_VALIDATION_002`，远程代理上按无法解码的消息了结（竞争订阅进死信，广播订阅丢弃）。外部发送方要去掉 `dataschema`，或把数据模式放进自己的扩展属性。读过 `Extension("dataschema")` 的代码随之改。
  - 升级前已经在队列、流、主题或发件箱表里的消息：框架自己发出的不会带保留名的扩展属性（`Encode` 拒绝），不受影响；外部写进来的带 `dataschema` 的消息升级后按无法解码处理。
- **规范**（规范仓库已提交，指针待更新）：RFC-0009「信封」写明保留名、第 5 条写明带 `dataschema` 的文档不合法、「写出」一段去掉同名扩展这一种原因，`protocol/cloudevents.md` 写明保留名与两个头；改参考实现已有的行为，记 MAJOR（`v1.0.0` 之前允许）。`envelope-extensions.json` 的 `names` 新增 `reserved`，`invalid` 补上十个保留名与 `ID`、`Data`，`valid` 补七个只比保留名多一个字符的名字；`cloudevents-envelopes.json` 的 `invalid` 补「带 `dataschema` 的文档」一条。
- **Go 一致性模块**：新增 `rules.EnvelopeExtensionReservedNames`，`IsEnvelopeExtensionName` 排除保留名（`DecodeCloudEvent` 随之拒绝带 `dataschema` 的文档）；`conformance.EnvelopeExtensionNames` 新增 `Reserved`。导出面只增。
- **六个 SDK**：只认领 `core` 与 `plugin`，不认领 `messaging`，不受影响。插件经插件协议发给宿主的信封带保留名的扩展属性时由宿主拒绝（`MESSAGING_VALIDATION_002`）；SDK 在本地先拒绝更好，但不是必须。Go SDK 升 conformance 模块不用改代码。
- **测试**：`EnvelopeExtensionVectorTests` 核对保留名与向量一致、信封对每个保留名都拒绝（`WithExtension` 与 `Extensions` 两条路）、合文法的保留名给出「保留」的原因、向量里有只比保留名多一个字符的合法名字；`MessageContractTests` 里「`Encode` 拒绝同名扩展」改为构造时就抛出；`CloudEventsHttpBindingTests` 新增 binary 模式 `ce-dataschema`、`ce-data` 两条。
- **Redis 停止时**（行为变化，公开面不变）：回收或维护途中收到停止信号，消费循环不再读新条目，它们留在流里，别的实例下一次读取就读到、从第 1 次尝试起；以前被这个停下的实例读走、退回投递次数之后挂在它名下，要等 `ClaimIdleTimeSeconds` 过后才被别的实例回收。只有一个实例时没有区别：重启后的读取与回收都会拿到它们。
- **Redis 的测试**：
  - 打桩数据库（不需要 Redis）：`FakeRedisDatabase` 记下读取与回收的次数，维护脚本可以挂一道闸门；新增「维护途中收到停止信号：不再回收、不再读取」（改动之前回收与读取各一次，读到的条目被退回）。
  - 真实 Redis（设置 `XIHAN_TEST_REDIS` 时）：新增「回收途中停止：新条目留在流里、组的读取位置停在回收到的那一条上，接手的实例直接读到它们、从第 1 次尝试起」（改动之前两条新条目挂在停下的消费者名下）。本机用临时容器跑过（Redis 8），改动之前这条失败、之后通过；CI 用 Redis 7。
- **文档**：文档站 `messaging` 各页里扩展属性名的说明、`appendix/spec`、更新日志待改。

## 没有解决的

- 信封不承载 CloudEvents 的 `dataschema`（备选 3）。要承载得同时给 `MessageEnvelope` 加属性、给 `plugin.proto` 的 `Envelope` 加字段并让六个 SDK 跟上，规范里的信封属性一并补上。
