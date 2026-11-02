# ADR-0097 SQLite 驱动钉版本、HTTP 活动超时按令牌归因与分片库时间列首绑核对

## 状态

已采纳（`spec/rfcs/0005-persistence.md`、`spec/rfcs/0012-objectstorage.md`、`spec/rfcs/0021-workflow.md`、`spec/rfcs/0022-ai.md`、`spec/conventions/error-codes.md` 随之修订）。修订 ADR-0094 第一节：租户分片库由「不核对」改为首次绑定时核对。ADR-0095 第二节的流式计时与 ADR-0093 第一节「启动之后才被拒绝或缺桶」的处理定为已定；ADR-0095 第二节的令牌归因推广到工作流的 `Http` 活动。

## 日期

2026-11-02

本 ADR 记录第八批回灌里第七批待定项的五项决定（`l-batch-backlog.md` 二之 1–5，来由见第七批 `k-data` 第 4 节、`k-host` 第 4 节、`k-res` 第 4 节），每项一节。二之 6（SqlSugar 的上游 issue）由用户自己处理，框架不动。

## 一、Microsoft.Data.Sqlite 钉到 10.0.13（二之 1）

### 背景

第七批核实了 Microsoft.Data.Sqlite 连接池激活的竞态（dotnet/efcore#39008）：`SqliteConnectionInternal.Activate` 先置活动标记、再记外层连接，两步之间这条连接被当作泄漏——并发打开时两个连接可能拿到同一个原生连接（"cannot start a transaction within a transaction"），清池时正在打开的连接被释放（`ObjectDisposedException`）。上游在 10.0.13 修复（#39009 / #39012），第七批的独立复现程序在 10.0.11 上清池 15 秒失败 20 次、10.0.13 上 0 次。

框架里用 SQLite 的有两条路：

- `Persistence.SqlSugar` 依赖 `SqlSugarCore` 5.1.4.221，它声明 `Microsoft.Data.Sqlite >= 10.0.11`。NuGet 按下限解析，框架里用 SqlSugar 的测试项目、引用本包的应用都用着 10.0.11，带着这个竞态。
- `Persistence.EntityFrameworkCore` 不依赖任何 SQLite 包；用 SQLite 的应用自己引用 `Microsoft.EntityFrameworkCore.Sqlite`，10.0.13 带来 `Microsoft.Data.Sqlite.Core` 10.0.13。

### 备选

1. 只在 `next/Directory.Packages.props` 钉版本：仓库里开着中央传递固定（`CentralPackageTransitivePinningEnabled`），框架自己的项目会解析到 10.0.13，打包时固定的传递依赖也会进 nuspec；但这依赖一个仓库级开关的打包副作用，意图不在包的依赖声明里。
2. 钉版本，并由 `Persistence.SqlSugar` 显式引用 `Microsoft.Data.Sqlite`（采纳）。
3. 同时让 `Persistence.EntityFrameworkCore` 依赖 `Microsoft.Data.Sqlite.Core` 10.0.13：用 SQL Server、PostgreSQL 的 EF Core 应用平白多一个 SQLite 程序集与 `SQLitePCLRaw.core`。
4. 不动，文档让应用自己钉：SqlSugar + SQLite 的应用多半不知道要钉。

### 决策

- `next/Directory.Packages.props` 新增 `Microsoft.Data.Sqlite` 10.0.13；`Persistence.SqlSugar` 显式引用它（`SqlSugarCore` 依赖的就是这个元包，不是 `.Core`）。打出的 `XiHan.Framework.Persistence.SqlSugar` nuspec 依赖里有 `Microsoft.Data.Sqlite` 10.0.13；引用本包的应用解析到 10.0.13，连带 `Microsoft.Data.Sqlite.Core` 10.0.13 与 `SQLitePCLRaw` 2.1.13（都是 `Microsoft.Data.Sqlite` 10.0.13 自己声明的依赖，没有升级别的包）。
- `Persistence.EntityFrameworkCore` 不加 SQLite 依赖；README 与 docs-next 包页写明 SQLite 用 `Microsoft.EntityFrameworkCore.Sqlite` 10.0.13 或更高。
- 不进规范：依赖的版本下限是打包事实，不是契约。

### 理由

- 修复只在驱动里，框架没有办法绕过；按下限解析是 NuGet 的规则，只有把下限写进本包的依赖才能带到使用方。
- 显式引用让意图留在包的依赖声明里，不依赖仓库的中央传递固定开关。
- 不用 SQLite 的 SqlSugar 应用不多出依赖：`SqlSugarCore` 本来就带着 `Microsoft.Data.Sqlite`，只是下限变高。EF Core 的 SQLite 版本由应用自己的驱动引用决定，框架不替它选。

### 影响

- 核对：仓库里引用 SQLite 的 10 个测试项目还原后都是 `Microsoft.Data.Sqlite(.Core)` 10.0.13 / `SQLitePCLRaw` 2.1.13，与改动前相比只有这一族变了；`dotnet pack` 80 个包里只有 `Persistence.SqlSugar` 声明 SQLite 依赖；本地包源上只引用 `Persistence.SqlSugar` 的消费程序解析到 10.0.13，第七批的竞态复现程序 `clear-all` / `clear-own` 各 15 秒 0 次 `ObjectDisposedException`。
- 应用自己直接引用了更低版本（如 `Microsoft.Data.Sqlite` 10.0.11）时，还原报降级 `NU1605`（SDK 缺省当错误，实测）：删掉那条引用或抬到 10.0.13。
- 测试夹具「只清本库连接池」（第六批）在 10.0.13 上仍然是对的，不改回 `ClearAllPools`。

## 二、AI 流式按每次更新计时，定为已定（二之 2）

### 背景

ADR-0095 第二节给 `IAIChat.StreamAsync` 加了框架的计时：等每个更新（含第一个）的时长，只在等 Provider 时计时，调用方处理更新的时间不算，流的总时长不限；流里的超时抛出 `TimeoutException`。第七批收口时把它列为新加的行为，留待确认。

### 备选

1. 保留按每次更新计时（采纳）。
2. 整条流一个总时限：长回答、调用方处理得慢的流会在正常输出时被截断，`TimeoutSeconds` 要按最长的回答来配，失去对卡住连接的约束力。
3. 退回不计时：卡住的连接没有上限，流里的超时没有归因（Provider 自己的取消异常原样抛给调用方）。

### 决策

保留，不改代码。RFC-0022「超时与取消」、docs-next AI 指南、AI.Abstractions 包页与 README 写明已定；要限制一次对话的总时长，由调用方给自己的取消令牌或调用的截止时间。

### 理由

流式的价值是边生成边交付，计时要约束的是「Provider 卡住不出数据」，不是回答的长短；总时长本来就有调用方的令牌与截止时间可管。

### 影响

无行为变化。

## 三、工作流 HTTP 活动的超时按令牌归因（二之 3）

### 背景

`Http` 活动（`HttpActivity`）的超时原来在 `WorkflowHttpClient.SendAsync(request, timeout, ct)` 里按墙钟 `CancelAfter`，归因按「调用方令牌没有触发的取消即超时」推断（`catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)`）。与 ADR-0095 第二节处理 AI 之前同一形状：活动超时与调用方取消同时触发时原样抛出取消；应用替换的处理器接住取消、交回响应时当作成功；`TimeoutException` 是活动抛出的未处理异常。超时按墙钟计时，测试注入不了时钟。

### 备选

1. 维持原样：同时触发时报取消。
2. 活动持有超时令牌、按令牌归因（采纳）。
3. 在 `WorkflowHttpClient` 里计时并把「是否超时」交回活动：客户端可被应用替换，归因的依据不该放在可替换的那一层。

### 决策

- 活动自己持有超时令牌：`TimeoutSeconds`（缺省取引擎选项 `HttpTimeoutSeconds`）按 `ActivityContext.Clock`（引擎的 `TimeProvider`）落成定时取消，与调用方令牌（活动收到的、即这次调用的取消令牌）链接后交给客户端。`WorkflowHttpClient.SendAsync(request, timeout, ct)` 改为 `SendAsync(request, ct)`，客户端不另计时（PublicAPI 在 Unshipped，直接改，不留旧重载）。
- 归因只看令牌，客户端抛出与写成结果同一规则：
  1. 超时令牌已触发即超时，不论调用方令牌是否也已触发：客户端抛出的取消、处理器接住取消后交回的响应，都是节点故障 `WORKFLOW_UNEXPECTED_001`（消息键 `workflow.http.failed`，`details.status` 为 0、`details.reason` 为 `timeout`），响应不写进变量。
  2. 只有调用方取消：抛出的取消原样抛出，交回的响应不写进变量、改为抛出调用方的 `OperationCanceledException`；引擎把它当作调用被取消，整个操作随调用回滚，不记成节点故障。
  3. 都没触发：客户端抛出的取消是它自己的超时（应用替换的 `HttpClient` 设了 `Timeout`），按超时；`TimeoutException` 一律按超时；`HttpRequestException` 照旧是 `reason` 为异常消息的同码故障。
- 错误码、消息键、配置键都沿用，没有新增。RFC-0021「活动」一节新增「HTTP 活动的超时与取消」。

### 理由

- 与内核 `DeadlineMiddleware`（ADR-0092）、AI Provider（ADR-0095）同一原则：超时一旦发生就是这次请求的结论，不随调度报成取消。
- 时限由活动按注入的时钟计时，测试推进虚拟时间即可让请求超时，不靠真实等待。

### 影响

- 公开签名：`WorkflowHttpClient.SendAsync(HttpRequestMessage, TimeSpan, CancellationToken)` → `SendAsync(HttpRequestMessage, CancellationToken)`。只经容器替换 `WorkflowHttpClient` 实例的应用不受影响；直接调用 `SendAsync` 的要去掉 `timeout` 参数、自己给令牌。
- 行为：活动超时与调用方取消同时发生时，由抛出取消（调用以取消结束）改为节点故障（`reason: timeout`）；处理器接住取消后交回的响应由「成功」改为超时或取消；`TimeoutException` 的 `reason` 由异常消息改为 `timeout`。
- 测试：`Workflow.InMemory.Tests` 的 `HttpActivityTimeoutTests` 11 例（探针活动从引擎拿真实上下文、以用例控制的调用方令牌执行 HTTP 活动，`FakeTimeProvider` 推进时钟、挂起的处理器由用例放行）：只有超时 / 只有取消 / 先超时后取消 / 先取消后超时 × 抛出与交回响应，加上客户端自己的超时、`TimeoutException`、都没触发的正常响应；修前「两者同时」4 例全部失败（抛出路径得到 `TaskCanceledException`，交回响应路径得到成功）。

## 四、S3 启动之后才被拒绝或缺桶：继续重试、健康项 Failed，定为已定（二之 4）

### 背景

ADR-0093 第一节把启动或建实例时的被拒绝、缺桶恢复为启动失败，并定了「启动时连不上、之后的探测才被拒绝或缺桶」的处理：应用不停止、仍是尚未确认、后台按退避重试，修好自动确认；不覆盖写入仍是 `OBJECTSTORAGE_UNAVAILABLE_004`（HTTP 503，`reason` 带出原因）；缺省实例健康项 Failed、记 Error 日志。用户当时只要求「写清」，这个处理由分区定，留待确认。

### 备选

1. 维持（采纳）。
2. 停止重试、记为终态：权限改好或桶建好之后还要重启才能恢复。
3. 健康项只记 Degraded：与连不上分不开，看不出要人处理。

### 决策

维持，不改代码。RFC-0012「S3 的条件写入探测」、docs-next 存储指南与 S3 包页、S3 README 写明已定。命名实例的健康项按注册表的规则一律 Degraded（ADR-0090 已记），不为此改注册表。

### 理由

被拒绝与缺桶是配置问题，要人处理（Failed 区分出来），但修好之后不该再要求重启（继续重试）；写入方没有过错，不以 403 / 404 回给它。

### 影响

无行为变化。

## 五、SQLite 时间列核对扩到租户分片库：首次绑定时核对（二之 5）

### 背景

ADR-0094 第一节让 EF Core 特性启动时逐连接核对 SQLite 上由约定映射成 UTC ticks 的时间列，库里还是旧的文本形式即启动失败（`PERSISTENCE_VALIDATION_008`）——照读会被静默读成错误的时刻。租户分片库在调用时才解析、启动时枚举不到，没有核对：分片库是旧库时，这个租户的读写仍会静默读错。

### 备选

1. 维持不核对，让应用给分片库建表或迁移后自己调核对入口：要开公开 API，应用多半想不到调。
2. 首次绑定时核对、结果缓存（采纳）。
3. 每次开工作单元都核对：每个请求多一次读表结构与抽样。
4. 核对不符也记下（之后直接失败、不再查）：修好库之后要重启才能恢复。

### 决策

- 时机：工作单元开始（`BeginAsync`）时本作用域的 `DbContext` 建在租户分片上（Tenant 阶段写进调用的分片，或 `TenantShardScope` 显式绑定的分片；选项回调把分片绑定记进作用域的 `ContextConnection`），而这个分片库还没有核对通过，就在开始之前核对一次。范围（没有登记到命名连接的实体，与默认连接相同）、判定、不打开不存在的库文件都与启动时相同。命名连接的绑定优先于分片，不在此列。
- 只查一次：新的内部单例 `ShardTimestampChecks` 按分片连接串记下核对通过的分片，之后每次开工作单元只查一次字典（不加锁）；同一分片的并发首绑经每分片一个信号量排队，只有一个去查，其余等到之后看记录，各用自己的令牌与 `DbContext`（前一个取消或抛出时，下一个接着查）。不是 SQLite 的分片不建记录。
- 容量上限（收口时补，见下「理由」）：核对通过的记录最多 `XiHan:Persistence:EntityFrameworkCore:VerifiedShardCapacity` 条（`EntityFrameworkCorePersistenceOptions.VerifiedShardCapacity`，正整数，默认 1024，值不合法装配时即抛 `ArgumentException`、启动失败），记满淘汰最早记下的（先进先出）；被淘汰的分片下次绑定时重新核对一次，并发再绑同样只查一次。排队门只在有首绑在用时存在（使用者计数归零即移除并释放），条数不超过同时在核对的分片数。
- 不记下的情形：核对不符（每次绑定都重查、都失败，修好即恢复，不用重启）、库文件还不存在（`SqliteTimestampCheck.FindMismatchesAsync` 改为返回 `null` 区分「没有可查的」，下次绑定再查）、核对抛出异常（打不开库、忙等超时、调用取消）。
- 结果：不符抛出携带 `PERSISTENCE_VALIDATION_008` 的 `FrameworkException`，工作单元保持未开始；新消息键 `persistence.schema.shard_timestamp_format_mismatch`（参数 `tenant`、`columns`，不带连接串，10 种区域）。经管道的调用在 Transaction 阶段抛出，由 Observe 阶段转为 `KERNEL_UNEXPECTED_001`（HTTP 500，原因是本码）——与 `BeginAsync` 里其余装配类错误一样；这是服务端的数据问题，不按 VALIDATION 回 400。
- SqlSugar 不核对：它的 `DateTimeOffsetConverter` 在 SQLite 上一直存 UTC 日期时间文本（ADR-0013 起写入没有变过，ADR-0037 只改了 PostgreSQL 的读取），读取按整段文本解析（`DateTime.Parse`），不是取开头的年份；库里是整数或浮点数（如 EF Core 建的库）时转换抛出 `InvalidCastException`，读取失败而不是读错。没有同类的静默误读。

### 理由

- 分片库的旧数据与默认库一样会被静默读错，只是启动时看不到；首次绑定是框架第一次能看到它的时候。
- 核对开销与启动时相同（每库一次读表结构加每表至多 1000 行抽样），缓存之后摊到这个分片的第一次调用上；排队保证并发首绑不重复查。
- 不符不缓存：失败本来就要人修，修好之后不重启即恢复，与 ADR-0093 对 S3「修好自动恢复」同一取舍；代价是坏掉的分片每次调用都重查一次，只落在这个分片自己的调用上。
- 记录要有上限（仓库约定：缓存、注册表不得无界增长）：分片库数由应用决定，进程内的记录不能跟着无界增长。淘汰的代价只是被淘汰的分片再核对一次（每库一次读表结构加每表至多 1000 行抽样），语义不变，所以用最简单的先进先出：快路径只读、不记访问先后。上限做成配置键而不是常量：SQLite 分片库很多的应用可以调大，避免活跃分片数超过上限时反复核对。

### 影响

- 启动期没有新的失败条件；运行期新的失败：EF Core + SQLite 的租户分片库里由约定映射成 UTC ticks 的列声明为 `TEXT`（含空表）或最早写入的 1000 行里有非整数值时，这个分片上的调用都以 `KERNEL_UNEXPECTED_001` ← `PERSISTENCE_VALIDATION_008` 失败，别的租户与不带分片的调用不受影响。修法同 ADR-0091 第三节的换算式，修好之后下一次调用即恢复。
- 内存：核对通过的记录至多 `VerifiedShardCapacity` 条（默认 1024，每条是一个分片连接串）；排队门至多是同时在核对的分片数。
- 公开签名：`PersistenceMessages.ShardTimestampFormatMismatch` 与 `EntityFrameworkCorePersistenceOptions.VerifiedShardCapacity` 新增（只增）；`EntityFrameworkCoreUnitOfWork` 的内部构造多一个参数。新配置键 `XiHan:Persistence:EntityFrameworkCore:VerifiedShardCapacity`（CLI 知识库 `configuration.json` 同步），值不合法启动失败。没有新错误码、表结构变化。
- 不经工作单元、在作用域里直接解析 `DbContext` 读写分片的不在范围内。
- 测试：EF Core 测试项目的 `EntityFrameworkCoreShardTimestampCheckTests` 3 例（处理器不碰库，分片上读写连接的打开次数就是核对次数）：旧库分片每次绑定都失败、别的租户照常、按换算式修好之后下一次绑定即通过且读回同一时刻、之后不再核对；合格分片 8 个并发首绑只核对一次（第一个核对打开连接后挂起，8 个都进入核对之后才放行），之后的绑定不碰库；分片库文件还不存在时不建出文件、不记下，放进旧库之后下一次绑定即失败。修改验证：去掉排队门，并发用例 3/3 失败。容量（收口时补）：上限 1 时两个分片交替绑定，记满后照常工作、被淘汰的分片再绑定重新核对一次且之后只看记录、被淘汰的分片上 8 个并发再绑只核对一次；`VerifiedShardCapacity` 取 `0`、`-1`、`1e3`、` 8`、`many` 都启动失败、消息带配置键。修改验证：不淘汰时容量用例失败，去掉排队门时两个并发用例都失败。

> 修订（2026-11-02）：第五节「经管道是 `KERNEL_UNEXPECTED_001`（HTTP 500）、原因码 008」由 ADR-0099 第三节定为已定：不转成 400 的同码结果，也不另立码。
