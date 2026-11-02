# ADR-0101 工作流查询列改名与兼容库按通配匹配已定、EF Core 在 SQLite 上字符串匹配统一为 LIKE

## 状态

已采纳（`spec/rfcs/0005-persistence.md`、`spec/rfcs/0021-workflow.md` 随之修订）。第一节把 ADR-0099 第一节自定的列改名定为已定；第二节把 ADR-0098 的待定项（兼容库是否也改写）与运行时数据源定为已定，并承接 ADR-0099 第四节留给兼容库的工作流关键字；第三节修订 ADR-0098 与 RFC-0005「规约里的字符串匹配」的大小写一行（原来「不统一，照各库」，EF Core 在 SQLite 上三个方法三种规则）。

## 日期

2026-11-02

本 ADR 记录第十批回灌（第九批待定项的拍板，`n-batch-backlog.md`）里 n-misc 分区的三项决定：第 1、2、3 条各一节。来由见第九批 `m-round-summary.md` 第 4 节与分区汇报 `m-sugar.md`、`m-wf.md`。

## 一、工作流关键字查询列改名定为已定（第 1 条）

### 背景

ADR-0099 第一节把关键字的匹配范围对齐 4.x 时，顺带把实例行的 `SearchTitle` 改名 `SearchText`（实例标题 + 关联 ID）、书签行的 `SearchInstanceTitle` 改名 `SearchInstanceText`（实例标题 + 关联 ID + 定义名）。改名是分区自定的，第九批收口时列为待定：若想保留旧列名，只要改回两个属性名与 PublicAPI。

### 决策

用户拍板接受改名，定为已定，不改代码。RFC-0021 的 `Workflow.Database` 一段写明两处列名已定；docs-next 的 Workflow.Database 包页「从上一版升级」一段同步。

### 理由

列里已经不只是标题，旧名会误导回填与排查；5.0 还在预览、BasicApp 的 6.0.0 基线还没发布，改名的代价只是重新生成基线。

### 影响

无新的变化，ADR-0099 第一节的升级说明照旧：实例表与书签表加新列、旧列留着（框架不再读写），回填式不变，BasicApp 的 6.0.0 基线要重新生成。

## 二、SqlSugar 的兼容库与运行时数据源维持按通配匹配，定为已定（第 2 条）

### 背景

ADR-0098 让 SqlSugar 的 `string.Contains` / `StartsWith` / `EndsWith` 在 SQLite、PostgreSQL、MySQL、SQL Server、Oracle 上按字面匹配（`LIKE … ESCAPE '!'`），只改写按连接配置 `DbType` 是这五种的；兼容库（达梦、人大金仓、openGauss、GaussDB、TiDB、OceanBase 等）沿用 SqlSugar 原来不转义的翻译，留作待定。运行时数据源（ADR-0078）的客户端不施加约定，同样沿用原来的翻译。ADR-0099 第四节撤销工作流关键字的全角替换后，兼容库上工作流关键字里的 `%`、`_` 也照库的 `LIKE` 当通配，随这一项待定解决。

### 备选

1. 维持按通配，在真实库上验证之后再加进名单（采纳）。
2. 按「与原库兼容」推定：PostgreSQL 系（openGauss、人大金仓的 PG 模式）按 PostgreSQL 改写，MySQL 系（TiDB、OceanBase 的 MySQL 模式）按 MySQL 改写。兼容库的 `LIKE`、`ESCAPE`、字面量与排序规则未必与原库一致（达梦、人大金仓各有多种兼容模式），推定错了就是生成不合法的 SQL 或按错的规则匹配。
3. 运行时数据源也施加约定：运行时数据源是给代码生成、读外部库结构的应用用的，库的种类由应用的记录决定、可能是任何一种 SqlSugar 支持的库；只登记三个外部函数而不施加整套约定，是双轨，也同样要按名单判断。

### 决策

取备选 1，不改代码。

- 兼容库与其余数据库维持 SqlSugar 原来的翻译，按通配匹配：实参里的 `%`、`_` 是通配符，PostgreSQL 系的兼容库上以反斜杠结尾的实参可能报错。某个库在真实库上验证了 `ESCAPE '!'` 与转义规则之后再加进名单（`LiteralStringMatching` 按 `DbType` 判断的那一处），与部分唯一索引（ADR-0081、ADR-0084）的名单同一做法。
- 运行时数据源的客户端不施加约定，同样按通配匹配，不为它单独登记外部函数。
- 工作流不另做绕法：兼容库上的关键字与应用自己实体的 `Contains` 查询一样，随持久化把库加进名单一并解决（ADR-0099 第四节的结论定为已定）。
- RFC-0005「规约里的字符串匹配」的「字面匹配」一行写明这个例外；SqlSugar 段落、Provider 表、RFC-0021 关键字一段、SqlSugar 包 README 与 docs-next 的持久化指南、SqlSugar 包页、工作流指南写成已定。

### 理由

- 没有真实库就无法验证；名单之外保持 SqlSugar 原来的行为，至少不会因推定错误生成不合法的 SQL。
- 与部分唯一索引同一做法，「加进名单」的门槛一致：在 CI 或本机 Docker 上的真实库跑通本包的字面匹配用例。

### 影响

无行为变化。兼容库上需要字面匹配的应用，在名单扩充之前不要在这三个方法的实参里放 `%`、`_`。

## 三、字符串的包含、前缀与后缀一个库一套大小写规则：EF Core 在 SQLite 上统一译成 LIKE（第 3 条）

### 背景

RFC-0005「规约里的字符串匹配」原来写「大小写与重音不统一，照各库」，并照实写明 EF Core 在 SQLite 上的不一致。用 EF Core 10.0.13 实测（`ToQueryString` 与实跑），SQLite Provider 对三个方法用了三种翻译、两套大小写规则：

| 方法 | 实参是值（常量、闭包变量） | 实参是列或表达式 |
| --- | --- | --- |
| `Contains` | `instr(列, @p) > 0`，区分大小写 | `instr(列, 实参) > 0`，区分 |
| `StartsWith` / `EndsWith` | `列 LIKE @p ESCAPE '\'`（参数值在客户端转义），只对 ASCII 字母不区分 | `substr(列, 1, length(实参)) = 实参`，区分 |

同一个库上 SqlSugar 一律是 `LIKE … ESCAPE '!'`（ADR-0098），只对 ASCII 字母不区分。于是 `x.Name.Contains("casekey")` 在 EF Core 上不命中 `CaseKey`、`x.Name.StartsWith("casekey")` 命中，换成 SqlSugar 两个都命中。其余库照实核对（本机 Docker，与 CI 同镜像）：

| 库 | EF Core 的翻译 | 大小写 |
| --- | --- | --- |
| PostgreSQL 17（Npgsql 10.0.0） | 值实参 `LIKE`（参数值转义）、列实参 `strpos` 与 `left` / `right` 比较 | 都区分，与 SqlSugar 的 `LIKE` 一致 |
| SQL Server 2022（10.0.9） | 值实参 `LIKE … ESCAPE '\'`、列实参 `CHARINDEX` 与 `LEFT` / `RIGHT` 比较 | 都按排序规则（缺省不区分），与 SqlSugar 一致 |
| Oracle Free 23（Oracle.EntityFrameworkCore 10.23） | 值实参 `LIKE … ESCAPE '\'`、列实参 `INSTR` 与 `SUBSTR` 比较 | 都区分，与 SqlSugar 一致 |

三种库上三个方法本来就一致、也都按字面；只有 SQLite 不一致。MySQL 的 EF Core Provider（Pomelo）没有 EF Core 10 的版本可用，没有实测。

### 备选

1. 维持，只在 RFC 里照实写：同一个库换 Provider、同一 Provider 换方法结果都会变，应用要记住 SQLite 上 EF Core 的特例。
2. SQLite 上三个方法都区分大小写（`instr` 与 `substr` 比较，或 `PRAGMA case_sensitive_like`）：EF Core 与 SqlSugar 在 SQLite 上又不一致，除非 SqlSugar 也跟着改；`case_sensitive_like` 已被 SQLite 标为弃用，而且是连接级开关、影响应用自己写的 `LIKE`。
3. SQLite 上三个方法都译成 `LIKE … ESCAPE`（采纳）：只对 ASCII 字母不区分大小写，与 SqlSugar 在 SQLite 上同一套规则；`StartsWith` / `EndsWith` 的值实参本来就是这样，变的只是 `Contains` 与列实参。
4. 两边各加 `lower()` 再用 `instr` / `substr`：对 ASCII 等价于 `LIKE`，但规则是另造的，与 `LIKE`（及 SqlSugar）只在缺省配置下巧合一致。

实现方式另有几个选择：

- 只改仓储里的规约（表达式改写）：应用直接用 `DbContext` 写的 LINQ 不变，同一个库上仍然两套规则。
- 方法翻译插件直接接管 `string.Contains`：插件先于 Provider 内置的翻译，`Contains` 可以这样改；但 `StartsWith` / `EndsWith` 在 SQLite 的 SQL 翻译访问器里先于方法翻译器就被处理掉，插件拿不到。
- 替换 SQLite 的翻译访问器工厂或预处理器：要继承 `Microsoft.EntityFrameworkCore.Sqlite` 的内部类型，本包不引用任何 SQLite 包，也不该依赖 EF Core 的内部 API。
- 拦截器改写成 `EF.Functions.Like(x, 转义后的模式, "\\")`：不用插件，但投影里落到客户端求值的部分会调用 `EF.Functions.Like` 而抛出异常。
- 查询表达式拦截器 + 方法翻译插件（采纳）：拦截器在查询编译之前（参数已提取）把这三个方法换成本包的标记方法，插件把标记方法译成 `LIKE`；标记方法有 .NET 的方法体，落到客户端求值时与原来一样。两者都是 EF Core 公开的扩展点，经特性加在 `DbContext` 选项上，应用不用改代码。

### 决策

取备选 3。

- RFC-0005「规约里的字符串匹配」的大小写一行改为一个库一套规则：同一个库上，三个方法之间、实参是常量、闭包变量还是列或表达式之间，大小写规则都相同，换 Provider 也相同；规则照那个库，不统一成区分或不区分（内存实现区分；SQLite 上 EF Core 与 SqlSugar 都只对 ASCII 字母不区分；PostgreSQL、Oracle 区分；SQL Server、MySQL 按排序规则）。重音另起一行照排序规则。SqlSugar 开了 `MoreSettings.EnableILike` 是应用有意的选择，不在此列。
- `EntityFrameworkCorePersistenceFeature<TContext>` 构建 `DbContext` 选项时（在应用的配置回调之前）加上：
  - `SqliteStringMatchingInterceptor`（`IQueryExpressionInterceptor`，无状态单例）：Provider 是 SQLite 时，把 `string.Contains` / `StartsWith` / `EndsWith` 的单实参重载（字符串或字符，字符常量换成字符串常量，其余字符实参调 `ToString()`、SQL 里是 `CAST(… AS TEXT)`）换成 `SqliteStringMatching` 的标记方法；其余 Provider 原样返回；带 `StringComparison` 等多个实参的重载不在契约内，不改写。
  - `SqliteStringMatchingExtension`（`IDbContextOptionsExtension`）把 `SqliteStringMatchingTranslator`（`IMethodCallTranslatorPlugin`）登记到 EF Core 的内部服务容器。插件只认标记方法，译成 `被匹配 LIKE 模式 ESCAPE '\'`：实参是字符串常量时在翻译时转义（反斜杠、`%`、`_`，先转义反斜杠）并拼上 `%`；是参数、列或表达式时在 SQL 里以三层 `replace` 转义、以 `||` 拼上 `%`，并与上「实参 IS NOT NULL」——EF Core 按 C# 语义把字符串拼接里的 NULL 当空串（加 `COALESCE`），不加这一条 NULL 实参会变成匹配全部的 `%%`；实参不可空时 EF Core 会把这一条去掉。NULL 实参不匹配、取反照 EF Core 对 `LIKE` 的二值语义，与原来一样。
  - 转义符是反斜杠：SQLite 的字面量里反斜杠没有特殊含义，与 EF Core 自己在 SQLite 上的 `LIKE` 同一个转义符。
- 标记方法的方法体按 .NET 原方法计算（`Contains` 序数比较，`StartsWith` / `EndsWith` 当前文化），只在投影落到客户端求值时执行。
- 范围：特性建出的 `DbContext` 上的全部 LINQ——仓储的规约、应用直接写的查询、投影、子查询、参数化集合（`json_each`）里的匹配、编译查询。不在范围内：应用的全局查询过滤器（`HasQueryFilter` 在拦截之后才展开）、不经特性自己建出的 `DbContext`、应用用 `UseInternalServiceProvider` 提供的 EF Core 内部服务容器（扩展的服务不会登记，标记方法译不出，这种用法不受支持）。
- 一致性套件新增用例 11f `List_StringContainsStartsWithEndsWith_AgreeOnLetterCase`：多租户实体的笔记 `ACME`、租户 `acme`，三个方法 × 闭包变量 / 常量 / 租户列九种写法命中的结果必须相同（全都命中或全都不命中，不断言是哪一种）。
- 没有公开 API、错误码、消息键、配置键的变化；本包的新类型都是 internal。

### 理由

- 目标是「同一个库换 Provider 结果一致、同一 Provider 三个方法一致」：SQLite 上 SqlSugar 已经是 `LIKE`，`StartsWith` / `EndsWith` 的值实参在 EF Core 上也已经是 `LIKE`，把剩下的 `Contains` 与列实参并过去改动最小，不动 SqlSugar。
- 用 `LIKE` 本身而不是另造等价规则（`lower()`），两个 Provider 由同一个库函数决定大小写，SQLite 的配置怎么变两边都一样。
- 字面匹配不丢：常量在翻译时转义，参数与列在 SQL 里转义，SQL 文本里只有参数名或列名，没有实参的值。
- 拦截器与插件都是 EF Core 的公开扩展点，不依赖 SQLite 包与 EF Core 内部 API；改写只在首次编译某个查询形状时发生，之后走 EF Core 的编译缓存。

### 影响

- 行为变化（EF Core + SQLite，特性建出的 `DbContext`）：`Contains` 原来区分大小写，现在只对 ASCII 字母不区分（`x.Name.Contains("casekey")` 命中 `CaseKey`）；`StartsWith` / `EndsWith` 的实参是列或表达式时同样从区分变成只对 ASCII 不区分。值实参的 `StartsWith` / `EndsWith` 结果不变。非 ASCII 字母仍区分。按字面不变。字符实参与字符串一样。
- SQLite 的 `LIKE` 模式最长 50000 字节（`SQLITE_MAX_LIKE_PATTERN_LENGTH`），`Contains` 的实参超过即报错 "LIKE or GLOB pattern too complex"（原来的 `instr` 没有这个限制；EF Core 原来的 `StartsWith` / `EndsWith` 与 SqlSugar 在 SQLite 上本来就有）。
- 性能：`instr` 与 `LIKE '%…%'` 都不用索引；列实参多了三层 `replace`。
- PostgreSQL、SQL Server、Oracle 等其余 Provider 不受影响（拦截器只在 SQLite 上改写）。
- 工作流（`Workflow.Database` 在 EF Core + SQLite 上）：关键字查询列与关键字两边都已小写折叠（ADR-0099 第四节），`Contains` 从 `instr` 换成 `LIKE` 后结果不变，`%`、`_`、反斜杠照旧按字面；RFC-0021 关键字一段里小写折叠的理由随之改写（不再以 EF Core 的 `instr` 为例），折叠照旧要保留。
- 验证：修复前 EF Core 上一致性用例 11f 失败（`contains` 三种写法都不命中，`starts` / `ends` 的值实参命中、列实参不命中），本包新增的 SQLite 用例 4 例失败（列实参按字面的一例原本就通过，是守护；「只在 SQLite 上改写」一例随实现加入）；修复后 EF Core 测试 112 / 112。一致性用例 11f 在内存实现（区分）、SqlSugar 的 SQLite（不区分）与 PostgreSQL 17（区分，本机 Docker）上都通过；SqlSugar 测试在 PostgreSQL、SQL Server 2022、Oracle Free 23 上 216 通过、只跳过 MySQL 的 14 例；`Workflow.Database` 在 EF Core + SQLite 上的关键字用例照旧通过。修改验证：去掉「实参不为 NULL」一条，NULL 实参的用例失败。
