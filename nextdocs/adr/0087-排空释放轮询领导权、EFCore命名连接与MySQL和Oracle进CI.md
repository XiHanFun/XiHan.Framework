# ADR-0087 排空释放轮询领导权、EF Core 命名连接与 MySQL 和 Oracle 进 CI

## 状态

已采纳（`spec/rfcs/0005-persistence.md`、`spec/rfcs/0021-workflow.md`、`spec/conventions/error-codes.md` 随之修订；修订 ADR-0084 第一节（EF Core 的命名连接）、第二节（排空期间的领导权））。

## 日期

2026-11-01

## 背景

第四批收口时数据分区留下几项待定（`h-round-summary.md` 第 4、5 节、`h-data.md` 第 4 节），用户逐条拍板，全按推荐（`i-batch-backlog.md`）。本 ADR 记数据相关的几项，每项一节。

## 一、数据库工作流在应用不在服务状态时不持有轮询领导权

### 背景

ADR-0084 第二节让 `DueBookmarkPoller.RunCycleAsync` 在应用不在服务状态时什么也不做，但数据库实现的轮询循环（`DueBookmarkPollingLoop`）每个周期照旧先经 `PollerLeadership.TryHoldAsync` 拿或续 `workflow.poller` 租约，只是不轮询。排空从第一个依赖工作流的特性开始停止算起，到数据库工作流特性自己停止（`StopAsync` 才释放领导权）为止：中间要等依赖它的特性逐个停完，各自可能等在途调用、等停止超时。这段时间里排空的节点一直续着领导权，在服务的另一个实例拿不到，到期的书签停在原地。启动中的节点同样会先拿到领导权，而它还不能轮询。

### 备选

1. 维持现状：排空通常几秒，停止时释放（ADR-0084 第二节的理由）。排空的时长不由工作流决定，依赖它的特性多、停止超时长时可以到几十秒；滚动发布时这正是另一个实例该接手的时候。
2. 特性停止时先释放、再等进行中的轮询。释放的时刻仍是本特性停止时，排空的前半段照样占着。
3. 每个周期先看应用状态：不在服务状态就不拿也不续，持有就立即释放；进入服务状态后的周期再去拿。

### 决策

选 3。`DueBookmarkPollingLoop` 的构造新增 `App`，每个周期（新增公开的 `RunCycleAsync(CancellationToken)`，循环的每个刻度调用它，测试推进注入的时钟后可直接调用，与循环互斥）先看 `App.State.IsServing()`：为假就 `PollerLeadership.ReleaseAsync`（没持有时什么也不做）并返回 0；为真才拿或续领导权、持有时调用 `DueBookmarkPoller.RunCycleAsync`。停止时的释放也在同一把互斥里。

### 理由

- 领导权只为省资源（多节点同时轮询也不会重复执行），不在服务状态的节点本来就不轮询，持有它没有任何用处，只会挡住别人。
- 排空开始后至多一个轮询周期（缺省 1 秒）就释放，不等特性停止，也不等租约到期（缺省 60 秒）。
- 判断放在周期入口，与 `DueBookmarkPoller.RunCycleAsync`、调度引擎每个周期先看应用状态同形；特性拿不到「应用开始排空」的通知，周期里判断最简单。

### 影响

- 公开 API：`DueBookmarkPollingLoop` 的构造 `(poller, leadership, options, clock, loggerFactory)` → `(poller, leadership, options, app, clock, loggerFactory)`；新增 `DueBookmarkPollingLoop.RunCycleAsync`。经特性装配的应用不受影响。
- 行为：启动中的节点不再先拿领导权，进入服务状态后的第一个周期才去拿；排空中的节点在下一个周期释放，另一个实例随即接手。配置、表结构不变。
- 调度引擎（`JobSchedulerEngine`）有同类但较轻的情况：不在服务状态的周期直接返回，不续约也不释放，租约要等 `LeaseTimeToLive`（缺省 30 秒）到期或调度特性停止时释放；它的领导权只管维护（失联节点恢复、历史清理）与不共享存储时的按计划运行，不在本节范围，记为待定。内存工作流没有领导权，不受影响。
- 测试：数据库工作流一个用例，探针特性排在工作流之后启停，注入时钟、直接调用周期：启动中不拿领导权；服务中拿到、另一个实例拿不到；排空中时钟过了续约点，这个周期释放，另一个实例随即拿到；没有工作流的告警。先写用例复现了「启动中拿到」与「排空中续约、另一个实例拿不到」，再改实现。

## 二、EF Core 支持命名连接，SQLite 上的工作流租约表放到另一个库文件

### 背景

ADR-0084 第一节给持久化加了命名连接（实体登记到命名连接、工作单元绑定一个连接），只在 SqlSugar 上实现，EF Core 明确拒绝（一个 `DbContext` 一个连接）。EF Core 在 SQLite 上跑数据库工作流时租约表因此放不到另一个库：引擎在调用方事务里写过业务库之后取实例锁，租约步骤在新作用域里的 `DbContext`（另一条连接）写同一个库文件，要等调用方自己的事务结束，忙等超时后 `WORKFLOW_TIMEOUT_001`。实测：引擎一致性套件在 EF Core + SQLite（单库文件）上 10 例以 `workflow.lock.timeout` 失败，全是加入调用方事务后取锁的用例。文档当时写「用 PostgreSQL 等行锁数据库，或改用 SqlSugar」。用户拍板：修好，带用例。

### 备选

1. 租约用独立的 `DbContext` 类型：要工作流包认识 EF Core、或应用另写一个 `DbContext` 并注册它的仓储；工作流只依赖持久化契约，做不到，而且绕开了仓储与工作单元。
2. 绑定连接时改已建出的 `DbContext` 的连接串（`Database.SetConnectionString`）：配置回调不知道这是哪个连接（拦截器、按连接区分的设置都看不到）；应用以现成的 `DbConnection` 配置时会改到别人的连接对象。
3. 普通工作单元里也支持命名连接上的实体：每个连接一个 `DbContext` 实例、工作单元跨上下文顺序提交。要按连接再建 `TContext`（反射构造或应用提供工厂），工作单元、仓储、残余变更与唯一键核对都要改成多上下文，工作流只用得到绑定连接的工作单元。
4. 与 SqlSugar 对齐「绑定连接的工作单元」：命名连接在 `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` 声明；工作单元在开始时才建本作用域的 `DbContext`，绑定了连接就让选项回调按那个连接建——回调收到的绑定主值换成它的连接串、属性沿用 `db` 绑定，与 SqlSugar 方式一的回调同形。

### 决策

选 4。

- `EntityFrameworkCorePersistenceOptions.Connections`：名字与值的规则同 SqlSugar（合 `^[a-z0-9][a-z0-9-]{0,63}$`，不能是 `default` 或 `shard`，值不能空白、不能写成子节，否则 `ArgumentException`）。
- 本作用域的 `DbContext` 建在哪个连接上由一个内部的作用域服务记着：`DbContextOptions` 的回调先看它——绑定了命名连接就交命名连接的绑定，否则照旧（带分片的调用交分片绑定，其余交 `db` 绑定）。
- `EntityFrameworkCoreUnitOfWork` 由容器以工厂创建，构造不再公开；`Context` 在 `BeginAsync` 时建出（与原来在构造时建出一样落在 Transaction 阶段）。绑定没有声明的连接 → `PERSISTENCE_VALIDATION_004`；本作用域的 `DbContext` 已经按别的连接建出后再绑定 → `PERSISTENCE_VALIDATION_002`，工作单元保持未开始。
- 仓储每个操作经工作单元取 `DbContext` 时核对落点：绑定了连接而实体落在别处 → `PERSISTENCE_VALIDATION_004`（与 SqlSugar 一致）；没有绑定而实体登记在命名连接上 → `PERSISTENCE_UNAVAILABLE_008`，不静默落到默认库。
- 实体登记到没有声明的连接在装配时抛出 `PERSISTENCE_VALIDATION_004`（原来是 `PERSISTENCE_UNAVAILABLE_008`）。
- `EnsureCreated` 对登记了实体的每个命名连接各执行一次（EF Core 按整个模型建表，那个库里多出的空表不用；只用于开发与测试）。

### 理由

- 工作流只用绑定连接的工作单元碰租约表（租约步骤与健康探针都在自己的作用域里开），绑定连接这一半就够用；普通工作单元里的命名连接实体按 Provider 不支持明确拒绝，不做推测性的多上下文事务。
- 回调按连接各调用一次、绑定的形状与 SqlSugar 方式一相同，应用同一份配置换 Provider 照样合法。
- 延迟到 `BeginAsync` 才建 `DbContext`，不改已建出上下文的连接，不碰应用传进来的连接对象。

### 影响

- 公开 API：新增 `EntityFrameworkCorePersistenceOptions.Connections`；`EntityFrameworkCoreUnitOfWork(DbContext)` 构造不再公开（由容器创建）。新配置键 `XiHan:Persistence:EntityFrameworkCore:Connections:{name}`（CLI 知识库已登记）。
- 行为：实体登记到命名连接，原来装配即 `PERSISTENCE_UNAVAILABLE_008`，现在声明了连接就能用，没声明是 `PERSISTENCE_VALIDATION_004`；绑定连接的工作单元原来 `BeginAsync` 抛 `PERSISTENCE_UNAVAILABLE_008`，现在声明了就能用。EF Core 上配 `XiHan:Workflow:Database:LockConnection` 原来装配失败，现在指向声明的连接即可。不用命名连接的应用行为不变（`DbContext` 的建出时刻从解析工作单元挪到开始工作单元，两者在 Transaction 阶段紧挨着）。
- 测试：EF Core 一组用例（两个 SQLite 文件：回调看到命名连接的绑定、行落各自的库、业务事务占着默认库写锁时绑定连接的工作单元照样提交、落点不一致与没声明的连接、`DbContext` 已建出后再绑定、选项校验）；数据库工作流在 EF Core + SQLite（租约表在命名连接的另一个库文件）上跑引擎与任务一致性套件全部通过，另一个用例在同一次调用里先写业务再取实例锁、断言租约行落在租约库、结果处理器的写入与实例完成一起回滚或提交。
