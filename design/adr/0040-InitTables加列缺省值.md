# ADR-0040 `InitTables` 给已有行的表加非可空列：PostgreSQL 上配 CLR 缺省值；约定不碰 BCL 类型

## 状态

已采纳（`spec/rfcs/0005-persistence.md` 修订）。

## 日期

2026-10-28

## 背景

BasicApp 6.0 N1 第九批（菜单管理）给已有行的 `sys_menu` 加了非可空的枚举与布尔列，开发库（PostgreSQL，`InitTables` 开启）启动失败：`23502: column "kind" of relation "sys_menu" contains null values`。`SqlSugarConventions` 只按类型标可空，不给非可空列缺省值；SqlSugar 的 CodeFirst 在 PostgreSQL 上加列时于是直接 `ADD COLUMN … NOT NULL`。集成测试每次空库建表，暴露不了。

用临时探针在真实 PostgreSQL 与 SQLite 上实测了 SqlSugar 5.1.4.221 的行为：

- PostgreSQL：列带 `DefaultValue` 时，SqlSugar 先加可空列、用缺省值回填旧行（一次字典更新）、再 `SET NOT NULL`，最后 `SET DEFAULT`。整数、枚举、布尔、Guid、小数都正确回填并读回。此后每次 `InitTables` 它都会对这些列重发一次 `SET DEFAULT`（数据库里的缺省值与实体一致也照发）。
- SQLite：SqlSugar 加列一律是可空列（不带 NOT NULL），旧行读回 CLR 缺省值，本来就不失败；反而给了缺省值后它会走重建列流程，布尔列读回失败。
- 空串缺省值被 SqlSugar 当成没有缺省值：非可空字符串列照样 `ADD COLUMN … NOT NULL`。
- 实测时还暴露了一个既有缺陷：SqlSugar 对它内部承载字典更新的 `Dictionary<string, object>` 也调用实体服务，`SqlSugarConventions` 把其中没有公开 setter 的 `Count` 标成忽略列，字典里名为 `count` 的列于是被丢掉、更新无列可写，报 `Update List<T> need Primary key`。回填就是这样一次字典更新，应用自己的字典更新同样会中招。

## 备选

1. `RFC-0005` 写明 `InitTables` 只负责空库建表，演进一律重建开发库：每次给实体加非空列都要丢开发数据。
2. 约定给所有数据库的非可空列都配缺省值：SQLite 上反而出错，其他数据库未验证。
3. 只在 PostgreSQL 上给非可空的数值 / 枚举 / 布尔 / Guid 列配 CLR 缺省值，其余情况写明不受支持。
4. 框架自己实现一套加列演进（绕开 SqlSugar 的 CodeFirst）：等于重写 CodeFirst 的比对逻辑，与 SqlSugar 的后续行为打架。

## 决策

采用 3，并修正字典更新的既有缺陷。

- `SqlSugarConventions.Apply` 的实体服务在 `Configure` 之后调用 `ConfigureDefault(property, column, config.DbType)`（内部方法，数据库类型在回调执行时读取）：只在 PostgreSQL 上，给非可空、非主键、非自增、实体没有自己声明缺省值 / 列类型 / 转换器的列配 CLR 缺省值——数值与枚举 `0`、布尔 `false`、Guid 全零。只影响建表与加列的 DDL，写入照常带全部列的值。
- 不配的情况写进 RFC-0005：SQLite（不需要也不能配）、其他数据库（未验证）、非可空字符串（SqlSugar 忽略空串缺省值）、日期时间类型（没有可移植的缺省值）。这些列加到已有行的表上时声明成可空，或重建开发库 / 走迁移脚本。
- `Configure` 与 `ConfigureDefault` 对声明在 BCL 类型（命名空间 `System` / `System.*`）上的属性原样交还：约定只管实体。
- `InitTables` 的职责写清：建缺表、加缺列；删列、改名、改类型不在契约内；它是开发与测试的便利，生产库结构演进走迁移脚本。

## 理由

- 缺省值取 CLR 缺省值，与"实体构造后没赋值时的值"一致，不是另造一个业务值；日期时间与字符串没有这样无歧义且可移植的值，所以不配。
- 只在实测过的数据库上生效：SQLite 的反例说明这类行为必须逐库验证，不能按"看起来一样"推断。
- 每次 `InitTables` 重发 `SET DEFAULT` 是 SqlSugar 的行为、不是缺省值写错（实测数据库里与实体一致仍照发），代价只落在开启 `InitTables` 的环境；BasicApp 仅 Development 开启。

## 影响

- PostgreSQL 上开启 `InitTables` 时，非可空数值 / 枚举 / 布尔 / Guid 列带 `DEFAULT`；新建表的 DDL 多出缺省值子句，已有表每次启动对这些列各发一次 `SET DEFAULT`。
- 字典更新里与 `Dictionary<,>` 成员同名（`Count`、`Keys`、`Values`、`Comparer`）的列不再被丢掉。
- 公开 API 不变（`ConfigureDefault` 为内部方法）。
- 测试：`SqlSugarSchemaEvolutionTests`——约定只在 PostgreSQL 配缺省值（另用不同 ConfigId 避开 SqlSugar 按类型与 ConfigId 的映射缓存）、SQLite 与 PostgreSQL（门控）上给已有行的表加非空列后旧行读回 CLR 缺省值 / 新行往返 / 再次建表不失败、字典更新保留名为 `Count` 的列。反向验证：去掉 BCL 判断后字典用例与 PostgreSQL 演进用例都失败。
