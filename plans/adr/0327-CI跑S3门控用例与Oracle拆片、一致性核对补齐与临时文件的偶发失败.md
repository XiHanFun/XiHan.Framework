# ADR-0327 CI 跑 S3 门控用例、Oracle 拆成三个作业，一致性套件补两类插件一侧的核对，样例请求严格编码，修临时文件被占用的偶发失败

## 状态

已采纳（规范 `plugin/invocation.md` §7、RFC-0017 状态行、`conformance/README.md` 与运行器 `conformance/plugin` 已改：用例 `unsubscribed` 逐条投递读不出的 `time`，新增用例 `event-envelope` 与选项 `--envelopes`；`.github/workflows/ci.yml` 的 integration 作业起 RustFS、oracle 作业拆成三份，`framework/test/shards.json` 新增分片 `oracle-workflow`，`.github/actions/test-shard` 新增输入 `part` 与 `filter`；`Plugins.Conformance` 新增 `EnvelopeOperation` 与用例 `HandleEvent_HandsTheDataSchemaToTheHandler`，示例请求严格编码；`DataSourceRegistry` 释放时等后台探测结束；示例 EchoPlugin 读宿主写来的 `time`、`events.list` 回显信封，EchoGoPlugin 的 Go SDK 升到 `d2f3962`）。

## 日期

2026-11-27

## 背景

`plans/待办-边角问题.md`「测试与工具」里的四条，仓库所有者已拍板都做；整合时又补了一条 Oracle 作业的期限：

- **CI 没有 S3 服务**：`ObjectStorage.S3.Tests` 的门控用例只在设了 `XIHAN_TEST_S3` 时跑，CI 没有设，哪里都不跑。第四轮在本机 RustFS 1.0.6 上 28 个门控用例全过；之后 ADR-0308、0317、0322 又改了 S3 对不合规应答的处理，这些改动在真实服务上还没跑过。
- **运行器覆盖不到的 SDK 行为**：ADR-0305 让信封承载 `dataschema`，ADR-0306 让 SDK 读不出宿主写来的 `time` 时以 gRPC `InvalidArgument` 拒绝投递。两件都只有各 SDK 的单元测试核对：规范的运行器与 .NET 的 `Plugins.Conformance` 不发读不出的 `time`；处理器不回显信封，黑盒看不到作者拿到的 `dataschema`。
- **一致性套件宽松编样例载荷**：`PluginProtocolConformance` 与 `WasmPluginConformance` 用 `ByteString.CopyFromUtf8` 编派生类给的 `SampleRequests`，孤立的代理项被换成 U+FFFD，插件收到的是另一份请求（RFC-0001「写出与转交」，ADR-0312）。
- **临时文件被占用的偶发失败**：全量测试偶发 `%TEMP%` 下的文件被占用、`IOException`，见过 `PluginOutputLimitTests.OverlongOutputLine…` 与 `SqlSugarDataSourceConformanceTests.GetOrAdd…`，重跑通过。
- **Oracle 作业会超时**：ADR-0326 给 `Workflow.Database.Tests` 补了 Oracle 的引擎（42 条）与任务（9 条）门控类。按 CI 运行 45854971994 推算，现有 17 条 Oracle 用例串行约 9.5 分钟、每条约 33 秒（每条新建用户与全部表；`OracleGroup` 不与别的集合并行，见它的说明），多出的五十来条约多 29 分钟，`test-oracle` 会到约 43 分钟，超过 `timeout-minutes: 30`。

## 查明的事实

### RustFS 镜像

- 本机有 `rustfs/rustfs:latest`，只看不运行（`docker image inspect` 与 `docker image history`）：标签 `version=v1.0.6`、`release=1.0.6`、`org.opencontainers.image.revision=de9009d8…`，仓库摘要 `sha256:ee717072290bec9b11abfc0365f8310b00df1cd9b3639943edae47bdef05044a`；以 `rustfs` 用户运行，`VOLUME /data`、`RUSTFS_VOLUMES=/data`，开放 9000 与 9001；装了 `curl`，没有 `HEALTHCHECK`。
- Docker Hub 的标签接口（只读请求）列出 `1.0.6`，摘要与本机的相同（2026-11-15 推送）；`latest` 已是 2026-11-26 的 `1.0.7`。
- 测试自己建桶（`S3TestServer.EnsureBucket`，`BucketAlreadyOwnedByYou` / `BucketAlreadyExists` 当已存在），密钥缺省 `rustfsadmin`（RustFS 镜像不设密钥时的根凭据，ADR-0086），可由 `XIHAN_TEST_S3_ACCESS_KEY` / `XIHAN_TEST_S3_SECRET_KEY` 改。

### 六个 SDK 能不能过新的核对（只读）

- 读不出的 `time`：Go、TypeScript、Rust、Java、Python、PHP 都在找订阅与就绪核对之前读 `time`、以 `InvalidArgument` 拒绝。改过的运行器在本机实跑：Go（Windows 与 Linux 容器）、TypeScript、Python、Rust（Windows）、PHP（Linux 容器）的 `examples/echo` 的 `unsubscribed` 都通过；Java 只读了代码（`Wire.parseTimestamp` 在 `PluginService` 找订阅之前）。
- `dataschema`：六个 SDK 的信封都承载它（Go `DataSchema string`、TypeScript `dataSchema: string`、PHP `$dataSchema string`，空串是没有；Rust `Option<String>`、Java `Optional<String>`、Python `str | None`）。六个 `examples/echo` 都没有回显信封的操作，`events.list` 只返回 ID（Rust 另有 `deliveries`），所以新用例 `event-envelope` 现在跳过；给了 `--envelopes events.list` 时 Go 的 echo 以「载荷里没有 `envelopes`」失败。
- 各 SDK 的黑盒测试：Go 从运行器 `help` 读用例全集、要求恰好全跑；TypeScript、Rust、Java、PHP 写死 14 个用例名并核对先后，Python 写死集合；六个都把跳过当失败。

### 临时文件被占用

- **`SqlSugarDataSourceConformanceTests`：查实**。`DataSourceRegistry.GetOrAddAsync` 登记或替换之后以 `_ = ProbeEntryAsync(...)` 立即探一次，不等它；SqlSugar 的探测另建客户端、打开连接走一次往返（`SqlSugarRoundTrip`），也就是开着 SQLite 库文件。`DisposeAsync` 只取消 `_stopping`、不等进行中的探测就释放数据源并返回。用例 `GetOrAdd_…` 登记、替换各触发一次探测，随后宿主释放、测试类的 `Dispose` 立刻 `File.Delete`（不重试）：探测还没结束时文件被它的连接占着。全量测试时线程池忙，探测排得晚，窗口变大。新加的用例 `Dispose_WaitsForTheProbeStartedOnRegistration_BeforeDisposingTheSource` 用不看取消令牌的替身探测复现：修之前 `DisposeAsync` 在探测仍在进行时就返回，并且先释放了它在用的数据源。
- **`PluginOutputLimitTests`：推断**。用例各自已经有独立的临时目录（`Directory.CreateTempSubdirectory`），这个目录同时是脚本插件（Windows 上 `cmd.exe`）的工作目录，脚本读着里面 8 MiB 的输出文件。宿主释放插件进程时只等根进程退出（`ProcessTermination.KillAsync` 等的是根进程的退出任务），`Process.Kill(entireProcessTree: true)` 对进程树里别的进程（`conhost.exe`）的终止是异步的；删除目录撞上它们还没放开的工作目录或文件句柄即 `IOException`。本机在不加负载时各跑了 150 次自然退出与 100 次强制终止后立即删目录，一次都没复现，所以这是按机制推断，没有抓到现场；杀毒软件扫描刚写入的文件同样可能短暂占住，也没法排除。

## 备选

### CI 的 S3 服务

1. **RustFS，钉 `1.0.6`**：采纳。与第四轮本机实测同一版本、同一摘要；ADR-0086 已经选它作 `xihan pack` 的 `blob` 依赖服务（零配置、内建不要求认证的 `/health`、认条件写入）。
2. **钉 `latest` 或 `1.0.7`**：否决。`latest` 随时变；`1.0.7` 还没有人在它上面跑过这些用例，第一次跑出问题分不清是版本还是代码。
3. **钉摘要（`rustfs/rustfs:1.0.6@sha256:…`）**：否决。现有服务都钉标签（`qdrant/qdrant:v1.18.2`）；摘要写在注释里，标签被重新推送时能对出来。
4. **放进哪一片**：integration（采纳）。它本来就是 Redis、RabbitMQ、Qdrant 这类起得快的服务的合集，作业约 2.5 分钟、远短于 core 的约 3.6 分钟；RustFS 约 1 秒就绪，镜像几百 MB，`ObjectStorage.S3.Tests` 的门控用例几十条，作业估计长到 3–3.5 分钟，仍不是最慢的。单独一片（像 Oracle 那样）否决：它既不慢也不大，多一台运行器只多一遍还原与构建。
5. **凭据**：容器设 `RUSTFS_ACCESS_KEY` / `RUSTFS_SECRET_KEY`，作业设 `XIHAN_TEST_S3_ACCESS_KEY` / `XIHAN_TEST_S3_SECRET_KEY`，都写测试专用的固定值 `rustfsadmin`（与第四轮本机实测、文档站的 `docker run` 命令相同，只给 CI 的一次性容器）。不写而靠两边的缺省：否决，镜像的缺省以后可能变。
6. **健康检查**：`curl -fs http://localhost:9000/health`，间隔 5 秒、10 次，与 Redis、PostgreSQL 同一节奏；ADR-0086 实测过这个端点。
7. **建桶**：不另建，测试自己建。
8. **`XIHAN_TEST_S3_UNCONDITIONAL`（不认条件写入的服务，如 Zenko）**：不在这次范围，照旧跳过；`TestShardTests` 不管没有分片提供的变量。

### Oracle 作业

9. **只放宽期限**：否决（拍板）。
10. **按类与方法名把 Oracle 用例拆成三个并行的作业，各起一个 Oracle**：采纳。`rest` 跑 `oracle` 分片两个项目里除引擎与任务两个 Oracle 类之外的全部（现有的 17 条，与现在的作业一样约 13 分钟）；`workflow-a` 跑任务类的全部与引擎类里方法名以 A–H 开头的 15 条（共 24 条，估计 15 分钟左右）；`workflow-b` 跑引擎类其余的 27 条（估计 17 分钟左右）。三份过滤器互补：`rest` 用 `--filter-not-class` 排除两个类，`workflow-a` 用 `--filter-method` 选，`workflow-b` 用 `--filter-class` 加同一组 `--filter-not-method`。本机核对过 xUnit v3 在 MTP 下的语义：同类的正向过滤器取并、反向取交，不同类取交，选出与排除的合起来恰好是全部；过滤器选不出用例时测试程序以退出码 8 失败。引擎类的新用例按方法名的首字母必落进 a 或 b，不会漏掉；类改了名，选它的那份失败、排除它的那份照样跑它；过滤器里的类名另由架构测试核对（决策 13）。
11. **一个作业里让 Oracle 的集合并行**：否决。`OracleGroup` 不并行是因为建用户与建表都是同步的阻塞调用，几个类同时跑会占住线程池、把同一进程里 SQLite 宿主的启动拖过期限（它的说明里写着）。
12. **拆成按项目的两片**：否决。`Workflow.Database.Tests` 一个项目就有六十来条 Oracle 用例，仍要半个多小时。
13. **在分片表里写过滤器**：否决。过滤器只在 CI 拆作业时有意义，本机跑一片（`New-TestShard.ps1`）照旧跑全部；写在 `ci.yml` 的矩阵里，组合动作 `test-shard` 多两个可选输入：`filter` 原样追加在 `dotnet test` 之后（关掉路径名展开，`*` 是 xUnit 的通配符），`part` 进测试结果的产物名，免得同一片的两份重名。拆出的引擎两份要一个只含 `Workflow.Database.Tests` 的分片，否则另一个项目选不出用例、以退出码 8 失败：分片表新增 `oracle-workflow`（`XIHAN_TEST_ORACLE`），一个项目可以在几片里（`database` 与 `oracle` 本来就共用这两个项目）。

### 插件一侧的两类核对

14. **读不出的 `time` 放进哪个用例**：放进 `unsubscribed`（采纳）。它本来就核对「宿主写来的内容不合法」那一步（未知的方式、没有信封），读不出的 `time` 同属第 2 步；向没人订阅的随机主题投递，答 `plugins.event.unsubscribed` 即说明 SDK 把它排到了找订阅之后。不必有订阅，任何插件都能核对；用例名单不变，五个写死名单的 SDK 测试不受影响。另开一个用例：否决，多一个进程、多一处名单要改。
15. **投哪些文本**：规范向量 `envelope-times.json` 的 `invalid` 与 `sdk-envelope-times.json` 的 `unreadable`，逐条（采纳）。只投一两条自拟的文本：否决，规范已经列出 SDK 必须读不出的全部写法。合文法而超出某种语言时间类型的时刻（Windows 上 Rust 的 `SystemTime`）各平台不同，不收。.NET 套件把这两份向量随包内嵌（`EmbeddedResource`，与 `Cli` 内嵌规范正文同一做法），不另抄一份；读取先过 JSON 文本的共同检查。
16. **黑盒怎么看到作者拿到的 `dataschema`**：被测插件声明一个回显信封的操作，运行器以 `--envelopes <操作 ID>`、套件以 `EnvelopeOperation` 指明它（采纳）。载荷是 JSON 对象，`envelopes` 是数组，每一项 `id` 与 `dataSchema`（没有时为 `null`、空串或不写）。否决的有：运行器提供 `Host` 服务、让处理器把信封经 `Host.Publish` 发回来（运行器要监听两种回调端点、核对会话与权限，被测插件仍要写一段只为一致性的发布）；从 stderr 的日志里找（日志的内容不是协议）；处理器以失败把信封带进错误细节（把业务失败当回显通道）；保留一个约定的操作 ID 或主题（进插件的清单，与业务操作混在一起）。
17. **没有指明回显的操作时**：跳过（采纳），与没有订阅时 `event` 跳过一样。各 SDK 的黑盒测试都把跳过当失败，升规范指针时会看到这一条、给 `examples/echo` 补上回显。悄悄当作通过：否决，那就没有核对。
18. **投什么 `data_schema`**：三条，依次是 `HTTPS://Schemas.Example.COM/conformance/./v1/../order.json?v=1#Frag`（URI 库会改写大小写与点段）、`urn:xihan:conformance:数据模式`（非 ASCII 字符）与空串（没有）；作者拿到的必须逐字符相同（ADR-0305「SDK 原样交给作者，不另解析」）。`time` 取 `envelope-times.json` 的 `written` 里第一个有时刻的文本（宿主的写法），把一律拒绝非空 `time` 的 SDK 也拦下来。只有空白的值规范不作规定，不投。
19. **Wasm 套件**：不加这两类。Wasm 插件不经 SDK 的 gRPC 运行时，`time` 与 `dataschema` 由客体自己读，ADR-0305、0306 的要求是对进程外 SDK 的。

### 示例请求的编码

20. **编不成即用例按配置错误失败**：抛 `InvalidOperationException`，消息写明操作 ID 与改 `SampleRequests`（采纳），与插件可执行文件找不到时的配置错误同一种异常；编码照仓库已有的严格编码（`UTF8Encoding(throwOnInvalidBytes: true)`，同 `PluginJsonCodec`、`ResultJson`）。两个套件共用一个内部类型 `SamplePayloads`。照发 U+FFFD：否决（RFC-0001「写出与转交」）。跳过那个操作：否决，配置错了应当看得见。

### 临时文件被占用

21. **`DataSourceRegistry` 释放时等后台探测**：采纳。登记时立即开始的探测与定时器的那一轮都记下来（加入时清掉已完成的，至多是登记数加一），`DisposeAsync` 取消之后先等它们结束、再释放数据源；释放返回时不再有本注册表开的连接留在库上，也不会先释放了探测还在用的数据源。探测本来就吞掉自己的异常，等它不会抛出；Provider 不响应取消时，等到它的探测返回为止（每次探测仍有 `HealthProbeInterval` 的期限）。只在测试里删文件之前重试：否决，那是掩盖一个真实的泄漏——生产里应用停止时，库文件同样可能在释放之后还被探测占着。替换与移除时等旧实例的探测：这次不做，旧实例的探测结果本来就丢弃，SqlSugar 的探测另建客户端、不碰被释放的实例，最晚在注册表释放时等到。
22. **`PluginOutputLimitTests` 删目录时稍等再删**：采纳，最多 50 次、每次 100 毫秒，仍删不掉照常抛出；测试类改为 `IAsyncDisposable`。与几个 SQLite 夹具删库文件的做法相同。在宿主里等整棵进程树退出：否决，`Process` 拿不到已经被终止的子进程的退出，要另起进程快照，为一个推断的原因改生产代码不划算。
23. **加公开的临时目录工具到 `XiHan.Framework.Testing`**：否决。两处偶发里一处的根因在框架，另一处只此一个用例要等进程树；其余十几处 `CreateTempSubdirectory` 没有偶发记录，公开包不为它们多一个类型。

## 决策

### 规范

1. `plugin/invocation.md` §7（MINOR，只增核对，SDK 的义务不变）：套件与运行器的用例列表补上读不出的 `time` 与 `dataschema` 交给作者；新增一段写清两类怎样驱动与断言（决策 14–18 的投递、文本、回显操作的载荷形态与比较方式，没有指明或没有订阅时跳过），并把「SDK 读写信封的 `time` 由 SDK 实现的测试覆盖」改成「取值由 SDK 实现的测试覆盖」。RFC-0017 状态行追加同日补充，对应 ADR 加 0327。
2. 运行器（`conformance/plugin`）：`unsubscribed` 逐条投递两份向量里读不出的文本；新增用例 `event-envelope`（`CaseEventEnvelope`，排在 `event` 之后）与选项 `Options.EnvelopeOperation`、命令行 `--envelopes`；`TestAgainstAPluginDirectory` 认 `XIHAN_CONFORMANCE_ENVELOPES`。`fake_test.go`：合规的假插件声明 `events.list` 回显信封、读 `time`，通过全部用例；`legacy` 的处理器拿不到 `dataschema`，在 `event-envelope` 上失败；新增只是不读 `time` 的 `lenient-time`，只在 `unsubscribed` 上失败；不给回显操作时 `event-envelope` 跳过。`conformance/README.md` 的用例表、选项表与技能参考同步。

### 框架

3. `Plugins.Conformance`：新增 `protected virtual string? EnvelopeOperation`（缺省 `null`）与用例 `HandleEvent_HandsTheDataSchemaToTheHandler`；`Unsubscribed_DeliveryWithoutASubscription_IsNotFound` 逐条投递读不出的 `time`；两份向量随包内嵌；两个套件的 `SampleRequests` 经 `SamplePayloads` 严格编码。README 同步。
4. 示例：EchoPlugin 按 RFC-0003 规则 8 读宿主写来的 `time`（`EnvelopeTime`，示例不引用 XiHan 包，自己按规范读），读不出以 `InvalidArgument` 拒绝、排在找订阅之前；`events.list` 多回 `envelopes`（`id` 与 `dataSchema`，没有为 `null`）；`EchoPluginConformanceTests` 指明 `EnvelopeOperation => "events.list"`。EchoGoPlugin 的 Go SDK 由 `d14d5f8`（ADR-0306 之前）升到 `d2f3962`（已推送），它读 `time`；它的 echo 还没有回显操作，`event-envelope` 跳过。
5. CI：integration 作业起 `rustfs/rustfs:1.0.6`、设 `XIHAN_TEST_S3`，分片表把 `ObjectStorage.S3.Tests` 列进 integration；oracle 作业照决策 10、13 拆成 `rest`、`workflow-a`、`workflow-b` 三份，期限仍是 30 分钟。
6. 架构测试 `TestShardTests` 新增 `WorkflowFilters_NameTestClassesThatExist`：`ci.yml` 里 xUnit 类与方法过滤器提到的类都要有测试项目声明它。
7. `DataSourceRegistry.DisposeAsync` 照决策 21 等后台探测；类的说明补「释放」一段。`PluginOutputLimitTests` 照决策 22。

## 影响

- 行为（`Persistence.Abstractions`，Preview）：`DataSourceRegistry.DisposeAsync` 现在等进行中的后台探测结束再释放数据源、再返回，以前不等。Provider 的探测不响应取消时，释放随之变慢，最多到那次探测自己的期限。公开面不变。
- 公开面（`Plugins.Conformance`，Preview）：新增 `EnvelopeOperation` 与一个用例；`SampleRequests` 里有孤立代理项的派生类以前照发 U+FFFD，现在用例以 `InvalidOperationException` 失败——迁移：改正样例。插件 SDK 还没有把 `time` 读不出排在找订阅之前的，`unsubscribed` 现在失败。
- 规范：运行器多一个用例、多一个选项，`AllCases` 多一项；各 SDK 升到这个规范提交时，黑盒测试会看到 `event-envelope` 跳过（写死名单的五个还会看到名单不符），要给 `examples/echo` 补回显操作并传 `--envelopes`（报告「SDK 要跟的」）。规范的指针由整合统一升。
- CI：integration 作业多一个服务容器，估计长 1 分钟左右；oracle 由一个作业变成三个，每个都要拉镜像、起 Oracle、还原与构建（约 2 分钟），总的运行器时间增加，最长的一份估计 17 分钟左右。
- 测试：`ObjectStorage.S3.Tests` 由 core 移到 integration（不连服务的用例照样跑）。新增运行器自测 3 个（合规与 `legacy` 两个假插件的用例照旧，覆盖面扩到新用例）、`DataSourceRegistryTests` 1 个、`TestShardTests` 1 个、样例编码的复现用例 2 个（进程外与 Wasm 各一）。

## 没有解决的

- CI 第一次跑 RustFS 时可能红的地方：1.0.6 的 `/health` 与凭据环境变量的名字没在 CI 上起过；ADR-0308、0317、0322 新加的不合规应答判定（缺 `Content-Length`、列举缺 `<Size>`、SDK 解析不了的头、整读内容对不上 `ETag`）在真实 RustFS 上第一次跑，RustFS 若对某种请求（如分块传输的读取）不带 `Content-Length`，整读会以 `OBJECTSTORAGE_UNAVAILABLE_001` 失败；健康探测、预签名直连在 GitHub 运行器的回环地址上第一次跑。
- Oracle 每条用例的时长按现有用例的平均推算，引擎套件的用例若明显更慢，`workflow-b` 要再拆；拆法照决策 10 在 `ci.yml` 的矩阵里多加一份互补的过滤器。
- `PluginOutputLimitTests` 的根因是推断；若加了重试仍偶发，要在失败时把占着句柄的进程记下来再查。
- 六个 SDK 的 `examples/echo` 补回显操作之前，`dataschema` 交给作者在 SDK 一侧仍只有单元测试核对。
