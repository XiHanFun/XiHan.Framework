# ADR-0100 SqlSugar 的 PostgreSQL 连接固定 standard_conforming_strings = on

## 状态

已采纳（`spec/rfcs/0005-persistence.md` 随之修订：Provider 表 SqlSugar 一行）。处理 ADR-0098「影响」里记下而没有处理的那一条（PostgreSQL `standard_conforming_strings = off` 下 SqlSugar 的多行写入失败）；不修订已有决定。

## 日期

2026-11-02

## 背景

第九批 m-sugar 在验证字符串按字面匹配时顺带发现：PostgreSQL 会话的 `standard_conforming_strings` 是 off 时，SqlSugar 一次写入多行、值里含反斜杠，生成的 SQL 不合法。本项按用户要求修掉它。

根因（SqlSugar 5.1.4.221 反编译核对）：SqlSugar 在若干路径上不绑定参数，而是把字符串值内联成 `'…'` 字面量，转义只做 `DbExtensions.ToSqlFilter`——把单引号写成两个（另有一个只在值里同时出现 `)` 与 `\''` 时才加倍反斜杠的启发式）。这只在 `standard_conforming_strings = on` 时正确；off 时普通字面量里的反斜杠是转义符。内联的路径：`PostgreSQLInsertBuilder` 的多行插入、`PostgreSQLUpdateBuilder` 的批量更新（`UPDATE … FROM (VALUES …)`）、按实体删除（`DeleteableProvider` 按主键 `IN (…)`，单个实体也是）、`Queryable.In(…)` / `Deleteable.In(…)`（`ToJoinSqlInVals`）、集合 `Contains`（`ContainsArray`）、`Storageable`。单行插入、单行更新、`UseParameter()` 的插入与更新、`Fastest` 的二进制 `COPY`、规约里的 `==` 与 `Contains` / `StartsWith` / `EndsWith`（ADR-0098）是参数，不受影响。

在 PostgreSQL 17 上实测（库级 `ALTER DATABASE … SET standard_conforming_strings = off`，值 `a\b`、`x\'y`、`tail\`、`line\n`、`tab\t`、`it's`、真换行、`\\`）：

- 值里有反斜杠后跟单引号：上面的内联路径全部报 42601（syntax error）。
- 没有的：**静默改值**——多行插入把 `a\b` 存成 `a` + 退格、`line\n` 存成换行、`\\` 存成一个反斜杠，主键随之变成另一个值；批量更新按改掉的主键匹配不到行、影响 0 行而不报错；`In`、集合 `Contains`、按实体删除静默漏掉这些行（5 个里只命中 1 个）。
- 仓储的 `AddRangeAsync`（多于一项时）、`UpdateRangeAsync`、`RemoveAsync` / `RemoveRangeAsync`、`FindManyAsync` 与批量方法的预查都走这些路径；规约里的集合 `Contains`（如工作流的 `CorrelationIds`）也是。

## 备选

1. **连接串的启动参数固定 on**（采纳）：Npgsql 的 `Options` 在建连时把 `-c standard_conforming_strings=on` 交给服务器，它优先于库、角色与配置文件里的缺省值，连接池复用时的重置（`DISCARD ALL`）也回到它；一处覆盖 SqlSugar 全部内联路径，也覆盖应用直接用客户端写的 `In`、批量写入，没有逐条命令的开销。
2. 每次打开连接执行 `SET standard_conforming_strings = on`：SqlSugar 在 `IsAutoCloseConnection` 下每条命令开关一次连接，多一次往返；会话 `SET` 还会被连接池的重置清掉。
3. 让这些路径都走参数：多行插入与批量更新有 `UseParameter()`，但按实体删除、`In`、`ContainsArray` 没有参数化的入口（SqlSugar 连 `Where("x in (@ids)", 数组)` 也是内联展开）；只改仓储的路径，应用直接用客户端的照样出错。
4. 改成 `E'…'` 字面量：要替换 SqlSugar 的插入、更新、删除构建器与 `ToSqlFilter`，没有扩展点。
5. 启动时查 `SHOW standard_conforming_strings`，off 即失败：不修复，只是把错误提前；设置还可能按角色或库各不相同，查一个连接不代表全部。

## 决策

- `SqlSugarConventions.Apply(config)` 在 `config.DbType == DbType.PostgreSQL` 时，对连接串与读写分离的从库连接串（`SlaveConnectionConfigs`）调内部类 `PostgreSqlStartupOptions.Enforce`：按 `DbConnectionStringBuilder`（`NpgsqlConnectionStringBuilder` 的基类，解析与引号规则相同，不必引用 Npgsql 的类型）解析，启动参数 `Options` 里没有 `standard_conforming_strings` 的就追加 ` -c standard_conforming_strings=on`（已有的启动参数保留）；已经写成 on（`on`、`1`、`true` / `yes` 的前缀，`-c name=value`、`-cname=value`、`--name=value` 三种写法）的原样不动；写成别的值是与 SqlSugar 冲突的显式配置，抛 `ArgumentException`（消息只说这个参数与它的值，不带连接串），方式一装配即失败——不静默改成 on。
- 方式一（特性按回调建客户端）在回调之后施加约定，默认连接、`shard` 连接、命名连接都带上；方式二由应用对每个连接配置调 `Apply`，要在调用之前设好数据库类型与连接串（之后再改的连接串不带这个参数）。
- 不加的：源自 PostgreSQL 的其他 `DbType`（人大金仓、openGauss、GaussDB、瀚高、Vastbase、PolarDB 等，驱动与 SqlSugar 构建器各不相同，没有验证）；运行时数据源（ADR-0078，不施加约定，是外部库）。
- 会话里自己执行 `SET standard_conforming_strings = off` 的不在支持之内（SqlSugar 的内联字面量随之出错）。
- 公开 API、错误码、消息键、配置键都不变；`Apply` 多了一种抛出（`ArgumentException`）。

## 理由

- SqlSugar 内联字面量的转义规则就是 on 的规则；让会话满足它的前提，比逐条路径绕开 SqlSugar 改动小，覆盖面却最全（含应用直接用客户端的代码）。
- 启动参数在建连时施加一次，没有逐条命令的开销；优先级高于库与角色的缺省值，正好覆盖「库或角色被设成 off」这种常见来源。
- 显式写成 off 时报错而不是覆盖：那是应用的明确意图，静默推翻会让应用自己的原生 SQL 换了语义而无人知晓。

## 影响

- 应用自己的连接（经同一个 `ConnectionConfig` 建出的全部连接：仓储、应用直接用客户端的查询与写入、`Ado` 的原生 SQL、`CopyNew` 的副本）都是 on。PostgreSQL 9.1 起缺省就是 on，没改过这个设置的库不变；库、角色或服务器设成 off 的，应用自己的原生 SQL 里普通字面量的反斜杠从转义符变成普通字符（要转义用 `E'…'`），这正是 PostgreSQL 的缺省语义。
- 连接串被重写：按 `DbConnectionStringBuilder` 重新序列化（实测键变成小写、含分号的值加双引号），Npgsql 解析结果相同；连接池按新的连接串分组。
- PgBouncer：按它的文档，1.20 起接受启动参数 `options` 里跟踪的参数，`standard_conforming_strings` 缺省就被跟踪；更早的版本拒绝 `options`，连接即失败——升级 PgBouncer，或在它的 `ignore_startup_parameters` 里加 `options`（那样这个参数被丢掉，会话取服务器的缺省值）。没有在 PgBouncer 上实测。
- 验证：新增 PostgreSQL 门控用例（库级 off：会话是 on 而不带约定的连接是 off；仓储的批量新增、按主键批量查找、规约里的集合 `Contains` 计数、批量更新、批量删除；客户端的多行插入、`In`、集合 `Contains`、批量更新、按实体删除），修复前 3 例全部失败（会话仍是 off；客户端多行插入报 42601；仓储调用以未处理异常结束），修复后通过；不连库的连接串用例 14 例（追加、保留已有参数、不重复、显式 on 不动、显式 off 拒绝且消息不带口令、从库、别的数据库与 PostgreSQL 系其他 `DbType` 不动）。把整个服务器设成 off（`ALTER SYSTEM`）重跑全部 PostgreSQL 门控用例：`Persistence.SqlSugar.Tests` 204 通过、41 跳过（其他库的门控）、0 失败，`Workflow.Database.Tests` 217 / 217。
- 其他数据库的同类核对（同一组值与路径，实测）：
  - SQLite、Oracle（Free 23）：全部路径正确（字面量里反斜杠不是转义符）。
  - MySQL 9.7 缺省模式：多行插入与批量更新正确（插入的字符串走参数，`ContainsArray` 自己加倍反斜杠），但 **`In(…)`、按实体删除（单个与批量）、`Deleteable.In(…)` 出错**——`ToJoinSqlInVals` 只加倍单引号，而 MySQL 缺省把反斜杠当转义符：值里有 `\'` 时报语法错误，没有的静默漏行。`NO_BACKSLASH_ESCAPES` 模式（连接串写了 `NoBackslashEscapes=true`）下反过来：这些路径正确，集合 `Contains` 因为加倍了反斜杠而漏行。MySqlConnector 没有建连时执行命令的选项，两种模式下 SqlSugar 各有一组路径不对，没有一个连接级设置能同时满足，本项没有处理（见待定）。框架自己的字符串主键（租户、调度、工作流定义）都是不含反斜杠与引号的受限标识符，不受影响；受影响的是应用里主键任意取值的字符串主键实体，以及对任意字符串用 `In` / 集合 `Contains` 的查询。
  - SQL Server 2022：字面量路径都正确；但 SqlSugar 的 CodeFirst 缺省把字符串列建成 `varchar`（`MoreSettings.SqlServerCodeFirstNvarchar` 缺省 false），缺省排序规则下中文与表情在所有写入路径（含参数化的单行插入）都存成 `?`——与字面量无关，本项没有处理（见待定）。
- 待定：
  1. MySQL 缺省模式下 `In` 与按主键删除的反斜杠：要么仓储对字符串主键改为自己绑定参数（普通、分表、差异日志三条删除路径都要改），要么等 SqlSugar 上游修 `ToJoinSqlInVals`。
  2. SQL Server 的 `varchar`：约定是否开 `SqlServerCodeFirstNvarchar`（已有的 `varchar` 列要迁移）。
  3. PostgreSQL 系其他 `DbType` 是否也加这个启动参数，要在真实库上验证。
