# ADR-0044 SqlSugar 仓储写入按实体开启差异日志

## 状态

已采纳（Provider 行为，不改规范：RFC-0005 的仓储契约与差异日志无关）。

## 日期

2026-10-29

## 背景

BasicApp 6.0 N2 第三批（Auditing）报告：`SqlSugarRepository` 的插入、更新、删除（含批量、软删除与分表）都走 `InsertableByObject` / `UpdateableByObject` / `DeleteableByObject`，从不调 `EnableDiffLogEvent`。方案 D6 的"差异日志经 `AopEvents.OnDiffLogEvent` 挂"因此永远不触发，应用也拿不到更新前的值。SqlSugar 没有变更跟踪，差异日志是它取前后值的唯一途径。

SqlSugar 的约束（对照 5.1.4.207 源码与实测）：

- 按对象入口的 `EnableDiffLogEvent` 与 `SplitTable` 不能连用：插入、删除的 `EnableDiffLogEvent()` 返回的 `CommonMethodInfo` 没有 `SplitTable()`，反过来也没有。
- 泛型构建器上，分表插入（`SplitInsertable`）与分表更新（`SplitTableUpdateByObjectProvider`）沿用差异日志开关；分表删除（`SplitTableDeleteByObjectProvider`）按表重建 `Deleteable<T>()`，丢掉开关。
- 差异日志每次写入多一次按主键的读取：更新、删除读写前值，插入读写后值。
- 分表插入的各组复用同一个 `DiffLogModel`，每组替换前后值后触发一次回调。

## 备选

1. 应用自己在写入后再查一次：前值拿不到，绕开仓储。
2. 全部实体开启：每次写入多一次读，审计不需要的表也付代价。
3. 按实体类型的谓词开启，缺省关；开启的实体走泛型构建器带 `EnableDiffLogEvent`，其余实体维持原路径。

## 决策

采用 3。

- `SqlSugarPersistenceFeature.DiffLog`（`Func<Type, bool>?`，init）设置哪些实体开启，经 `SqlSugarPersistenceOptions.DiffLog` 交给仓储（不从配置读取，缺省一个都不开）。
- 开启的实体，仓储的写入经内部的 `DiffLoggedWrites<T>`（按闭合类型构造一次）走泛型构建器并调 `EnableDiffLogEvent(data)`：插入、更新（含软删除）、物理删除与三个批量方法，分表实体同样成立。分表删除由框架按 `[SplitField]` 的值分组到表，每张表一次按主键删除并带开关，写前值取自同一张表。
- 业务数据 `SqlSugarDiffLogData(EntityType, Services)`：被写入的实体类型，以及仓储所在作用域的服务提供器。回调在写入执行后同步触发（不等提交），所以调用方的 AsyncLocal 可读，作用域服务与当前调用（`IInvocationAccessor`）经 `Services` 取。
- 应用在连接配置的 `AopEvents.OnDiffLogEvent` 上接收（方式一在配置回调里设，默认与分片连接都会经过同一回调；方式二在自己的 `ConnectionConfig` 上设）。事务回滚由应用处理：按调用缓冲，调用成功再落库。要缓冲时在回调里复制所需的值，不能留 `DiffLogModel` 的引用（分表插入复用同一个模型）。
- 没开启的实体行为不变，仍走按对象入口。

## 理由

- 谓词让应用按审计需要选表，多一次读的代价只落在这些表上。
- 泛型构建器是 SqlSugar 同时支持差异日志与分表的唯一入口；仓储的实体类型没有 `new()` 约束，按闭合类型反射构造一次即可，实体须有公开无参构造函数，与按对象入口的要求相同。
- 业务数据带作用域服务，而不是让回调依赖全局状态：`SqlSugarScope` 或共享的连接配置是单例，回调本身拿不到作用域。

## 影响

- 公开 API：`SqlSugarPersistenceFeature.DiffLog`、`SqlSugarPersistenceOptions.DiffLog`、`SqlSugarDiffLogData`；`SqlSugarRepository` 构造函数增加 `SqlSugarPersistenceOptions` 与 `IServiceProvider`（由容器注入，5.0 尚未发布）。
- 行为：只影响开启的实体，每次写入多一次读并触发回调；没装回调时只多一次读。
- 测试：`SqlSugarDiffLogTests`（SQLite 与 PostgreSQL）：插入 / 更新 / 软删除的前后值、主键标记、业务数据与作用域、AsyncLocal；批量插入 / 更新 / 物理删除各一条事件；没开启的实体不触发；分表实体跨月插入、按表更新与分表删除。
