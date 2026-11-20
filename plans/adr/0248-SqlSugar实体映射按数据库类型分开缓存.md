# ADR-0248 SqlSugar 实体映射按数据库类型分开缓存

## 状态

已采纳（只改 SqlSugar Provider 的实现；公开 API 与规范不变）。

## 日期

2026-11-20

## 背景

- SqlSugar 取实体映射（`EntityMaintenance.GetEntityInfo`）时，缓存键是 `"GetEntityInfo" + 类型哈希 + 类型全名 + ConfigId`，存在连接配置的反射信息缓存（`ReflectionInoCacheService`）里，缺省是进程内共用的静态缓存。数据读取器的物化器用同一份缓存，键里同样没有数据库类型。
- `SqlSugarConventions` 的列约定随数据库类型而变：SQL Server 上 `DateTimeOffset` 的写入参数按 `datetime2` 传（ADR-0231），有长度的字符串在 SQL Server 上建成 `nvarchar`、在 Oracle 上建成 `nvarchar2`（ADR-0232），PostgreSQL 上非可空的值类型列带 CLR 缺省值（ADR-0040）。
- 于是 `ConfigId` 相同、数据库类型不同的连接会用到先建好的那份映射，数据库类型在键里体现不出来。
  - 例子：一个应用的集成测试在同一进程里先起了几个 SQLite 宿主，后面的 SQL Server 宿主（`ConfigId` 都是 `default`）沿用了 SQLite 的映射。时间参数按 `datetime` 传，读回只剩 1/300 秒，4 个比对时刻的用例偶发失败，单独跑却能过。
  - 生产上也会碰到（推断，未复现）：租户分片连接的 `ConfigId` 都是 `shard`。不同租户用不同类型的库时，先服务过 PostgreSQL 租户，SQL Server 租户的时间就会被截精度，`InitTables` 加的字符串列也会建成 `varchar`，存不了汉字。
- 框架自己的测试一直在绕开这个问题：`SqlSugarIndexTests` 与 `SqlSugarSchemaEvolutionTests` 给每种库各用一个 `ConfigId`。

复现：新增的 `SqlSugarMappingCacheTests` 修复之前有 2 例确定失败，是 `ConfigId` 相同时先建 SQLite 再建 SQL Server、先建 SQL Server 再建 SQLite 这两个顺序。

## 备选

1. **要求每种库用不同的 `ConfigId`**：否决。`ConfigId` 是落点规则的依据（`default`、`shard`、命名连接），不能按库类型改名；租户分片的库类型要到调用时才知道。
2. **每个连接配置一份独立的缓存**：否决。本特性每个作用域都新建连接配置，映射会每次调用都重建一遍。
3. **自建按数据库类型分开的存储**：否决。SqlSugar 的 `RemoveCacheAll` 只清它自己的静态缓存，这些条目就清不掉了，还要另外处理容量与并发。
4. **在原来的缓存外包一层，键加上数据库类型**：采纳。条目仍在原来的缓存里，同类型的连接照旧共用，`RemoveCacheAll` 照样清得掉；数据库类型在每次取用时读取。

## 决策

1. `SqlSugarConventions.Apply` 把配置的反射信息缓存包进内部的 `DbTypeScopedReflectionCache`。包的是此刻的缓存，缺省的或应用设过的都一样。之后每个键前面加上 `xihan.<数据库类型>:` 再交给它；已经包过的不再包。
2. 数据库类型在每次取用时从连接配置读取，与列约定的读法一致：应用可以在 `Apply` 之后才设数据库类型。
3. 运行时数据源（`SqlSugarDataSourceProvider`）不施加约定，不受影响。

## 理由

- 映射的差别只来自数据库类型，所以键补上数据库类型就够了，不必按连接分开。
- 只改 Provider 内部的缓存键，SqlSugar 的取用路径与公开 API 都不变。

## 影响

- 线上行为：`ConfigId` 相同、数据库类型不同的连接各用各的列定义；同类型的连接共用一份映射，与之前相同。每次取用多拼一次键前缀。
- 公开 API 不变，规范不变。
- 测试：新增 `SqlSugarMappingCacheTests`，覆盖两种先后顺序、同类型共用一份、`Apply` 之后才设数据库类型。`SqlSugarIndexTests` 与 `SqlSugarSchemaEvolutionTests` 里「不同的 ConfigId 才不会串用」的说明已删去。
