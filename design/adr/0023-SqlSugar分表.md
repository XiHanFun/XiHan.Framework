# ADR-0023 SqlSugar 分表：仓储自动走分表入口，查询以时间范围提示限定扫描的表

## 状态

已采纳（RFC-0005 修订）。

## 日期

2026-10-27

## 背景

`SqlSugarRepository<TEntity, TKey>` 的查询与写入不调用 `SplitTable`，带 SqlSugar `[SplitTable]` 的实体（BasicApp 的操作日志、登录日志等按月分表）经仓储读写会落到不存在的基表上。用户于 2026-10-27 决定在框架里实现，BasicApp 不写绕过代码。分表查询要能限定扫描的表，否则每次查找都要 `UNION ALL` 全部分表；而 `EntityQuery<T>` 是 Provider 无关的，不能带 SqlSugar 的表名委托。

## 备选

1. 分表实体不走仓储，由 BasicApp 直接用 `ISqlSugarClient`。
2. 仓储按实体是否带 `[SplitTable]` 自动切换到分表入口；`EntityQuery<T>` 加 Provider 无关的时间范围提示，分表 Provider 据此限定扫描的表，其他 Provider 忽略。
3. 同 2，但时间范围做成过滤条件（Provider 要知道哪个属性是时间列）。
4. 在 `Persistence.SqlSugar` 加专属的查询扩展携带表名委托（`EntityQuery` 之外另开一套）。

## 决策

采用备选 2。

- `EntityQuery<T>.TimeRange`（`TimeRange(From, To)`，两端都含，`From` 不能晚于 `To`）与 `Within(from, to)`：按时间分表 / 分区的 Provider 据此只扫描范围内的表；它不是过滤条件，不分表的实体与不支持范围的 Provider 忽略它、范围之外的行照常返回，要按时间过滤另写规约。一致性套件加用例固定这条语义（不分表实体带 `Within` 仍返回全部行）。
- `SqlSugarRepository` 按闭合泛型判断一次 `[SplitTable]`：查询在全部 `Where` / `In` 之后、排序与分页之前调用 `SplitTable(from.UtcDateTime, to.UtcDateTime)`，没有提示调用 `SplitTable()` 扫全部分表（SqlSugar 把之前的条件下推到每张分表再 `UNION ALL`）；写入经 `InsertableByObject(x).SplitTable()` / `UpdateableByObject(x).SplitTable()` / `DeleteableByObject(x).SplitTable()`，按 `[SplitField]` 的值落表（列表按表分组，缺少的分表由 SqlSugar 建出）。分表实体的写入路径不加 `TranLock`（多表 `UNION ALL` 上不能加行锁），并发由并发戳保证。
- `[SplitField]` 列用 `DateTime` 并按 UTC 写入（SqlSugar 按 `DateTime` 推表名，`DateTimeOffset` 不能转换），分表字段写入后不要再改（更新与删除按它定位所在的表）。
- `InitTables` 对分表实体走 `CodeFirst.SplitTables().InitTables()`。

## 理由

- 分表是实体的声明（`[SplitTable]` + `[SplitField]` + 表名占位符），仓储按声明自动切换，业务代码与不分表时完全一样；这与协议接口驱动映射的思路一致（ADR-0013）。
- 时间范围做成"提示"而不是过滤（备选 3）：过滤需要抽象层知道时间列是哪一个，等于在 `EntityQuery` 里引入第二套条件语言；提示只影响扫描范围，语义简单、Provider 无关、可被忽略，且一致性套件把"忽略"固定下来，不是静默的行为差异。
- 不另开一套查询对象（备选 4）：`EntityQuery` 是跨 Provider 的契约，BasicApp 的查询服务写一遍就要能在三个 Provider 上跑。

## 影响

- `Persistence.Abstractions` 公开面（Preview）：新增 `TimeRange`、`EntityQuery<T>.TimeRange`、`EntityQuery<T>.Within`。`Persistence.InMemory` 与 `Persistence.EntityFrameworkCore` 不改代码（忽略提示）。
- `Persistence.Conformance` 新增用例 19；`Persistence.SqlSugar.Tests` 新增分表用例（按月落表、跨表查找、范围提示只扫一月、更新与并发戳、批量更新 / 删除）。
- RFC-0005 的查询行与 SqlSugar 行修订。
