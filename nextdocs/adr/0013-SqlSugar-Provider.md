# ADR-0013 SqlSugar Provider：应用持有客户端，协议约定替代基类

## 状态

已采纳（分片连接的选择方式被 ADR-0022 修订：从"整个作用域切到分片连接"改为按实体落点的多连接客户端）

## 日期

2026-10-23

## 背景

ADR-0005 把 `Persistence.SqlSugar` 留到 P5 随迁移提供。[06-BasicApp迁移清查](../architecture/06-BasicApp迁移清查.md) 查出 BasicApp 的数据层全是 SqlSugar 原生 API（1975 处），所以它从可选升为 P5-03 前置项。要决定依赖包、客户端归属、实体映射方式与并发策略。

## 备选

1. 依赖 `SqlSugarCoreNoDrive`，驱动由应用引用；实体继承框架提供的 SqlSugar 基类并标注 `[SugarColumn]`。
2. 依赖 `SqlSugarCore`；客户端由特性按回调创建或由应用注册；实体只实现框架协议接口，映射由 `ConfigureExternalServices.EntityService` 的约定推出；并发用框架自己的并发戳。
3. 同备选 2，但并发用 SqlSugar 的 `IsEnableUpdateVersionValidation` 版本列。

## 决策

采用备选 2。

- 包依赖 `SqlSugarCore`（存量应用用的就是它，与 `SqlSugarCoreNoDrive` 同名程序集不能并存）。
- `SqlSugarPersistenceFeature(configure)` 创建作用域内的 `SqlSugarClient` 并自动应用 `SqlSugarConventions`；`SqlSugarPersistenceFeature()` 使用应用注册的 `ISqlSugarClient`（`SqlSugarScope`、多库、分表等保持应用自管），应用在创建前调用 `SqlSugarConventions.Apply(config)`。
- 约定：`Id` 主键不自增、`ConcurrencyStamp` 长度 32、可空引用类型列可空、无公开 setter 与集合属性忽略；实体与 EF Core Provider 共用同一套领域类型。
- 软删除与租户以表达式按属性名显式施加在每个查询上（不用 SqlSugar 全局过滤器，避免与应用的过滤器互相覆盖）。
- 并发：更新与删除先在事务内加行锁（`TranLock`，SQLite 由数据库写锁保证）取回当前行，比对并发戳，不同即冲突；行被锁住，读取与写入之间别人改不了它。
- `DateTimeOffset` 经转换器按 UTC 时刻往返；实体类型没有 `new()` 约束，写入经 SqlSugar 的按对象入口。
- 工作单元首次写入 `BeginTran`，与 EF Core Provider 一样延迟开启。

## 理由

- 存量迁移的核心诉求是"实体与数据访问不推倒重来"：协议接口加约定让同一实体类在 EF Core 与 SqlSugar 两个 Provider 上都能跑一致性套件。
- SqlSugar 的版本列机制会接管戳的生成与格式，与 `PersistenceConventions.NewStamp` 的 32 位十六进制及跨 Provider 的并发语义冲突。
- 批量优先（ADR-0012）在 SqlSugar 上天然成立：`Insertable(list)`、`Updateable(list)`、`Deleteable().In(ids)` 都是一次调用。

## 影响

- 新包 `XiHan.Framework.Persistence.SqlSugar`（Ring 3，`persistence.relational@1`，Preview）与测试 `Persistence.SqlSugar.Tests`（SQLite 跑 `Persistence.Conformance` 全部用例）。
- 规范 `package-ids.md` 的技术名加 `SqlSugar`，RFC-0005 的 Provider 表加一行。
- BasicApp 迁移（P5-04）先以方式二接入既有客户端，再逐模块把原生 SqlSugar 查询收进仓储。
