# ADR-0091 调度排空释放领导租约、EF Core 命名连接只绑定连接与 SQLite 时间整数存储

## 状态

已采纳（`spec/rfcs/0013-scheduling.md`、`spec/rfcs/0005-persistence.md`、`spec/conventions/error-codes.md` 随之修订；完成 ADR-0087 第一节「影响」里调度引擎的待定项；把 ADR-0087 第二节备选 3（普通工作单元里的命名连接）定为不做；EF Core 在 SQLite 上的时间列由框架代办，替换第五批让应用自己配转换的做法）。

## 日期

2026-11-01

## 背景

第五批收口时数据分区留下几项待定（`i-round-summary.md` 第 5 节、`i-data.md` 第 4 节），用户逐条拍板，全按推荐（`j-batch-backlog.md`）。本 ADR 记数据相关的几项，每项一节。

## 一、调度引擎在应用不在服务状态时不持有领导租约，数据库实现的租约释放不经管道

### 背景

ADR-0087 第一节让数据库工作流在应用不在服务状态时释放轮询领导权，同时记下调度引擎有同类情况：`JobSchedulerEngine` 不在服务状态的周期直接返回，不续约也不释放，租约要等 `LeaseTimeToLive`（缺省 30 秒）到期或调度特性停止。

复现时发现数据库实现更糟：特性停止时的释放也没生效。`DatabaseLeaderLease.ReleaseAsync` 经 `LeaseRows` 走 `StoreInvocation`，即 `App.InvokeAsync`；特性停止时应用已在排空（第一个特性开始停止，应用状态就是 `Draining`），管道直接拒绝调用，释放返回 `SCHEDULING_UNAVAILABLE_003`，记一条 `Releasing the leader lease for 'scheduling.jobs' failed …; it expires on its own.` 告警，行留到租约到期。也就是说数据库实现每次正常停机都要另一个节点等满 30 秒，还多一条告警。内存实现的释放是进程内的，Kubernetes 实现的释放是一次 API Server 调用，都不受应用状态影响。

### 备选

1. 维持现状：调度的领导权只管维护（失联节点恢复、历史清理）与不共享存储时的按计划运行，晚几十秒影响不大。可数据库实现「停止时释放」的说法根本不成立，每次停机都记告警。
2. 只修数据库的停止释放，引擎不变：排空期间仍占着租约，要等调度特性自己停止，排空长时（依赖调度的特性多、停止超时长）照样挡住别人。
3. 照数据库工作流的做法（ADR-0087 第一节）：周期入口看应用状态，不在服务状态就不拿不续、持有就立即释放；数据库实现的释放改为不经管道，否则排空中与停止时都删不掉行。
4. 数据库实现的租约三步（获取、续约、释放）都不经管道，与工作流的租约表一样：获取与续约只在服务状态进行，不需要绕开管道；改动面更大，「宿主停止后选举不可达」的现有语义也要跟着变。

### 决策

选 3。

- `JobSchedulerEngine` 每个周期先看 `App.State.IsServing()`：为假就释放持有的租约（没持有时什么也不做）后返回；停止时的释放与周期在同一把轮询互斥里（等待不用停止令牌：宿主不再等待时令牌已取消，用它等会直接抛出、租约就留到到期）。释放先把租约从引擎上摘下再调用 `ReleaseAsync`，记 Information 日志 `Released the leader lease for '{Resource}'.`。
- `Scheduling.Database` 的 `LeaseRows.ReleaseAsync` 不经管道：在根提供者的新作用域里开可写工作单元，按令牌找到自己的行删掉再提交；行没了、令牌变了、提交时撞上别的节点都算已释放，异常归为存储不可达。获取与续约照旧经管道。
- RFC-0013：引擎第 1 步、生命周期一段与数据库 Provider 一段写明；`LeaderLease.Release` 在应用不在服务状态时也必须生效。

### 理由

- 不在服务状态的节点本来就不按计划运行、不做维护，持有租约没有任何用处，只会挡住在服务的实例；排空开始后至多一个轮询周期（缺省 1 秒）就释放。
- 释放是唯一必须在应用不在服务状态时进行的租约操作：租约行是平台数据，不需要 Tenant / Authorize 阶段，同包的健康探针（`LeaseProbe`）已经这样直接开工作单元。获取与续约留在管道里，存储不可达的归类与「宿主停止后不可达」的现有语义都不变。
- 与数据库工作流的轮询领导权同形，运维看到的是同一种行为。

### 影响

- 公开 API、配置、表结构不变。新增一条 Information 日志。
- 行为：排空中的节点在下一个周期释放调度租约，另一个节点随即接手；数据库实现的停止释放真正删行，不再记 `Releasing the leader lease … failed with 'SCHEDULING_UNAVAILABLE_003'` 告警。启动中的节点原来就不拿租约，不变。
- 测试：内存一例（探针特性依赖 `scheduling.jobs`、在调度之后启动之前停止，注入时钟、经 `PollOnceAsync` 直接执行周期：启动中不拿；服务中拿到、另一个持有者拿不到；排空中时钟走过半个租约，这个周期释放，另一个持有者随即拿到）；数据库两例（两个节点共用一个 SQLite 文件与一个时钟：同样的排空用例，另断言没有调度的告警；停止前没有排空周期时，停止即删行，时钟不动另一个节点立刻拿到）。先写用例：修前三例都失败；只改引擎时数据库两例仍失败（释放经管道删不掉行），再改 `LeaseRows`。

## 二、EF Core 的命名连接只支持绑定连接的工作单元，另一半不补

### 背景

ADR-0087 第二节让 EF Core 支持命名连接，只做了「绑定连接的工作单元」这一半：一个 `DbContext` 一个连接，登记到命名连接的实体只能在绑定那个连接的工作单元里读写，普通工作单元里取用抛 `PERSISTENCE_UNAVAILABLE_008`；SqlSugar 则在普通工作单元里照常让这样的实体参与整体事务。另一半（ADR-0087 第二节备选 3）当时记为待定。

### 决策

不补，维持明确报错（用户拍板）。RFC-0005「命名连接」的 EF Core 一行、Provider 表、`error-codes.md` 的 `PERSISTENCE_UNAVAILABLE_008`、docs 持久化指南与 EF Core 包页、EF Core 包 README 写明已定：不做每个连接一个 `DbContext`、跨上下文顺序提交的多上下文工作单元；要在业务事务里一起读写命名连接上的实体，用 SqlSugar Provider。顺带改正 docs `persistence-abstractions` 页里「只有 SqlSugar 支持命名连接」的旧说法。

### 理由

- 框架里用到命名连接的只有工作流租约表与它的健康探针，都在自己的作用域里开绑定连接的工作单元；普通工作单元里的跨连接取用没有使用方。
- 补齐要按连接再建 `TContext`（反射构造或应用提供工厂），工作单元、仓储、残余变更与唯一键核对都改成多上下文，还要回答跨上下文提交到一半失败怎么办——为没有使用方的能力付这些代价不值。
- 明确报错不会把实体静默写到默认库，应用在第一次取用时就知道要换写法或换 Provider。

### 影响

无代码、配置与行为变化，只有文档与规范措辞。

## 三、EF Core 在 SQLite 上把 DateTimeOffset 存成 UTC ticks 整数

### 背景

EF Core 的 SQLite Provider 把 `DateTimeOffset` 存成文本，翻译不了大小比较与 `ORDER BY`（`SQLite does not support expressions of type DateTimeOffset in ORDER BY clauses`）。数据库工作流的到期书签、实例与任务列表都按时间过滤和排序，第五批让应用自己在 `ConfigureConventions` 里配 `DateTimeOffsetToBinaryConverter`（文档与测试的 `WorkflowDbContext` 都这样写）。用户拍板：框架代办，应用不用自己配。复现：去掉测试 `WorkflowDbContext` 里的转换，EF Core + SQLite 上的工作流引擎、任务一致性套件与 EF Core 工作流用例 41 例全部失败。

### 备选

作用范围：

1. 只转换框架自己的表（工作流、调度、审计、授予等）：要在 `ModelBuilder` 上认出哪些是框架实体（名单或按命名空间判断），同一个 `DbContext` 里应用实体的 `DateTimeOffset` 照样不能比较与排序，框架的通用仓储（按时间过滤、分页）对应用实体仍然坏着。
2. 模型里全部实体（框架表与应用实体），应用已经配了值转换或存储类型的属性不动。

存储格式：

1. EF Core 自带的 `DateTimeOffsetToBinaryConverter`：把本地时钟的 ticks 除以 1000 左移 11 位再并上偏移分钟——精度 100 微秒，比 SqlSugar 粗；整数按「本地时钟」排序，偏移不同的值之间不保序（只在全是 UTC 时恰好保序）。
2. Unix 毫秒：精度 1 毫秒，比 SqlSugar 粗，同一时刻经两个 Provider 往返结果不同。
3. UTC ticks（`DateTimeOffset.UtcTicks`，读回偏移为零）：100 纳秒，与 SqlSugar 在 SQLite 上存的 UTC 日期时间文本（7 位小数）同精度；随时刻单调，任何偏移写入的值之间都保序。

### 决策

范围选 2、格式选 3。`ModelBuilderExtensions.ApplyPersistenceConventions(Database)` 在 Provider 是 SQLite（`Microsoft.EntityFrameworkCore.Sqlite`）时，给模型里每个实体（含从属类型、共享类型与复杂类型）声明的 `DateTimeOffset` 与 `DateTimeOffset?` 属性设值转换 `UtcTicksConverter`（内部类型，`INTEGER` 列）；属性已有值转换（`HasConversion`、`ConfigureConventions` 的 `HaveConversion`）或存储类型的不动；其余 Provider 不动。RFC-0005 的 Provider 表写明。

### 理由

- 与 SqlSugar 对齐：SqlSugar 对每个实体的每个 `DateTimeOffset` 列都按 UTC 往返（`DateTimeOffsetConverter`），不分框架表与应用表；同一份实体换 Provider，时间的精度与语义不变。
- 只转换框架表需要一份会过期的名单，还把同一个 `DbContext` 分成能按时间查与不能按时间查两半。
- 显式配置优先于约定，这是 EF Core 自己的规则；原来照文档配了 `DateTimeOffsetToBinaryConverter` 的应用库不用动。

### 影响

- 公开 API、配置不变；新增内部类型 `UtcTicksConverter`。
- 行为：EF Core + SQLite 上 `DateTimeOffset` 列建成 `INTEGER`，读回的值偏移为零（原来保留写入时的偏移文本）；比较与排序按时刻。EF Core + SQLite 跑数据库工作流不再需要应用自己配转换。
- 已有的 SQLite 库（实测 Microsoft.Data.Sqlite 10.0.11）：原来没配转换的列存的是 EF Core 的文本（`2026-11-01 08:00:00+08:00`），升级后按整数读，**旧行被静默读成 `0001-01-01T00:00:00.0002026` 这类错误时刻，不报错**（SQLite 把文本开头的 `2026` 当整数）。开发与测试库删了重建；要留数据的重建表（SQLite 改不了列类型，EF Core 迁移改列类型就是重建表），搬数据时把文本换算成 UTC ticks，换算式（实测往返一致）：

  ```sql
  (unixepoch(substr(col, 1, 19))
   - (CASE substr(col, -6, 1) WHEN '-' THEN -1 ELSE 1 END) * (CAST(substr(col, -5, 2) AS INTEGER) * 3600 + CAST(substr(col, -2, 2) AS INTEGER) * 60)) * 10000000
  + CASE WHEN substr(col, 20, 1) = '.' THEN CAST(substr(substr(col, 21, length(col) - 26) || '0000000', 1, 7) AS INTEGER) ELSE 0 END
  + 621355968000000000
  ```

  只就地 `UPDATE` 不重建表时，列的声明类型仍是 `TEXT`，整数按文本存，比较退化成等长数字串的文本比较，不要这样留着。原来自己配了转换的列不受影响。
- 测试：EF Core 测试项目新增四例（SQLite 上存成整数、不同偏移写入的值按时刻比较与排序、可空列判空与比较、100 纳秒往返与偏移为零、库里存的就是 UTC ticks；应用自己配的转换保留；复杂类型也转换、其余 Provider 不转换；转换器的边界值）；工作流测试的 `WorkflowDbContext` 去掉手工转换后 EF Core + SQLite 的引擎、任务一致性套件与工作流用例 41 例全部通过（修前全部失败）。

> 修订（2026-11-02）：第三节「影响」里已有 SQLite 库被静默误读的风险，改为 EF Core 特性启动时核对、库里还是旧的存储形式即启动失败（`PERSISTENCE_VALIDATION_008`），见 ADR-0094 第一节；第三节换算式里丢失的引号（`'-'`、`'.'`、`'0000000'`）已补上，补后的式子由 EF Core 测试逐字执行验证。

> 修订（2026-11-02）：第一节「影响」里内存与数据库各自的排空用例并入调度一致性套件的应用生命周期用例（`LeaderElectionConformance.StartNodeAsync`，三个实现都通过），见 ADR-0094 第二节。
