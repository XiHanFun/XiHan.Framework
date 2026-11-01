# ADR-0079 RabbitMQ 的队列只由订阅方声明、消息的广播订阅

## 状态

已采纳（`spec/rfcs/0009-messaging.md`、`spec/plugin/plugin.proto`、`spec/plugin/handshake.md`、`spec/plugin/invocation.md` 与 `spec/protocol/cloudevents.md` 随之修订）。

## 日期

2026-11-01

## 背景

BasicApp 6.0 接上 RabbitMQ 之后发现两处与消息有关的问题，用户批准交框架处理（BasicApp 方案 9.4、9.5 N1）：框架自己发的、应用没有订阅的事件在 RabbitMQ 里越堆越多；消息没有「每个实例都收到」的订阅。本 ADR 每项一节。

## RabbitMQ 的队列只由订阅方声明

### 背景

`Messaging.RabbitMQ` 把主题映射成持久队列 `<QueuePrefix><topic>`，经默认交换机按队列名路由，发布前先声明这个队列。没有任何实例订阅的主题照样被声明成持久队列，消息进去以后再也没有人取：BasicApp 里调度在作业增删停启时发的 `scheduling.job.*` 就这样在队列里一直堆积。

上一轮（ADR-0064）给发布通道开了发布确认，消息以 `mandatory` 发出，`basic.return` 与 `basic.nack` 都按未确认处理、返回 `MESSAGING_UNAVAILABLE_001`。只要发布方还在声明队列，退回只在队列被外部删掉时出现；一旦改成不声明，没人订阅的主题每次发布都会被退回，按现状就成了发布失败：经发件箱发出的会退避重试，十次之后记进死信，直接发布的框架事件每次记一条 Warning。

内存代理上一轮已经定下「发布到没人订阅的主题丢弃、照常成功、记 Debug 日志」（ADR-0074）。Kafka 没有同类问题：消息进主题日志，按保留期清理。

### 决策

- 发布方只声明交换机：新增选项 `XiHan:Messaging:RabbitMQ:Exchange`（缺省 `xihan`），持久的 direct 交换机，连接时声明。主题发布到这个交换机，路由键 `<QueuePrefix><topic>`；发布方不再声明任何队列。
- 队列只由订阅方声明：订阅方启动时声明持久队列 `<QueuePrefix><topic>`（参数与先前一致：持久、非独占、不自动删除），并以同名路由键绑定到交换机。订阅方第一次启动之后，它离线期间的消息照旧留在队列里。
- 发到交换机的消息被退回（路由键上没有队列绑定，即没有订阅者）与内存代理一致：丢弃，发布照常成功（批量逐项成功），代理用 `LoggerMessage` 记一条 Debug 日志（条数与主题）。`basic.nack` 仍是 `MESSAGING_UNAVAILABLE_001`，连接或通道异常仍整批抛出。
- 重试与死信不经主题的交换机：经默认交换机定向发布到所消费的队列（重试，不再重新声明队列）或死信队列 `<queue><DeadLetterSuffix>`。死信队列由死信的一方声明为持久队列并以同名路由键绑定到交换机，即主题加后缀的竞争队列。定向发布被退回说明目标队列已不存在，仍按未确认处理（原消息 `basic.nack` 并重新入队）。
- 核对了框架自己发布的事件：调度（`scheduling.job.*`，`SchedulingEventPublisher`）、工作流（`workflow.*`，`WorkflowEventPublisher`）、领域事件、CloudEvents 入站的发布模式、插件回调的发布都经 `IMessagePublisher`（直接发布者或发件箱）到 `IMessageBroker.PublishAsync / PublishManyAsync`，走的就是上面这条路径；审计不发消息。
- Kafka 不改：生产者发到不存在的主题时由 broker 按 `auto.create.topics.enable`（缺省开启）自动建主题，没人订阅的主题里的消息只留到保留期（`retention.ms`，缺省 7 天），没有消费组也就没有积压。broker 关闭自动建主题时，发布到不存在的主题在客户端判定主题不存在后失败（`MESSAGING_UNAVAILABLE_001`）；这类部署要预先建好应用与框架事件用到的主题。RFC-0009 写明。

### 理由

- 「没有订阅者就没有队列」是 RabbitMQ 发布订阅的本来用法：消息由交换机按绑定分发，谁要收谁声明队列并绑定。持久队列的生命周期跟着订阅方，没人订阅的主题从源头上不再有地方堆积。
- 退回不当失败：发布方无从知道哪些主题有人订阅，框架事件发到没人订阅的主题是常态；把它当失败会让发件箱为它退避重试直到死信，也会让调度、工作流的日志刷 Warning。与内存代理的决定（ADR-0074）一致，日志同样用 Debug。
- 继续以 `mandatory` 发出，而不是关掉它让代理静默丢弃：退回给了代理记 Debug 日志的机会，主题名写错或订阅方还没启动过时可以打开日志排查。
- 路由键带前缀：先前不同前缀的应用在同一个 vhost 里互不串扰（队列名不同）；路由键用前缀加主题，两个应用共用缺省交换机也照样隔离，测试按前缀隔离的做法不变。
- 竞争队列名与参数不变：已部署环境里订阅方用到的队列在新版本里原样复用（声明幂等）并补上绑定，积压的消息照常消费。
- 重试与死信要定向：经交换机重新发布会把重试送给同一路由键上的所有队列（下一节的广播队列也在其中）；死信要保证留存，所以由死信的一方先声明死信队列再定向发布，不能依赖「有人订阅了主题加后缀」。

### 影响

- 公开 API（Unshipped）：`RabbitMQMessagingOptions.Exchange`；配置键 `XiHan:Messaging:RabbitMQ:Exchange`（CLI 知识库的配置索引同步）。交换机名不能为空、不能以保留前缀 `amq.` 开头，否则装配时抛 `ArgumentException`。
- 行为变化：发布到没人订阅的主题现在成功并丢弃，不再建队列；订阅方第一次启动之前发布的消息不再留存（以前发布方建好的队列会替它攒着）。发件箱不会再因为没人订阅的框架事件进死信。
- 升级（已部署环境）：
  - 先前由发布方建出、没有人订阅的队列（如 `xihan.scheduling.job.started`）不再收到新消息，也不会被框架删除，已经堆积的消息留在原处。确认没有消费者后由运维删除：`rabbitmqctl list_queues name messages consumers` 找出 `consumers` 为 0 的框架主题队列，`rabbitmqctl delete_queue <队列>` 或在管理界面删除。
  - 订阅方用到的队列名与参数不变，升级后第一次启动即补上绑定，积压照常消费。
  - 滚动升级时，发布方已是新版本、订阅方仍是旧版本的那段时间里，新版本发出的消息没有绑定可走，按「没有订阅者」丢弃：先升级订阅方（或让一个新版本的订阅实例先启动一次建好绑定），再升级只发布的服务。同一个应用的实例既发布又订阅，第一个新版本实例启动即建好绑定。
  - 只发布、希望在订阅方上线前就留存消息的部署，可以由运维预先声明持久队列 `<QueuePrefix><topic>` 并以同名路由键绑定到交换机。
- 交换机被外部删除时发布通道以通道错误关闭，健康项 `messaging.rabbitmq.connection` 转为 Degraded，需要重启实例重新声明。
- 测试：打桩的连接与通道覆盖「只声明交换机、经它按路由键发布、退回丢弃并成功、订阅方声明并绑定队列、重试与死信定向发布、死信队列由订阅方声明并绑定」；真实 RabbitMQ（`XIHAN_TEST_RABBITMQ`）上覆盖「没人订阅的主题没有队列、订阅方的队列在它离线期间留存消息」；一致性套件新增「发布到没人订阅的主题成功」，内存、RabbitMQ、Kafka 都通过。

## 消息的广播订阅

### 背景

有些事要在每个实例上各做一次：吊销会话后断开连在各个实例上的连接、清掉各实例进程内的缓存。BasicApp 的 Realtime 跨实例强制断开就卡在这里（BasicApp 方案 9.5 N1）。现有订阅都是竞争消费：RabbitMQ 每个主题一个持久队列，多个实例在同一队列上竞争；Kafka 所有主题共用一个 `GroupId`，把每个实例配成不同的 `GroupId` 能变相广播，但会让全部主题都变成广播，破坏工作队列语义。全仓没有广播的概念。

### 决策

- 订阅方式：`Messaging.Abstractions` 新增 `SubscriptionMode`（`Competing` 缺省、`Broadcast`）与 `MessageSubscription(Topic, Mode)`。`AddMessageHandler<THandler>(topic, mode = SubscriptionMode.Competing)`；`MessageSubscriptions` 按（主题，方式）分组：`Add(topic, handlerType, mode)`、`HandlersFor(topic, mode)`、`All`（订阅，按首次登记的顺序）、`Topics`（任一方式）；`MessageDelivery` 加 `Mode`（构造多一个参数）；`IMessageBroker.StartAsync(subscriptions, dispatch, ct)` 接收订阅而不是主题；`MessageDispatcher` 只执行与投递同一方式的处理器。同一主题可以同时以两种方式订阅，两种方式是两次独立的投递，各自计尝试、各自重试。
- 广播的语义边界：尽力而为——只有订阅时在线、与代理连着的实例收到，不在线、重启、断开期间的消息收不到，不回放历史；在本实例内按 `MaxAttempts` 重试，到上限丢弃并记 Warning 日志，不进死信，无法解码同样丢弃；不共享去重，派发照旧不写幂等键；Kafka 的重放只投给竞争订阅。
- 实例标识 `MessagingInstance.Id`：`MessagingRegistration.Configure` 登记的单例，每个宿主一个，构造时生成，机器名（小写，非 `[a-z0-9-]` 换成 `-`，最多 32 个字符）、进程 ID、8 位随机十六进制以 `-` 连接；不从配置读取。不放进 `MessagingOptions`：选项是配置的函数，同样的配置读出同样的选项（CLI 知识库的配置索引测试按此核对缺省值），随机的标识放进去就破坏了这一点。RabbitMQ 与 Kafka 代理的构造函数多一个 `MessagingInstance` 参数。
- 内存代理：单进程就是一个实例。一条消息给主题的每种订阅方式各入队一份投递（各占通道一个位置，容量判定按份数整批），每个主题仍是一个消费循环；广播投递到上限丢弃，不进死信列表；`DeliveredCount` 每种方式各算一次。交给 `StartAsync` 的订阅方式并入主题的投递方式。
- RabbitMQ：广播订阅为每个实例声明独占、非持久、`autoDelete` 的队列 `<QueuePrefix><topic>@<InstanceId>`，以主题的路由键 `<QueuePrefix><topic>` 绑定到交换机（上一节的交换机给竞争队列与每个广播队列各一份）。重试经默认交换机定向发回本实例的广播队列；到上限、无法解码时确认掉并记 Warning 日志。
- Kafka：广播订阅用本实例独占的消费组 `<GroupId>@<InstanceId>`，`auto.offset.reset=latest`，投递完成后不提交位点；竞争订阅照旧在 `GroupId` 里、从最早位点开始、投递完成后提交。消费者工厂改为按 `ConsumerConfig` 创建（内部测试接缝）。
- 插件：`plugin.proto` 的 `Subscription` 加 `mode`（`competing` / `broadcast`，空同 `competing`），`PluginSubscription` 带 `Mode`，`Matches` 按主题、类型与方式；握手里 `mode` 不合法、同一（主题，类型）以两种方式各声明一次都是 `PLUGINS_VALIDATION_004`；宿主每个（主题，方式）登记一次转交处理器，只投给以同一方式订阅了主题的插件。进程外与 Wasm 宿主同样处理。
- CloudEvents 入站的派发模式：在接收请求的实例里对竞争与广播订阅各投递一次（先竞争后广播），两种方式都没有处理器才 404；要让广播订阅到达每个实例用发布模式。
- 一致性：`MessageBrokerConformance` 新增三个用例——同一主题的竞争与广播各收到一次、各自重试；两个宿主时广播每个实例都收到；两个宿主时竞争每条只有一个实例收到。新增可覆写的 `SupportsMultipleInstances`（缺省真，内存代理覆写为假、跳过两个多实例用例）、`StartWithSubscriptionsAsync`、`WarmUpBroadcastAsync`（发预热消息直到每个实例的广播订阅都收到一条，Kafka 的消费组分区分配是异步的）。

### 理由

- 订阅方式放在订阅登记上，而不是放在主题或 Provider 配置上：同一主题既可能要「只处理一次」（发通知邮件），也可能要「每个实例都做」（断开连接），由订阅方决定；Provider 配置一个广播开关会把全部主题都变成广播（Kafka 改 `GroupId` 的老办法）。
- 两种方式是两次投递：重试计数、死信与否各不相同，混在一次投递里，一种方式的失败会让另一种方式的处理器重跑。
- 广播不进死信：死信是竞争订阅的共享留存，可以修好后重投；广播的每份失败只属于某个实例，那个实例可能已经不在，没有可以重放的共享位置。广播的典型用途（断连接、清缓存）错过一次的后果可以接受：实例重启时本来就没有这些连接和缓存。
- 实例标识不可配置、每次启动重新生成：标识重复会让 Kafka 的广播退化成同一消费组里的竞争，而且没有任何报错；机器名与进程 ID 让运维在代理的管理界面里认出是谁，随机后缀保证同一进程里的多个宿主（测试）与重启后不重复。
- RabbitMQ 的广播队列独占、非持久、随连接删除：实例下线不留下没人消费的队列（与上一节同一个目标），名字里的 `@` 不在主题文法里，不会与竞争队列或死信队列重名。
- Kafka 的广播从最新位点开始、不提交：不回放历史，消费组空了以后集群里不留位点，频繁发版不会攒下一堆孤儿消费组的位点。代价是组内再均衡（会话超时、处理超过 `max.poll.interval.ms`）后从最新位点续上，期间的消息收不到，属于尽力而为的范围。
- 一致性用预热而不是让 `StartAsync` 等分区分配：等待分配要在客户端回调里另做定位与超时处理，生产语义也只是「启动后几秒开始收」；用例以「每个实例都收到一条预热消息」作为接上的判据，对各 Provider 都成立。

### 影响

- 公开 API（Unshipped，直接改、不留旧签名）：
  - 新增 `SubscriptionMode`、`MessageSubscription`、`MessageSubscriptions.All`、`MessageDelivery.Mode`、`MessagingInstance`（`Id`）。
  - 改签名：`MessageSubscriptions.Add(topic, handlerType)` → `Add(topic, handlerType, mode)`；`HandlersFor(topic)` → `HandlersFor(topic, mode)`；`new MessageDelivery(topic, envelope, attempt)` → `new MessageDelivery(topic, envelope, attempt, mode)`；`IMessageBroker.StartAsync(IReadOnlyCollection<string> topics, …)` → `StartAsync(IReadOnlyCollection<MessageSubscription> subscriptions, …)`（三个 Provider 同步）；`AddMessageHandler<THandler>(topic)` 多一个缺省为 `Competing` 的参数，源码兼容；`RabbitMQMessageBroker`、`KafkaMessageBroker` 的公开构造函数在 `MessagingOptions` 之后多一个 `MessagingInstance`（经容器创建不受影响）。
  - 一致性包：新增 `NoticesTopic`、`SupportsMultipleInstances`、`StartWithSubscriptionsAsync`、`WarmUpBroadcastAsync` 与三个用例。
- 自己实现 `IMessageBroker` 或直接构造 `MessageDelivery`、调用 `HandlersFor` 的代码要跟着改；只用 `AddMessageHandler` 的应用不用改，行为不变（全是竞争消费）。
- 插件协议向后兼容：旧插件不填 `mode` 即竞争消费；Go SDK（framework-sdk-go）要让插件声明广播时需要跟进生成代码与选项，不在本仓库。规范仓库里的黑盒运行器 Go 代码已重新生成。
- 运维：RabbitMQ 管理界面里每个在线实例每个广播主题多一个 `…@<实例标识>` 的独占队列，实例下线即删除；Kafka 每个在线实例每个广播订阅多一个 `<GroupId>@<实例标识>` 的消费组，不提交位点。
- 测试：内存（单宿主用例 + 广播不进死信、两种方式各占一个位置）；RabbitMQ 打桩（独占队列与绑定、广播重试定向发回、到上限与无法解码不进死信）与真实 RabbitMQ（广播队列只属于本实例、实例下线即删除；两个实例的广播与竞争）；Kafka 打桩（消费组与起始位点、不提交、不发死信）与真实 Kafka（两个实例的广播与竞争）；插件握手（`mode` 解析、非法与两种方式重复声明被拒）；CloudEvents 派发模式（两种方式各一次、只有广播订阅也派发）。
