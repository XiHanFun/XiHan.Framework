# ADR-0098 SqlSugar 的字符串包含、前缀与后缀按字面匹配

## 状态

已采纳（`spec/rfcs/0005-persistence.md` 随之修订：「仓储」下新增「规约里的字符串匹配」，Provider 表 SqlSugar 一行与「一致性」段同步）。不修订已有 ADR。工作流为绕开本缺陷在库里存的折叠值（ADR-0096 的 `WorkflowKeyword`）不在本 ADR 处理，由第九批 m-wf 分区在本项合入 `next` 之后评估能否撤销。

## 日期

2026-11-02

本 ADR 记录第九批回灌第 2 条（`m-batch-backlog.md`）的 SqlSugar 修复部分，来由见第八批 `l-wf` 汇报第 4 节第 1 条。

## 背景

SqlSugar 5.1.4.221 把 `string.Contains` / `StartsWith` / `EndsWith`（以及 `SqlFunc.Contains` 等同名函数）译成不转义的 `LIKE`，各方言的写法（`DefaultDbMethod` 与 `SqliteMethod`、`PostgreSQLMethod`、`MySqlMethod`、`OracleMethod` 的覆写）：

| 数据库 | `Contains(@p)` | `StartsWith(@p)` |
| --- | --- | --- |
| SQLite | `like '%'\|\|@p\|\|'%'` | `like @p\|\|'%'` |
| PostgreSQL | `like concat('%',@p,'%')` | 参数值改成 `@p` + `%`，`like @p` |
| MySQL | `like concat('%',@p,'%')` | `like concat(@p,'%')` |
| SQL Server | `like '%'+@p+'%'` | `like @p+'%'` |
| Oracle | `like '%'\|\|:p\|\|'%'` | `like :p\|\|'%'` |

实参是参数（不拼接），但模式不转义。在 PostgreSQL 17、MySQL 9.7、SQL Server 2022、Oracle Free 23 与 SQLite 上实测（`budget_2026` / `budgetx2026`、`50% off` / `500 off`、`a\b` / `ab`、`[a]b` / `a` 成对的数据）：

- 五种库上 `_`、`%` 都是通配符：`t_2` 命中 `budgetx2026`，`0%` 命中 `500 off`，`%` 与 `_` 命中全部行。
- SQL Server 的 `[a]` 是字符类：命中 `a`、`ab`、`[a]b`。
- PostgreSQL 与 MySQL 的 `LIKE` 缺省以反斜杠转义：`a\b` 命中 `ab`；PostgreSQL 上以反斜杠结尾的实参整条查询报错（22025 "LIKE pattern must not end with escape character"），MySQL 上单个反斜杠什么也不命中。
- SQLite 另有启发式（`SqliteMethod.IsEscapedLikePattern`）：值里有 `\%` 或 `\_` 时补 `escape '\'`，同一个反斜杠在不同的值里含义不同。
- 列作实参（`x.Name.Contains(x.Other)`）同样不转义。

EF Core 按字面匹配（SQLite 上 `Contains` 译成 `instr`，`StartsWith` / `EndsWith` 译成带 `ESCAPE '\'` 的 `LIKE`、参数值先转义），内存实现执行 .NET 本身的方法。第八批的工作流关键字查询因此在库里存折叠值（`% _ [ \` 换成全角同形字符），让三种实现结果一致；当时没有改 SqlSugar 的翻译，留作待定，本批用户拍板在框架侧修好。

## 备选

1. **仓储里的表达式改写**（同 ADR-0035 的 `NullableComparisonRewriter`）：只管经仓储的规约，应用直接用客户端写的查询不变；SqlSugar 的内置函数里也没有带 `ESCAPE` 的 `LIKE`，改写成什么仍要一个自定义翻译。
2. **替换各方言的 `DbMethods`**（继承 `PostgreSQLMethod` 等、覆写三个方法）：`ExpressionContext` 由 `InstanceFactory` 按 `DbType` 名字创建，`DbMehtods` 写死在各上下文的构造函数里，没有公开的注入点；只能逐个查询设 `QueryBuilder.LambdaExpressions.DbMehtods`，删除、更新、子查询各自另建上下文，覆盖不全。
3. **AOP 改写 SQL 文本**（`Aop.OnExecutingChangeSql`）：执行前 SQL 已拼好、参数已绑定，只能按文本猜哪个 `like` 来自这三个方法（应用写的 `SqlFunc.Like` 与原生 SQL 长得一样），参数值还要按名字回查再改，脆弱。
4. **`MoreSettings`**：只有 `EnableILike`（把 `like` 换成 `ilike`），没有转义选项。
5. **改用位置函数**（`instr` / `strpos` / `CHARINDEX` / `LOCATE`，EF Core 对 `Contains` 的做法）：不用转义，但 SQLite 上大小写语义从不区分变成区分，与同库上 `StartsWith` / `EndsWith` 不一致；用户拍板的是 `LIKE` 加转义。
6. **同名外部函数**（采纳）：SqlSugar 解析方法调用时，`MethodCallExpressionResolve.GetMethodValue` 先按（映射后的）方法名查 `ConfigureExternalServices.SqlFuncServices`，查到就调用外部函数、不走内置翻译；外部函数拿到方法模型（各实参的 SQL 文本或参数名、参数列表）、数据库类型与表达式上下文。`Where`、`Select`、删除与更新条件、子查询的解析都带着这份列表。

转义符另有三个选择：反斜杠（与 EF Core 一致）——MySQL 缺省模式下字面量 `'\'` 没有结束（反斜杠转义了引号），写成 `'\\'` 在 `NO_BACKSLASH_ESCAPES` 模式下又是两个字符（"Incorrect arguments to ESCAPE"），PostgreSQL 的 `standard_conforming_strings = off` 同理，写法要随服务器模式变；转义符也作参数传——MySqlConnector 在客户端内联参数，同样落回字面量问题；`!`（采纳）——在五种库的字面量与 `LIKE` 里都没有特殊含义。

## 决策

- `SqlSugarConventions.Apply(config)` 在 `config.ConfigureExternalServices.SqlFuncServices` 登记 `Contains`、`StartsWith`、`EndsWith` 三个外部函数（内部类 `LiteralStringMatching`）。列表里已有同名的不登记：应用在 `Apply` 之前自己登记的优先（SqlSugar 按名字取第一个），重复 `Apply` 不重复登记。方式一（特性按回调建客户端）与方式二（应用自己注册客户端、对每个连接配置调 `Apply`）都经过它，所以经仓储的规约与直接用这个客户端的查询、删除条件、投影、子查询一样。集合的 `Contains` 在解析时先被改名成 `ContainsArray`（`IN`），不受影响。
- 按连接配置的 `DbType`（缺失时用 SqlSugar 交来的类型）在 SQLite、PostgreSQL、MySQL、SQL Server、Oracle 上译成 ` (subject like pattern ESCAPE '!') `：
  - 实参是 SqlSugar 为常量、闭包值或字符建的参数（每个实参一个 `MethodConst` 参数，本次调用独占）：参数值先转义、再按方法拼上 `%`，SQL 里只有参数名，不拼接实参。字符实参建参数时按字符推断了类型，换成字符串值后改为字符串类型。
  - 实参是列或 SQL 表达式（`x.Other`、`lower(@p)`），或参数值为 null：在 SQL 里以嵌套的 `REPLACE` 转义（先替换转义符自己），拼接写法与 SqlSugar 原来的相同（PostgreSQL / MySQL `concat`，SQL Server `+`，SQLite / Oracle `||`），实参为 NULL 时的结果与原来一样。
  - 转义 `%`、`_` 与 `!`；SQL Server 另转义 `[`。Oracle 的转义符后面只能跟 `%`、`_` 或它自己（否则 ORA-01424），其余库不转义 `[`（它在那些库里本来就不特殊）。
  - 连接配置开了 `MoreSettings.EnableILike` 时用 `ilike`（SqlSugar 原来在方法翻译之后做这个替换，外部函数接管后由它自己做）。
  - 带 `StringComparison` 的重载第三个实参照 SqlSugar 原样忽略。
- 其余数据库（MySQL、PostgreSQL、Oracle 的兼容库——TiDB、OceanBase、人大金仓、openGauss、达梦等——以及 ClickHouse、Access 等）没有在真实库上验证，外部函数交回 SqlSugar 原来的翻译（含 `ilike` 替换），行为不变。
- 运行时数据源（ADR-0078）不施加约定，沿用原来的翻译。
- 需要通配的查询用 `SqlFunc.Like(x.Name, pattern)`：它的方法名不是这三个，模式由调用方给出、不转义。
- 公开 API、错误码、消息键、配置键都不变；只是这三个方法在五种库上的翻译变了。
- RFC-0005 新增「规约里的字符串匹配」：三个方法是字面匹配，译成 `LIKE` 的 Provider 必须转义并以参数传值；大小写与重音照各库照实写明（内存实现、EF Core、SqlSugar 在五种库上），不在本条统一。一致性套件新增 `List_StringContainsStartsWithEndsWith_MatchWildcardCharactersLiterally`（闭包变量与常量，`%`、`_`、`[`、反斜杠、`!`、单引号，不断言大小写）。

## 理由

- 外部函数是 SqlSugar 公开的扩展点，按名字优先于内置翻译，一处登记覆盖全部解析路径；`Apply` 本来就是两种装配方式的共同入口，不用再加装配步骤。
- 值实参在客户端转义参数值，SQL 文本不含用户输入，不引入注入面；SqlSugar 的 PostgreSQL `StartsWith` 自己就是这样改参数值的，参数又是每个实参独占的。
- 列实参只能在 SQL 里转义；保留原来的拼接写法，NULL 的语义不变，变化只限于转义。
- `!` 让 SQL 里不出现反斜杠，结果不随 MySQL 与 PostgreSQL 的字面量模式变化；写明的 `ESCAPE` 同时替换了 PostgreSQL 与 MySQL 的 `LIKE` 缺省的反斜杠转义符，SQL Server、Oracle、SQLite 没有缺省转义符，靠它才能转义。
- 只在验证过的库上改写，与部分唯一索引（ADR-0084）「在真实库上验证后才加进名单」一致；兼容库保持原行为，不是假定它们与原库一致。

## 影响

- 行为变化（五种库上，经仓储与直接用施加了约定的客户端）：实参里的 `%`、`_`（SQL Server 还有 `[`）不再是通配符，反斜杠不再是转义符；PostgreSQL 上以反斜杠结尾的实参不再报 22025；SQLite 上值里的 `\%`、`\_` 不再触发 SqlSugar 的启发式转义，按字面是两个字符。依赖 `Contains` 通配的应用代码改用 `SqlFunc.Like`。大小写是否敏感不变（照各库的 `LIKE` 与排序规则）。
- 性能：前缀匹配仍是参数化的 `LIKE @p`，与原来相同；列实参多了三四层 `REPLACE`。
- MySQL 开了 `NO_BACKSLASH_ESCAPES` 的服务器：生成的 SQL 照样成立；但 MySqlConnector 不探测服务器模式，连接串要写 `NoBackslashEscapes=true`，否则驱动按缺省模式给参数里的反斜杠加引号（写入与查询都多一个反斜杠）——这是驱动对所有参数的要求（实测：服务器全局开这个模式而连接串不声明时，写入的 `a\b` 存成 `a\\b`）。PostgreSQL 的 `standard_conforming_strings = off`：查询照样成立；SqlSugar 的多行写入把值内联成字符串字面量、不转义反斜杠，off 下含反斜杠的值让写入失败（与本项无关，没有处理）。
- 验证：本包新增用例在 SQLite 与 PostgreSQL 17（`postgres:17-alpine`）、MySQL 9.7（`mysql:9.7`）、SQL Server 2022（`mssql/server:2022-latest`）、Oracle Free 23（`gvenzl/oracle-free:23-slim-faststart`）上跑经仓储（规约的闭包变量、常量、字符、列实参，计数）与经客户端（查询、`SqlFunc.Contains`、删除条件）的同一组用例，修复前 SQLite 15 例失败、四个库 30 例里 22 例失败（通过的只有大小写与 `SqlFunc.Like` 两组），修复后全部通过；另有不连库的翻译用例（参数值的转义、`ESCAPE`、兼容库沿用原翻译、应用的同名函数优先、重复施加、`EnableILike`、集合 `IN`、投影与子查询）。一致性用例在 SqlSugar（SQLite、PostgreSQL）、EF Core（SQLite）与内存实现上都通过。
- 工作流（ADR-0096）的折叠值仍在：本项合入后由 m-wf 评估能否撤销。
- 待定：兼容库（人大金仓、openGauss、达梦、TiDB 等）是否也改写，要在真实库上验证 `ESCAPE` 与转义规则之后再加进名单。

> 修订（2026-11-02）：「工作流的折叠值仍在」由 ADR-0099 第四节处理：撤销全角替换，只留小写折叠（EF Core 区分大小写，仍要）；兼容库上工作流关键字随本 ADR 的待定项一并解决。

> 修订（2026-11-02）：「影响」里 PostgreSQL `standard_conforming_strings = off` 下多行写入失败的那一条由 ADR-0100 处理：PostgreSQL 连接串的启动参数固定 `-c standard_conforming_strings=on`。

> 修订（2026-11-02）：「待定：兼容库是否也改写」由 ADR-0101 第二节定为已定——兼容库与运行时数据源维持按通配匹配，在真实库上验证之后再加进名单；RFC-0005 大小写一行由 ADR-0101 第三节修订为一个库一套规则，EF Core 在 SQLite 上的三个方法统一译成 `LIKE`。
