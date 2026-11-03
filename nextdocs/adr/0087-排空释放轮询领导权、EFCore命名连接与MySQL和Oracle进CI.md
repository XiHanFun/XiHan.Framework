# ADR-0087 排空释放轮询领导权、EF Core 命名连接与 MySQL 和 Oracle 进 CI

## 状态

已采纳（`spec/rfcs/0005-persistence.md`、`spec/rfcs/0021-workflow.md`、`spec/conventions/error-codes.md` 随之修订；修订 ADR-0084 第一节（EF Core 的命名连接）、第二节（排空期间的领导权、周期中途进入排空）、第四节（Oracle 11g 与兼容库改为已定）、第五节（MySQL 与 Oracle 进 CI））。

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

## 三、轮询周期中途进入排空即停下，不逐个分区告警（缺陷修复）

### 背景

ADR-0084 第二节让 `DueBookmarkPoller.RunCycleAsync` 只在周期开头看一次应用状态。周期已经开始、之后才进入排空（某个宿主停机）时，剩下每个分区的扫描调用都被管道拒绝，按 `WORKFLOW_UNAVAILABLE_001` 各记一条告警；清理与恢复同样如此。BasicApp 升锁到 `6c162881` 后集成全量跑两次分别 4 条与 0 条（原来 476 条），那 4 条是同一个周期里的四个分区（平台、acme、contoso、globex），库并没有不可达。

### 备选

1. 每个分区开调用之前再看一次应用状态：看与调用之间状态照样会变，只是把窗口缩小。
2. 调用失败之后再看应用状态：已不在服务状态就停下这个周期、不告警；还在服务状态就照旧按不可达告警、继续别的分区。
3. 让工作流自己开调用的路径返回另一个错误码区分「应用不在服务状态」：`WORKFLOW_UNAVAILABLE_001` 是引擎在调用之外被调用时的公开契约，改码牵动所有调用方。

### 决策

选 2。周期里（`RunCycleAsync`）分区的扫描、恢复与清理调用失败后看 `App.State.IsServing()`：为假就停下这个周期——扫描阶段直接返回 0（已扫到的书签留给下一个在服务的周期），恢复阶段不再恢复剩下的书签，清理阶段不再清理剩下的分区——这些失败都不记告警。还在服务状态时照旧：一个分区不可达不挡住别的分区，按码告警。手动调用的 `PollOnceAsync`、`HousekeepAsync` 不在周期里，失败照实告警（ADR-0084 第二节不变）。

### 理由

- 失败之后看状态没有窗口：管道拒绝的调用不会有任何写入，失败时应用已不在服务状态，就说明这次失败与之后的分区都是排空造成的。
- 排空中剩下的分区调用必然被拒，继续调用只是逐个失败；停下来与周期开头的判断是同一条规则（不在服务状态的周期什么也不做）。
- 不改公开错误码，不改手动调用的语义。

### 影响

- 行为：应用停机时不再出现「同一周期里剩下的每个分区一条」的 `Scanning due workflow bookmarks … 'WORKFLOW_UNAVAILABLE_001'` 与 `Purging ended workflow instances failed …` 告警。公开 API、配置、表结构不变。
- 与第一节一起：数据库实现里，周期开头在服务状态时照常拿或续领导权，中途进入排空的这个周期停下，下一个周期（仍在排空）释放领导权。
- 测试：内存与数据库工作流各一个用例，平台加三个租户共四个分区，书签存储的装饰器在平台分区的扫描进行中让应用开始停机（依赖工作流的探针特性停在闸门前，应用一直处在排空中），断言只有平台分区的扫描到达存储、没有工作流的告警；数据库的用例另断言这个周期持有领导权、下一个周期释放。先写用例复现了 acme、contoso、globex 三个分区的扫描告警，再改实现。

## 四、Oracle 11g 与 Oracle 兼容库的读列维持不支持

### 背景

ADR-0084 第四节把 Oracle 的读列改为查数据字典：判自增要 `USER_TAB_COLUMNS.IDENTITY_COLUMN`，这一列 12c 才有，11g 上读列失败；达梦、OceanBase Oracle 模式等兼容库仍走 SqlSugar 的读列（`GetSchemaTable`：CLR 类型名、读不到 identity、缺省值错位），没有验证环境。当时记为待定。

### 决策

维持不支持（用户拍板），在 RFC-0005、docs-next 的持久化指南与 SqlSugar 包页、包 README 写明：11g 及更早读列以 `PERSISTENCE_UNAVAILABLE_006` 失败，不做 11g 分支；兼容库走 SqlSugar 原来的读列，没有验证，读到的结构不作保证。实体目录不连库，不受影响。

### 理由

- 11g 早已停止支持，为它另写一套不判 identity 的读列只是给代码生成一个更差的结果。
- 兼容库各家与 Oracle 的差异不一，没有验证环境就按「不支持」写明，比写成「可能可用」更不容易误导。

### 影响

无代码与行为变化；错误码 `PERSISTENCE_UNAVAILABLE_006` 的说明补上 11g。需要的应用另起一项、带验证环境再做。

## 五、MySQL 与 Oracle 的门控用例进框架 CI

### 背景

`Persistence.SqlSugar.Tests` 有 MySQL 与 Oracle 的门控用例（库结构读取各一例、库结构一致性套件各五例；`XIHAN_TEST_MYSQL`、`XIHAN_TEST_ORACLE`），ADR-0084 第五节只给 CI 加了 SQL Server，这两个库的用例在 CI 里一直跳过。用户拍板：进 CI，做法同 SQL Server；Oracle 镜像大、起得慢，尽量控制 CI 时长。

### 备选

1. 两个都加到主作业的服务容器：主作业要等最慢的服务健康才开始，Oracle 镜像的拉取与启动全压在主作业上。
2. Oracle 用每周或手动触发的单独工作流：时长不受影响，但推送与 PR 上看不到 Oracle 的失败。
3. MySQL 加到主作业；Oracle 单独一个作业，与主作业并行，只还原、构建、测试 `Persistence.SqlSugar.Tests` 一个项目，镜像选 `slim-faststart` 变体。
4. 缓存 Oracle 镜像（`docker save` 进 Actions 缓存）：镜像解压后约 6.5 GB，存取缓存本身就要几分钟，还占缓存配额，不比直接拉快。

### 决策

选 3。

- 主作业的 `services` 加 `mysql`：`mysql:9.7`（LTS），`MYSQL_ROOT_PASSWORD`（只给 CI 一次性容器用的固定口令，同 SQL Server），端口 `3306:3306`；健康检查 `mysqladmin ping -h 127.0.0.1 -uroot -p… --silent`，每 5 秒、最多 30 次——走 TCP：镜像初始化期间的临时服务器不开网络，正式服务器起来之后才报健康。测试步骤加 `XIHAN_TEST_MYSQL: Server=localhost;Port=3306;User Id=root;Password=…`。
- 新作业 `oracle`（`timeout-minutes: 30`）：服务 `gvenzl/oracle-free:23-slim-faststart`（库在镜像里已建好，启动只打开实例），`ORACLE_PASSWORD`，端口 `1521:1521`；健康检查用镜像自带的 `healthcheck.sh`（可插拔库 `FREEPDB1` 打开后才成功），每 10 秒、启动宽限 20 秒、最多 30 次。步骤只还原、构建（Release）、测试 `Persistence.SqlSugar.Tests`，测试步骤只设 `XIHAN_TEST_ORACLE: User Id=system;Password=…;Data Source=localhost:1521/FREEPDB1`（`SYSTEM` 能建删用户，每个用例建删一个用户），其余门控用例在主作业里跑、这里跳过；测试结果另传一个产物。
- 主作业测试步骤的注释改为「Oracle 在单独的作业里跑」，不再写「不在 CI 跑」。

### 理由

- MySQL 镜像小（压缩后约数百 MB）、十几秒健康，与其余服务容器并行拉取，放主作业几乎不加时长，做法与 SQL Server 一致。
- Oracle 单独并行：主作业不等它；作业只构建一个测试项目，几分钟内完成，总时长取决于主作业。`slim-faststart` 省掉建库，本机冷启动到健康约 30 秒。
- 整个测试项目跑一遍而不按名字筛 Oracle 的用例：SQLite 的用例几秒钟，不必维护筛选条件，新增的 Oracle 用例自动纳入。

### 影响

- CI：主作业多拉一个 MySQL 镜像；多一个并行作业。两组用例在 CI 里失败就是真失败。
- 本机按同样的镜像、环境变量、健康检查与连接串（端口换成 13306、11521）验证：MySQL（本机镜像 9.7.1）第二次检查即健康、Oracle 约 30 秒健康；照 `oracle` 作业的步骤单独还原、构建、测试 `Persistence.SqlSugar.Tests`：176 例 112 通过、64 跳过，Oracle 的 6 例全部真跑通过；设 `XIHAN_TEST_MYSQL`：MySQL 的 6 例全部真跑通过；用例建的库与用户都已删除。工作流 YAML 没有解析器可用，按现有服务的写法人工核对，并用一段只认映射、列表与块标量的小脚本按缩进解析出两个作业、六个服务容器与各步骤的环境变量核对。
- 修订 ADR-0084 第五节「MySQL 与 Oracle 的门控用例不进 CI」。

> 修订（2026-11-01）：第一节「影响」里记为待定的调度引擎改为同样的做法——应用不在服务状态时不持有领导租约，数据库实现的租约释放不经管道，见 ADR-0091 第一节。

> 修订（2026-11-01）：第二节备选 3（普通工作单元里也支持命名连接上的实体）定为不做，EF Core 维持只支持绑定连接的工作单元，见 ADR-0091 第二节。

> 修订（2026-11-03，第十八批）：BasicApp 报回「一致性套件删掉测试库那一刻，工作流轮询器还在抢领导权，冒一条 `WORKFLOW_UNAVAILABLE_001`」，查明不是本 ADR 的排空路径漏了情形，也不是工作流一致性夹具的释放顺序问题，定为已定、不改代码：
>
> - 来源是 BasicApp 为 `Security.Conformance` 用例 5（存储不可达）写的夹具 `ConformanceHosts.StartUnreachableAsync`：它在**完整的、还在服务的** BasicApp 宿主下 `DROP DATABASE … WITH (FORCE)`。这时轮询周期在服务状态下去拿领导权，驱动真实失败，`PollerLeadership` 如实告警；同一窗口里发件箱、授权策略、翻译加载等也各自报库不存在。在 BasicApp 的副本上把这个窗口拉长到 2.5 秒后稳定复现出同样的两条（`Acquiring the workflow poller leadership failed with 'WORKFLOW_UNAVAILABLE_001'`、发件箱 `3D000`）；工作流一致性两套 48 例在 BasicApp 宿主上跑没有这条。
> - 工作流一致性夹具（BasicApp 的 `WorkflowConformanceHosts` 与框架自己的测试宿主）本来就是先停宿主再删库：停止让应用排空，轮询器释放领导权、等循环退出，之后删库没有告警。
> - 服务状态下存储真的不可达时告警是正确的运维信号，不能压掉；本 ADR 只免去「应用不在服务状态」时的告警。
> - 文档补上：工作流夹具的释放句柄先停宿主再删库（`WorkflowEngineHost` / `WorkflowStoreHost` 的参数说明、套件 README、docs-next 一致性页），`Security.Conformance` 的 `CreateUnavailableStoreAsync` 在完整宿主上实现时不要让整个宿主的库不可达，用最小宿主或只让被测存储的连接不可达。用例 `PollerOutageTests`（SQLite 与 PostgreSQL）：服务中拿走存储，周期如实报 `WORKFLOW_UNAVAILABLE_001`；先停宿主再拿走存储，没有任何工作流告警。
