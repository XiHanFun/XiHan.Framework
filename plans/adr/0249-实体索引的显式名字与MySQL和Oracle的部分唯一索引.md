# ADR-0249 实体索引可以显式命名；SqlSugar 在 MySQL 与 Oracle 上以函数索引建部分唯一索引

## 状态

已采纳（`spec/rfcs/0005-persistence.md`「索引声明」随之修订）。修订 ADR-0072 的命名（名字只由规则推出）与 ADR-0081 的支持名单（MySQL、Oracle 启动失败）。

## 日期

2026-11-20

## 背景

- 软删除实体的唯一索引是只约束未删除行的部分唯一索引（ADR-0081），写入撞上时由 Provider 核对、返回 `PERSISTENCE_CONFLICT_003`（ADR-0072）。核对只认得 `[EntityIndex]` 声明的索引。
- BasicApp 的软删除实体（字典、字典项、OAuth 应用、角色、用户等 47 个索引）不用 `[EntityIndex]`，用自己的 `[ActiveUniqueIndex]` 在建表之后以原生 SQL 补建部分唯一索引。框架不知道这些索引：两个请求同时新增同一个编码，操作里的先查后写都没查到，第二个写入撞上索引，仓储核对不出是哪个唯一键，数据库异常原样抛出，调用以 `KERNEL_UNEXPECTED_001`（HTTP 500）结束，而不是 409。
- BasicApp 没有改用 `[EntityIndex]`，有两处做不到：
  - 名字。框架的名字只由规则推出（`UX_sys_dict_OwTeId_Co`），BasicApp 的索引已经按自己的规则（`ux_sys_dict_owner_tenant_id_code`）建在已发布版本的库里，结构测试也守着这条规则。换成推出的名字，每个部署都要先删旧索引、再建新索引。
  - 数据库。BasicApp 支持 MySQL，框架在 MySQL、Oracle 上遇到软删除实体的唯一索引就启动失败。BasicApp 在这两种库上用函数索引（每列包成 `CASE WHEN is_deleted = 0 THEN 列 END`），MySQL 已在它的 CI 上跑通。
- ADR-0081 否决函数索引的理由是"各库写法不同、版本要求不同（MySQL 8.0.13+），本机验证不了"。现在框架 CI 有 MySQL 与 Oracle 的服务容器（ADR-0084 之后），可以在真实库上验证。

## 备选

名字：

1. 维持只由规则推出，BasicApp 写迁移脚本把全部索引改名。每个已部署的库都要执行，五种数据库各一份脚本；改名期间旧名字的索引还在，脚本写错就是重复的索引或没有索引。
2. `[EntityIndex]` 加可选的 `Name`：给了就原样使用，没给照旧推出。
3. 全局的命名策略回调（选项里给一个函数）。应用的每个实体都套同一个规则，表达不了已有库里不规则的名字；两个 Provider 都要接这个选项。

MySQL 与 Oracle：

1. 维持不支持，BasicApp 保留自己的 `[ActiveUniqueIndex]`，另写一层把数据库错误里点名的索引翻成 409。两套机制并存，翻译靠解析各库的错误文本。
2. SqlSugar 在 MySQL（8.0.13 起）与 Oracle 上建函数索引：每列一个 `CASE WHEN 未删除 THEN 列 END`，已删除的行整键为空。
3. 用生成列模拟（`UNIQUE (Code, (CASE WHEN IsDeleted THEN Id END))` 之类）。要给表加列，绕开 SqlSugar 的建表与加列流程，实体映射也多一列。

## 决策

名字取 2，MySQL 与 Oracle 取 2；EF Core 不变。

- `EntityIndexAttribute.Name`（`string?`）：给了就用它，`EntityIndexes.Resolve` 不再推。显式的名字必须字母开头、只含 ASCII 字母、数字与下划线（名字原样拼进建索引的语句，这样的标识符在五种数据库上都不必转义；Oracle 不加引号的名字要字母开头），长度上限仍是 63；同一实体里与别的索引同名（不区分大小写）照旧是声明错误。PostgreSQL 上名字照旧折成小写，Oracle 上存成大写，其余数据库原样。
- 跨实体同名：推出的名字带表名，不会跨实体相同；显式的名字可能相同。PostgreSQL、SQLite 与 Oracle 的索引名按架构唯一，`IF NOT EXISTS` 或先查再建都会跳过后建的那个，那张表就静默少了唯一约束。`SqlSugarConventions.InitTables` 在建任何表之前核对这批实体的 `[EntityIndex]` 不跨实体同名（不区分大小写；同一实体在入参里出现两次不算），同名即抛 `InvalidOperationException`。EF Core 建库与迁移遇到同名索引时数据库本身报错，不会静默，不另加核对。
- SqlSugar 的部分唯一索引支持名单加上 `DbType.MySql` 与 `DbType.Oracle`：
  - 语句：`CREATE UNIQUE INDEX <名字> ON <表> (<每列一个 CASE WHEN 条件 THEN 列 END>)`，MySQL 的每个键再加一层括号（函数键），名字用 SqlSugar 的转义；Oracle 的名字不加引号。条件是删除标记为 0，键含可空列时另加这些列 `IS NOT NULL`。
  - 语义：已删除的行整键为空。MySQL 的唯一索引不约束含 NULL 的键，Oracle 不收全为 NULL 的键，所以已删除的行不参与唯一。Oracle 的组合键只要有一列不为空就要比较，可空的键列写进条件后，任一可空列为空的行整键为空、不受约束，与 PostgreSQL 不约束含 NULL 的键一致。MySQL 写同一个条件，两种库的定义一样。
  - 已有同名索引时跳过：这两种库没有 `CREATE INDEX IF NOT EXISTS`，先查数据字典。MySQL 的索引名按表唯一，按表与名字查 `information_schema.statistics`（都不区分大小写）；Oracle 按大写的名字查 `user_indexes`，与 SqlSugar 查已有索引的写法一致。
  - MySQL 的函数键要 8.0.13 起。更早的版本与 MariaDB（`DbType` 同为 MySQL）上建索引的语句失败，`InitTables` 报错。启动核对只看 `DbType`，不查服务器版本。
- 撞上唯一索引的核对（`UniqueKeyConflicts`）不变：它按声明比较未删除的写入项与已提交的行，不依赖索引在库里的写法，MySQL 与 Oracle 上同样返回 `PERSISTENCE_CONFLICT_003`。
- 其余数据库维持不支持（MySQL 与 Oracle 的兼容库、`DbType.MySqlConnector`、达梦、源自 PostgreSQL 而未验证的国产库等）：在真实库上验证过函数索引的建出、约束与再次建表不重复建之后，才加进名单。
- EF Core 不变：EF Core 的模型写不出函数索引，Pomelo MySQL 与 Oracle 的 Provider 建模照旧失败（`PERSISTENCE_UNAVAILABLE_008`）。显式的名字经 `HasDatabaseName` 原样交给它。

## 理由

- 显式名字解决的是已经存在的索引：应用换成 `[EntityIndex]` 后库里的索引原样沿用，不用改名，也就不用为每种数据库写一次性的改名脚本。缺省仍由规则推出，框架自己的表与新应用不受影响。
- 限制成不必转义的标识符：建索引的语句在 PostgreSQL、SQLite 上加引号，在 Oracle 上不加，在 MySQL、SQL Server 上用 SqlSugar 的转义。只收这类名字，各库建出的名字与 `Resolve` 给出的、核对冲突时报告的 `index` 相同，不用分库处理引号与大小写。
- 跨实体同名在建表前失败：显式名字带来的新问题是静默少一个唯一约束，只有在写入出现重复数据时才会发现。在 `InitTables` 里核对成本是一次解析，开发库与测试库在启动时就会暴露。
- 函数索引而不是生成列：不改表结构、不改实体映射，与带条件的索引一样在建表之后补建；语义与 PostgreSQL 的部分唯一索引一致，核对冲突的代码不用改。
- 只加验证过的库：MySQL 与 Oracle 在框架 CI 的服务容器上验证建出、约束、再次建表不重复建与写入冲突的核对；兼容库语法相近不等于行为一致，维持 ADR-0084 的做法。
- EF Core 不跟进：模型里没有函数索引，写原生 SQL 的迁移会让模型快照与库不一致；EF Core 在这两种库上维持启动失败，选择看得见。

## 影响

- 公开 API（Unshipped）：新增 `EntityIndexAttribute.Name`。`EntityIndex.Name` 的含义扩为"显式的或推出的名字"。
- 行为：SqlSugar 在 MySQL 与 Oracle 上登记了软删除实体的唯一索引，原先启动失败（`PERSISTENCE_UNAVAILABLE_008`），现在建出函数索引、写入冲突返回 `PERSISTENCE_CONFLICT_003`。`InitTables` 新增一种失败：一起建表的实体之间 `[EntityIndex]` 同名。
- 迁移（BasicApp）：`[ActiveUniqueIndex(列…, Name = "ux_…")]` 换成 `[EntityIndex(列…, IsUnique = true, Name = "ux_…")]`，删掉自己的补建代码；名字与定义不变，已有的库不用动。并发撞键从 500 变成 409 `PERSISTENCE_CONFLICT_003`。
- 测试：显式名字的解析与声明错误；SQLite、PostgreSQL、SQL Server、MySQL、Oracle 上显式命名、键含可空列的部分唯一索引（区域为空的行不受约束）；MySQL 与 Oracle 上函数索引的建出、约束、再次建表不重复建，仓储同一键反复删除不冲突、未删除的同键返回 `PERSISTENCE_CONFLICT_003`；两个实体同名时 `InitTables` 在建表前失败；不支持的库改用达梦验证启动失败。
- 文档：RFC-0005「索引声明」；docs 的表结构、SqlSugar、EF Core 指南与 Persistence.Abstractions、Persistence.SqlSugar、Persistence.EntityFrameworkCore 包页面。
