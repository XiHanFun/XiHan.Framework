# ADR-0030 Provider 健康项：清单声明的 `contributes.health` 由各 Provider 实现，连通性探针周期缓存、容量项同步计算，架构测试守护清单与实现一致

## 状态

已采纳。

## 日期

2026-10-27

## 背景

内核的 `IHealthContributor` 只有内核自身与两个插件宿主（`Plugins.OutOfProcess`、`Plugins.Wasm`）实现；14 个 Provider 的清单声明了 `contributes.health`（`caching.redis.connection`、`persistence.sqlsugar.database`、`scheduling.database.leader` 等）却没有任何实现，`/health/ready` 的 `contributions` 里永远看不到它们，`ObjectStorage.S3` 与 `Localization.Database` 连声明都没有。清单是装配的唯一事实源，声明了却不兑现是契约缺口。用户于 2026-10-27 决定在框架里补齐。约束：`IHealthContributor.Check()` 必须快速返回且不抛出；内核既定的聚合语义（条目 Degraded / Failed 只把 Ready 压成 Degraded，不改变 readiness 的通过与否）不动；Ring 1 零反射、只用 `Microsoft.Extensions.*` 抽象包。

## 备选

1. 探针在 `Check()` 里同步触网（PING、HEAD、打开连接）：实现最直接，但 `Check()` 要快且不抛，探针端点会被每次采集拖慢，外部服务卡住时 readiness 探针跟着超时。
2. 内核提供 `PeriodicHealthContributor`：Provider 只给探针委托，由它在后台按间隔重探、缓存最近结果，`Check()` 返回缓存；首探在特性 `StartAsync` 里同步完成。
3. 容量 / 状态类条目（内存实现的用量、文件系统配额、进程内领导权）也走周期探针：多一个循环却无 IO 可省；它们直接同步计算即可。
4. 让 Degraded 的条目使 readiness 失败：外部依赖一抖动整个实例就被摘流量；内核既定语义是 Degraded 仍服务，改它影响全部宿主，另议。
5. 连通性探针经 `App.InvokeAsync` 走完整管道：启动期应用还不在服务状态会被 `NotServing` 挡住，首探拿不到真实结果；探针也不需要 Tenant / Authorize 阶段。
6. 探针在根提供者上开作用域直接取客户端（`IConnectionMultiplexer`、`DbContext`、`ISqlSugarClient`），租约表的探针开一个只读工作单元读一行后回滚；不经管道、不提交。
7. 清单与实现的一致性靠 code review：会再次漂移。
8. 架构测试静态扫描源码：每个 `Health = [...]` 声明的名字（字面量或同包 `HealthName` 常量）必须在同一包里作为 `ContributorStatus(` 的条目名或 `PeriodicHealthContributor(` 的第一个参数出现，且该包有实现或注册 `IHealthContributor`；反过来注册了 `IHealthContributor` 的包必须声明 `Health`。

## 决策

采用备选 2、6、8，容量 / 状态类按备选 3 的反面同步计算。

- `Kernel/Hosting/PeriodicHealthContributor`（Ring 1）：`(name, probe, interval, TimeProvider, ILoggerFactory)`；`StartAsync` 先同步探一次（超时取 `interval`）再起 `PeriodicTimer` 循环；`Check()` 返回缓存——还没探过 `Started`，成功 `Ready`，失败 `Degraded`；探针返回的失败结果原样作为条目错误，抛出的异常（含超时）包成 `KERNEL_UNAVAILABLE_005`（`kernel.health.probe_failed`，参数 `name`、`reason`）；状态或错误码变化时记一条日志；`StopAsync` / `DisposeAsync` 停循环。
- 每个 Provider 加 `public const string HealthName`，清单改为 `Health = [HealthName]`，条目名进 `ContributorStatus(HealthName, …)`。
- 连通性类（都用 `PeriodicHealthContributor`，按 `HealthName` 作键登记，`IHealthContributor` 指向它；间隔来自各自选项新增的 `HealthProbeIntervalSeconds`，默认 15）：`Caching.Redis` PING；`Messaging.RabbitMQ` 连接与发布通道是否打开；`Messaging.Kafka` 以生产者句柄建从属管理客户端 `DescribeCluster`；`ObjectStorage.S3` 对新增选项 `HealthBucket` HEAD（没配时列桶）；`Persistence.SqlSugar` 独立作用域 `Ado.IsValidConnection()`；`Persistence.EntityFrameworkCore` 独立作用域 `Database.CanConnectAsync`（两者连不上返回 `PERSISTENCE_UNAVAILABLE_003` + 新消息键 `persistence.store.unavailable`）；`Scheduling.Database` / `Workflow.Database` 独立作用域开只读工作单元读一次租约行（`LeaseProbe` / `PollerLeaseProbe`）后回滚；`Scheduling.Database` 新增 `DatabaseSchedulingOptions`（`XiHan:Scheduling:Database`）承载间隔。
- 容量 / 状态类（同步实现 `IHealthContributor`）：`Caching.InMemory` 两张 LRU 表任一达到 `MaxEntries`（新错误码 `CACHING_UNAVAILABLE_002` + 键 `caching.store.capacity_reached`）；`Messaging.InMemory` 任一主题队列排满（`MESSAGING_UNAVAILABLE_002`）；`MultiTenancy.InMemory`（`MULTITENANCY_UNAVAILABLE_001`）；`Persistence.InMemory` 任一实体表满（`PERSISTENCE_UNAVAILABLE_002`，经 internal `IInMemoryTable` 遍历）；`Security.InMemory` 主体或授予存储满（`SECURITY_UNAVAILABLE_002`）；`ObjectStorage.FileSystem` 已用量达到 `MaxTotalBytes`（`OBJECTSTORAGE_UNAVAILABLE_002` + 新键 `objectstorage.quota.reached`，写入语义的 `quota.exceeded` 模板不适合健康项）；`Scheduling.InMemory` 报领导权状态（引擎在跑 Ready，停了 Stopped）。
- `Localization.Database`：`MessageTranslationLoader` 新增 `LastError` 并自己实现 `IHealthContributor`——最近一次加载失败 Degraded（上一份快照仍在生效），还没加载过 Started；不另起探测循环（加载器已有自己的刷新周期）。
- 架构测试 `HealthContributionTests`（两条）；插件宿主的条目名来自各插件自己的清单（RFC-0017 / RFC-0018），只豁免反向检查。

## 理由

- 周期缓存（备选 2）让 `Check()` 与探针端点保持 O(1)，外部服务卡住最多让条目 Degraded，不拖垮 readiness；首探同步保证宿主 Ready 时条目已是真实状态。
- 不经管道（备选 6）才能在启动期拿到真实结果；探针只验证连得上，不需要租户、授权与事务语义，租约表的读行用只读工作单元回滚结束，不留痕迹。
- 容量类不起循环（备选 3 的反面）：用量就在内存里，同步算零成本且没有陈旧窗口。
- 不改聚合语义（备选 4 否）：Degraded 仍服务是内核既定契约，运维按条目错误码处置；要让某个依赖决定 readiness 应是宿主策略，另议。
- 架构测试（备选 8）以源码为事实源、不实例化特性，常量解析限定在同一包内，清单与实现再漂移会在 CI 失败。

## 影响

- 新公开类型：`PeriodicHealthContributor`、`InMemoryCacheHealthContributor`、`InMemorySchedulingHealthContributor`、`InMemorySecurityHealthContributor`、`DatabaseSchedulingOptions`；`InMemoryStore`、`InMemoryTenantStore`、`FileSystemBlobStore`、`InMemoryMessageBroker`、`MessageTranslationLoader` 实现 `IHealthContributor`；各特性新增 `HealthName` 常量；选项新增 `HealthProbeInterval`（`Caching.Redis`、`Messaging.Kafka`、`Messaging.RabbitMQ`、`ObjectStorage.S3`、`Persistence.EntityFrameworkCore`、`Persistence.SqlSugar`、`Workflow.Database`）与 `S3ObjectStorageOptions.HealthBucket`。
- 新错误码 `KERNEL_UNAVAILABLE_005`、`CACHING_UNAVAILABLE_002`；新消息键 `kernel.health.probe_failed`、`caching.store.capacity_reached`、`persistence.store.unavailable`、`objectstorage.quota.reached`（10 种区域）。
- `ObjectStorage.S3` 与 `Localization.Database` 的清单新增 `Health` 声明；16 个 Provider 的探针端点响应从此带条目。
- 行为变化：连通性类 Provider 启动时多一次同步探测（受间隔上限约束的超时），探不到时应用以 Degraded 启动而不是失败。
