# ADR-0085 消息实例标识可配置、Kafka 预建主题、插件卸载撤回登记与启动核对

## 状态

已采纳（`spec/rfcs/0009-messaging.md`、`spec/rfcs/0004-pipeline-stages.md`、`spec/rfcs/0008-security.md`、`spec/rfcs/0011-operations.md`、`spec/rfcs/0017-plugin-protocol.md`、`spec/rfcs/0018-wasm-plugins.md`、`spec/conventions/error-codes.md` 与 `spec/plugin/` 的 `invocation.md`、`lifecycle.md`、`wasm.md` 随之修订）。修订 ADR-0079：实例标识可以配置（原为「不可配置、每次启动重新生成」），Kafka 主题不存在由待定改为已定。修订 ADR-0080：登记表不再只增不减（卸载按归属撤回），契约策略的核对从授权特性的 `StartAsync` 移到启动核对、不论失败策略都拒绝启动。

## 日期

2026-11-01

本 ADR 记录第四批回灌里宿主分区的决定（`h-batch-backlog.md` 第 3、4、10、11、15、17 条，来由是 ADR-0079、ADR-0080 之后留下的待定项），每项一节。第 15 条在仓库外（framework-sdk-go）、第 17 条是测试，各在最后一节简述。

## 一、消息实例标识可配置（第 3 条）

### 背景

广播订阅（ADR-0079）给每个实例一个独占的 RabbitMQ 队列 `<QueuePrefix><topic>@<InstanceId>` 与 Kafka 消费组 `<GroupId>@<InstanceId>`。实例标识 `MessagingInstance.Id` 每次启动随机生成（机器名、进程 ID、8 位随机十六进制），不能配置。运维在代理的管理界面里只认得出机器名部分，想用 Pod 名这类稳定的名字认出实例做不到。

ADR-0079 当时试过把标识放进 `MessagingOptions`，被 CLI 知识库的配置索引测试拦下：选项要是配置的函数（同样的配置读出同样的选项），随机值破坏这一点。

### 备选

1. 选项只记配置值（没有配置为空），单例 `MessagingInstance` 取配置值、没有时生成（采纳）。
2. 选项里放随机缺省值：同样的配置读出不同的选项，配置索引测试与「缺省值与不写等价」的约束都不成立。
3. 维持不可配置：运维诉求没法满足。

### 决策

- `MessagingOptions.InstanceId`（`string?`，配置键 `XiHan:Messaging:InstanceId`），缺省为空；`MessagingInstance` 的构造改为 `MessagingInstance(MessagingOptions options)`（去掉无参构造），配置了取配置值，否则照旧生成。生成时去掉机器名开头的 `-`，生成值与配置值同一文法。
- 文法 `^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$`（实现用 `\z` 结尾，不放过结尾换行）：1 到 64 个字母、数字、`.`、`_`、`-`，字母或数字开头，不含 `@`。合文法的值可以直接用在 RabbitMQ 队列名与 Kafka 消费组名里。写了这个键但不合文法（含空字符串）在装配时 `ArgumentException`，不退回随机值。
- 文档写明必须每个同时在线的实例唯一：Kafka 上重复即同一个广播消费组，组内分摊分区，广播静默退化成竞争；RabbitMQ 上后启动的实例声明同名独占队列被代理拒绝（`RESOURCE_LOCKED`），订阅失败、启动失败（`MESSAGING_UNAVAILABLE_001`）。部署指南给出 Kubernetes 下行 API 把 Pod 名设进 `XiHan__Messaging__InstanceId` 的写法。

### 理由

- 选项是配置的函数这条约束不动，随机性留在单例里：既满足配置索引，又让不配置的应用行为与以前完全相同。
- 非法值启动失败而不是退回随机：配了却悄悄不生效，运维会以为名字是自己配的那个。
- 不做唯一性的运行期检测：Kafka 上同组的两个成员本来就是合法的竞争消费，框架分不出「配错了」与「本来就该竞争」；RabbitMQ 上冲突自然表现为启动失败。文档把后果写清楚。

### 影响

- 公开 API：新增 `MessagingOptions.InstanceId`；`MessagingInstance()` → `MessagingInstance(MessagingOptions options)`（经容器创建不受影响）。
- 配置：新增 `XiHan:Messaging:InstanceId`；CLI 知识库 `configuration.json` 同步。
- 启动期新增失败：配置了 `XiHan:Messaging:InstanceId` 且不合文法；两个同时在线的实例在 RabbitMQ 上配了同一个值时，后启动的那个启动失败。
- 固定标识的代价写进文档：旧连接没有及时断开时（节点失联），同名重启的实例在 RabbitMQ 心跳超时前启动失败、在 Kafka 会话超时前收不到广播。缺省的生成值没有这个问题。
- 测试：配置值进单例与选项、同样的配置读出相等的选项、没有配置时选项为空而单例随机；合法与非法取值各一组；内存特性装配读到配置值、非法值装配失败；Kafka 广播消费组与 RabbitMQ 广播队列名带配置值（打桩）；真实 RabbitMQ 上两个实例配同一标识，第二个启动失败、第一个不受影响。

## 二、Kafka 主题不存在维持失败，要求预建主题（第 4 条）

### 背景

ADR-0079 让 RabbitMQ 发布到没有订阅者的主题时丢弃并照常成功。Kafka 的主题由 broker 按 `auto.create.topics.enable`（缺省开启）自动建出，没人订阅的主题按保留期清理，没有积压。生产集群常关闭自动建主题，这时发布到不存在的主题在客户端约 30 秒后失败（`MESSAGING_UNAVAILABLE_001`），调度、工作流这类没人订阅的框架事件也一样。是否把「主题不存在」也当作「没有订阅者」丢弃，留作待定。

### 备选

1. 维持失败，文档要求预建主题（采纳）。
2. 主题不存在当作没有订阅者丢弃：Kafka 的主题是否存在与有没有订阅者无关，主题名写错、`TopicPrefix` 配错也会被吞成一条 Debug 日志，消息无声丢失。
3. 框架在启动时替应用建主题：要管理员权限，分区数、副本数、保留期都要由部署方定，框架给不出正确的缺省。

### 决策

- 行为不变；RFC-0009 的「待定」改为已定：关了自动建主题的集群里主题不存在是部署错误，框架不建主题，也不当作没有订阅者。
- 文档写明三种表现（发布约 30 秒后失败、经发件箱的按发件箱规则退避重试到死信；订阅记 Warning 一直等；死信主题不存在时回退重投、分区停在原地）与要预建的清单：应用发布与订阅的主题、被竞争订阅主题的死信主题、装配了调度时的 9 个 `scheduling.job.*` / `scheduling.run.*`、装配了工作流时的 25 个 `workflow.*`，都带 `TopicPrefix`，没人订阅的也要建。

### 理由

- 宁可显式失败，不可无声丢失：第 2 种做法省掉的只是一次部署步骤，代价是配置错误再也看不见。
- 建主题属于部署：与数据库建表、对象存储建桶一样由基础设施负责（`ObjectStorage.S3` 的桶缺省同样由基础设施预先建好）。

### 影响

- 只改文档：Messaging.Kafka 包页新增「主题」一节（含清单与 `kafka-topics.sh` 示例），消息指南、部署指南、调度与工作流指南的「事件」一节、Kafka 包 README；顺带改正旧文档里不存在的示例主题 `scheduling.job.started`。
- 没有新测试：失败路径要真实 Kafka 关闭自动建主题、并等客户端约 30 秒判定，行为没有变化。

## 三、插件卸载按归属撤回登记，可直接再加载（第 10 条）

### 背景

进程外与 Wasm 插件的代理操作与清单声明的权限码（ADR-0080）登记进宿主的 `OperationRegistry` / `PermissionRegistry`，登记表只增不减：卸载（`UnloadAsync`）不撤回，卸载后调用返回 `PLUGINS_UNAVAILABLE_001`；同一插件卸载后再 `LoadAsync`，会撞上自己留下的登记（`KERNEL_OPERATION_001` / `KERNEL_SECURITY_001`），换版本只能重启宿主。`lifecycle.md` 里「重启失败到上限后直到 `UnloadAsync` + `LoadAsync`」对这类插件不成立。

### 备选

1. 登记带归属，卸载时按归属一次撤回操作与权限码（采纳）。
2. 再加载时允许覆盖同名登记：覆盖分不清「同一插件的新版本」与「两个插件抢同一个名字」，后者应当冲突。
3. 维持只增不减、卸载后须重启宿主：运行期加载卸载的意义只剩一半。

### 决策

- 登记带归属（特性或插件的清单 ID）：`OperationRegistry.AddRange(owner, operations, permissions)`（每个操作的 `OwnerId` 都必须是 `owner`，否则 `ArgumentException`）；`PermissionRegistry.Add(owner, permission)` / `AddRange(owner, permissions)`，每个码记下归属；`FeatureContext.AddPermission` 的归属是特性的清单 ID。
- 按归属撤回：`OperationRegistry.RemoveOwnedBy(owner)` 在登记表的锁里一次撤回该归属的全部操作与权限码（先操作、后经 `PermissionRegistry.RemoveOwnedBy(owner)` 撤回权限码），与登记互斥，返回撤回的操作数；没有登记过的归属什么都不做。撤回不经过挂上的核对，也不连带别的归属。
- 宿主：`UnloadAsync` 在同一段加载卸载锁里把运行时移出宿主并撤回登记，再停止插件；登记前核对运行时仍是宿主里的当前运行时（登记与撤回另由一把锁互斥），卸载之后才完成的重试握手、与卸载交错的运行期加载都不会留下指向已卸载插件的登记。进程外与 Wasm 宿主相同。
- 卸载后的调用：按 ID 查登记表的入口返回 `KERNEL_NOTFOUND_001`（不进管道）；撤回之前已经查到操作、正在执行的调用照常完成，或在插件停止后以 `PLUGINS_UNAVAILABLE_001` 结束。
- 不随卸载撤回的写进规范：配置阶段接进容器的订阅与消息目录（卸载期间发给它的事件不投给任何插件，再加载后恢复）；协议适配器在启动时发布的路由与文档（按操作 ID 转交调用器，再加载后同一 ID 照常可用，描述变了要重启宿主）；Wasm 插件贡献的中间件（卸载后失败即关闭，运行期又不能加载这类插件，换版本要重启宿主）。
- 别的归属的操作以 `permission` 引用了被撤回的码时照旧登记着，码重新登记之前它们的授权按策略不存在拒绝（`SECURITY_NOTFOUND_001`），不会放行。

### 理由

- 归属是现成的概念：操作描述早就带 `OwnerId`，中间件也按清单 ID 归属；权限码补上归属，撤回就是一次按键的批量删除。
- 撤回与登记同一把锁：登记失败只会是操作重名，不会撞上「操作撤回了、码还在」的半组（并发用例以此断言）。
- 先撤回再停止：新调用立刻得到未登记，不再排进正在停止的进程。
- 读取不取锁不变：调用按 ID 逐个查找，每个操作要么还在、要么已撤回；为此不把登记表改成写时复制。

### 影响

- 公开签名（都在 Unshipped，直接改，没留旧签名）：`OperationRegistry.AddRange(descriptors, permissions)` → `AddRange(ownerId, descriptors, permissions)`；`PermissionRegistry.Add(permission)` → `Add(ownerId, permission)`；`PermissionRegistry.AddRange(permissions)` → `AddRange(ownerId, permissions)`；新增 `OperationRegistry.RemoveOwnedBy`、`PermissionRegistry.RemoveOwnedBy`。经 `FeatureContext.AddPermission` / `AddOperation` 登记的代码不用改。
- 行为：插件卸载后调用它的操作由 `PLUGINS_UNAVAILABLE_001` 变为 `KERNEL_NOTFOUND_001`（HTTP 404）；卸载后可以直接再加载。
- 没有新错误码与消息键（卸载交错时的登记返回已有的 `PLUGINS_NOTFOUND_001`）。
- 测试：内核按归属撤回（别的归属与宿主的码不动、撤回后同一归属再登记）、混归属的组被拒、四个归属争同一组名字反复登记撤回（失败只会是操作重名）、权限登记表按归属撤回；进程外插件运行期加载卸载再加载、配置阶段发现的插件卸载后撤回声明的码并再加载、八个调用方并发调用中卸载（每个调用以成功、不可用或未登记结束，卸载后的调用都是未登记，之后再加载照常服务）；Wasm 插件卸载再加载、并发调用中卸载、贡献中间件的插件卸载后中间件失败即关闭。

## 四、契约引用不存在的策略在任何失败策略下都拒绝启动（第 11 条）

### 背景

ADR-0080 让授权能力的 Provider 在 `IHostedFeature.StartAsync` 里调用 `PolicyCatalog.EnforceOn` 核对契约按名引用的策略。失败走内核的特性启动失败：缺省 `FailurePolicy.TerminateStartup` 下应用启动失败；应用改用 `IsolateFeature` 时，授权特性记为 `Failed`、应用 `Degraded` 继续运行，Authorize 中间件仍在，坏的策略名调用时 404。

### 备选

1. 内核新增启动核对：特性在 `Configure` 里登记，容器建好之后、任何特性启动之前执行，抛出即终止启动，不归失败策略管（采纳）。
2. 按错误种类区分：特性启动时抛出的 `*_VALIDATION_*` 一律视为装配错误、不论策略终止。牵连所有在启动时做配置校验的特性，靠错误码的种类决定宿主行为也太隐晦。
3. 授权特性在隔离策略下自己让宿主退出：特性无权越过内核的失败策略，也会让别的 Provider 各自实现一遍。

### 决策

- 内核新增 `FeatureContext.AddStartupCheck(Action<IServiceProvider> check)`：`App.StartAsync` 在第一个特性启动之前按登记顺序各执行一次（只执行一次，重复启动不再执行），任一抛出即原样抛出、终止启动（不包成 `KERNEL_HOSTING_002`），后面的核对与全部特性都不再执行，记一条 Error 日志；不归 `KernelOptions.FailurePolicy` 管。
- `Security.InMemory`、`Security.Database` 被选为授权能力时在 `Configure` 里登记 `PolicyCatalog.EnforceOn(OperationRegistry)` 为启动核对，不再实现 `IHostedFeature`。自己实现 `security.authorization@1` 的 Provider 同样这样做（RFC-0008）。
- 坏的策略名：`TerminateStartup` 与 `IsolateFeature` 下都抛出 `SECURITY_VALIDATION_004`（异常就是它本身），宿主启动失败。

### 理由

- 失败策略管的是运行条件（连不上外部服务），隔离一个特性可以让其余功能先服务；装配错误（引用了不存在的东西）隔离哪个特性都修不好，与装配完成时的权限码核对（`KERNEL_VALIDATION_005`）同一性质，应当同样直接终止。
- 放在任何特性启动之前：核对挂到登记表上的规则（`OperationRegistry.Enforce`）先于插件宿主启动后的重试登记生效；配置阶段发现的插件的操作与声明的权限码此时都已登记。
- 显式登记而不是按错误码推断：哪些核对不受失败策略约束一目了然，别的特性的启动失败语义不变。

### 影响

- 公开 API：新增 `FeatureContext.AddStartupCheck`；`InMemorySecurityFeature`、`DatabaseSecurityFeature` 从 `IHostedFeature` 改回 `IFeature`（去掉 `StartAsync` / `StopAsync`，经 `UseFeature` 装配的应用不用改）。
- 启动失败的异常：契约引用了不存在的策略时由 `FrameworkException(KERNEL_HOSTING_002)` 包 `SECURITY_VALIDATION_004` 变为直接抛出 `FrameworkException(SECURITY_VALIDATION_004)`；`IsolateFeature` 下由「应用 Degraded 继续运行」变为启动失败。
- 测试：启动核对在全部配置之后、第一个特性启动之前按登记顺序各执行一次，重复启动不再执行；两种失败策略下核对抛出都原样终止启动、特性都停在 `Configured`；内存与数据库授权、Wasm 配置阶段发现的插件引用坏策略时直接抛出 `SECURITY_VALIDATION_004`；隔离策略下同样拒绝启动。

## 五、截止时间中间件对下游写下的取消结果按触发的令牌归因（第 17 条顺带）

### 背景

`Plugins.OutOfProcess.Tests` 的 `Deadline_IsPropagatedToThePlugin` 用真实时钟设 300 毫秒截止时间，全量并行跑时偶发失败。查下来不只是用例写法：内核 `DeadlineMiddleware` 以宿主时钟的定时器触发截止令牌，插件运行时接住 gRPC 的 `Cancelled`，再按 `context.IsExpired(clock.GetUtcNow())` 判断是超时还是取消。定时器按系统节拍触发，可能早于时钟读数到达截止时间，判断为「没过期」，于是写下 `KERNEL_CANCELLED_001` 结果正常返回；中间件只翻译异常，结果原样留下——超时被报成取消（本次改动期间复现过一次：期望 `KERNEL_TIMEOUT_001`，实际 `KERNEL_CANCELLED_001`）。RFC-0003 第 1 条要求超时是 `KERNEL_TIMEOUT_001`。

### 决策

- `DeadlineMiddleware`：下游正常返回、写下的是 `KERNEL_CANCELLED_001`，而截止令牌已触发、外部令牌没有时，改写为 `KERNEL_TIMEOUT_001`。只有中间件知道是哪个令牌触发的。RFC-0004 写明。
- 用例改为注入时钟（`FakeTimeProvider`），不再依赖墙钟：截止时间放在一小时后，经插件的 `echoes.relay` 把插件收到的 `InvokeRequest.metadata` 原样带回宿主回调，宿主按其中的 `deadline` 建回调上下文，回显出的截止时间与调用时给的精确到 100 纳秒相同——断言的是「插件进程收到了截止时间」，旧用例在截止时间根本没传到插件时也会通过（宿主自己的定时取消同样得到超时）。
- 插件进程这一侧必然用真实时钟：gRPC 截止时间以相对的 `grpc-timeout` 传过去，插件按自己的时钟换算；用例不对它做计时断言。

### 影响

- 行为：下游把取消写成结果的路径（进程外与 Wasm 插件、其他这样做的 Provider）在截止时间到时一律报 `KERNEL_TIMEOUT_001`（HTTP 504），不再偶发 `KERNEL_CANCELLED_001`。
- 测试：内核用 `FakeTimeProvider` 覆盖「下游写下取消结果、截止令牌触发」改写为超时与「外部取消写成结果」保持取消（改动前前者失败）；进程外插件的截止时间用例按上文改写。

## 六、framework-sdk-go 校验 `contributes.permissions`（第 15 条）

Go SDK 的 `manifest.Read` 原先不核对 `contributes.permissions`，不合文法或重复的码要到 .NET 宿主发现时才被拒（ADR-0080 给 Schema 加了 `pattern` 与 `uniqueItems`）。`Contributions.validate` 现按同一文法 `^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$` 与唯一性核对，错误消息指出是哪个码；新增测试接受空列表、多段与带数字连字符的码，拒绝单段、大写、空段、首尾点、数字开头、下划线、冒号与重复。在 framework-sdk-go 本地提交，`spec/` 子模块指针不动。
