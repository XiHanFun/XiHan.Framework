# ADR-0026 MultiTenancy.Database：租户目录进数据库，单例目录直接开工作单元读取并有界缓存

## 状态

已采纳（补足 ADR-0018 留给"另行按需做"的 `MultiTenancy.Database`；RFC-0007 的 Provider 表随之修订）。

## 日期

2026-10-27

## 背景

ADR-0018 把租户分片契约与默认解析器放进 `MultiTenancy.Abstractions`，并写明"`MultiTenancy.Database`（租户目录进数据库）另行按需做，分片不依赖它"。BasicApp 6.0 的租户目录在数据库里（4.x 的 `SysTenant` 表：ID、名称、启用状态、连接串等），默认的 `MultiTenancy.InMemory` 只能从配置装载、容量有界，应用要么自己实现 `ITenantStore`，要么把全部租户抄进配置。用户 2026-10-27 决定在框架里实现这个 Provider，而不是由 BasicApp 自己绕过。

难点在于目录的读取方式。仓储只能在活动的工作单元里使用，应用调用里由 Transaction 阶段的 `UnitOfWorkMiddleware` 开启；而 `ITenantStore` 是 Tenant 阶段（在 Transaction 之前）要查的东西，还会被分片解析器与后台线程调用。

## 备选

1. **目录经 `App.InvokeAsync` 走管道**（`Workflow.Database` / `Scheduling.Database` 的存储单例用的方式）：每次查找在根提供者的新作用域里发起一次嵌套调用，仓储在 Transaction 阶段内使用。否：嵌套调用会再过一遍 Tenant / Authorize 阶段，`XiHan:MultiTenancy:RequireTenant=true` 时嵌套调用没有租户就失败，租户目录读自己成了自指；而且 `InvokeAsync` 要求应用处于服务状态，启动期与停机期读不了。
2. **直接开工作单元读取**：在根提供者的新作用域里 `IUnitOfWork.BeginAsync`（只读、无租户）→ 取 `IRepository<TenantRecord, string>` → 读 → `CommitAsync`，异常时回滚后原样抛出；与 `OutboxDispatcher.RunOnceAsync` 同一方式。
3. **属性放子表**（`TenantProperty(TenantId, Key, Value)`）：查询友好，但每次读租户要两次往返或一次连接，写入要按行比对增删改。
4. **属性放 JSON 列**：`PropertiesJson` 一列，源生成的 `JsonSerializerContext` 序列化 `Dictionary<string, string>`。
5. **不缓存，每次查库**：简单，但每个调用的 Tenant 阶段都查一次库，且分片解析器已经按租户缓存，两层语义不一致。
6. **按租户有界缓存，带存活时间，写入时失效**。

## 决策

取 2、4、6。

- 新包 `XiHan.Framework.MultiTenancy.Database`（Ring 3，`role: provider`，清单 `xihan.multitenancy.database`）提供 `multitenancy.context@1` 与 `multitenancy.sharding@1`，需要 `persistence.relational@1`；装配后顶替默认的 `MultiTenancy.InMemory`（默认实现的两个能力都未被选中即不装配），只为被选中的能力注册存储、解析器、中间件与分片解析器。不声明健康项（健康项另行统一做）。
- 清单的 `requires` 不写 `persistence.relational@1`，改在 `StartAsync` 核对图里有它的提供者（没有即 `KERNEL_CAPABILITY_001` 中断启动）：`Persistence.SqlSugar` / `EntityFrameworkCore` 按 ADR-0018 可选依赖 `multitenancy.sharding@1`，内核把满足了的可选依赖也当作排序边，本包再声明持久化就成环（`xihan.persistence.sqlsugar -> xihan.multitenancy.database -> xihan.persistence.sqlsugar`）。两个方向的依赖在运行期都不需要装配顺序：仓储登记经 `RepositoryRegistrations` 对先后装配都生效，持久化 Provider 读分片只看调用上下文。
- 实体 `Tenants.TenantRecord : IEntity<string>, IHasCreationTime, IHasModificationTime, IHasConcurrencyStamp`：主键是租户 ID，`DisplayName`、`IsEnabled`、`PropertiesJson`（键区分大小写的字符串字典的 JSON 对象）。平台级数据：不实现 `IMultiTenant`、不软删除；EF Core 应用在 `DbContext` 里登记，SqlSugar 按约定映射；`PropertiesJson` 用长文本列。
- `Tenants.DatabaseTenantStore`（单例 `ITenantStore`）经包内的 `CatalogInvocation` 直接开只读工作单元读取。`FindAsync` 按租户缓存 `Maybe<TenantInfo>`（不存在也缓存），容量 `CacheSize`（默认 10000，满时整体清空）、每条存活 `CacheTimeToLive`（默认 30 秒，0 不过期，由 `TimeProvider` 判定）；`ListAsync` 按 ID 分页读完（每页 1000，最多 `MaxListedTenants` 条，超出记警告），不走缓存；`Invalidate(tenantId?)`。基础设施异常按 `ITenantStore` 契约原样抛出，属性列不是 JSON 对象同样抛出，不伪装成不存在。
- `Tenants.TenantRecordStore`（作用域，用调用方的工作单元）：`UpsertAsync` / `RemoveAsync` 批量逐项 `Result`（一次 `FindManyAsync` 取回已有行再 `AddRangeAsync` / `UpdateRangeAsync` / `RemoveRangeAsync`，同一批同一租户后者覆盖前者、同一行只写一次；不存在 → `MULTITENANCY_NOTFOUND_001`），`ListAsync(skip, take)`；写入后立即失效目录与分片解析器（可选依赖）的对应条目。
- 分片仍由抽象包的 `TenantStoreShardResolver` 从租户属性推出，本包不另写分片解析器。
- 选项 `DatabaseMultiTenancyOptions`（`XiHan:MultiTenancy:Database`：`CacheSize`、`CacheTimeToLiveSeconds`、`MaxListedTenants`）。不新增错误码与消息键。

## 理由

- 直接开工作单元避开了管道的自指与服务状态限制，`OutboxDispatcher` 已经证明这种方式与各持久化 Provider 兼容；租户是平台级数据，工作单元不带租户也与"目录不受当前调用租户影响"的语义一致。
- 属性是不透明的键值、按租户整体读写、不按属性查询，JSON 列一次往返读全、一次写入覆盖，与 `Workflow.Database` 的"查询列 + JSON 文档"做法一致；源生成上下文保持 AOT 兼容。
- 缓存让 Tenant 阶段在热路径上不触库；"不存在"也缓存，避免攻击者用不存在的租户 ID 反复触库；存活时间兜住绕过 `TenantRecordStore` 的改表与"失效先于提交"的窗口（写入在事务提交后才可见，失效发生在写入时，两者之间的并发读会把旧值重新装进缓存，到期后收敛）。

## 影响

- 新包 `MultiTenancy.Database` 与测试项目 `MultiTenancy.Database.Tests`（`TenantStoreConformance` 与 `TenantShardResolverConformance` 对内存持久化全部通过；对 SQLite（`Persistence.SqlSugar`）验证实体映射与属性往返）。
- `next/README.md` 包表加一行；RFC-0007 的 Provider 表由主会话修订。
- 待内核决定：能力图的拓扑排序是否该在可选依赖处断环（可选边只用于 Degraded 判定与排序偏好，不是硬依赖）。若断环，本包可把 `persistence.relational@1` 写回 `requires` 并去掉启动期核对；在此之前，任何"需要持久化、又被持久化 Provider 可选依赖"的 Provider 都只能用同样的方式。
- BasicApp 6.0 的租户目录改为 `TenantRecord` 表（或在迁移里把 `SysTenant` 投影成它），管理操作经 `TenantRecordStore` 写入；连接串放进属性 `ConnectionString`，分片语义不变。
