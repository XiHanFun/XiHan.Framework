# ADR-0232 SQL Server 与 Oracle 的字符串列存 Unicode、按字符计长

## 状态

已采纳（`spec/rfcs/0005-persistence.md` 随之修订）。

## 日期

2026-11-16

## 背景

`[StringLength(n)]` 的意思是最多 n 个字符，`SqlSugarConventions` 把它定成列长。SqlSugar 在 SQL Server 与 Oracle 上都把字符串建成 `varchar(n)`：

- SQL Server：`varchar` 按排序规则的代码页存，缺省的 `SQL_Latin1_General_CP1_CI_AS` 没有汉字，写进去的中文读回来是 `?`，数据直接丢了。BasicApp 在 SQL Server 上跑集成测试时，导出任务的失败原因读回来是 `???? 'identity.users.read'?`。
- Oracle：`VARCHAR2(n)` 的长度按库的 `NLS_LENGTH_SEMANTICS` 解释，缺省是字节。AL32UTF8 的库里一个汉字 3 字节，`varchar(10)` 只装得下 3 个汉字，超出即 ORA-12899。

SqlSugar 有连接级的开关 `MoreSettings.SqlServerCodeFirstNvarchar` 与 `OracleCodeFirstNvarchar2`，但要每个应用自己打开；Oracle 的那个只在建表时换类型，`InitTables` 给已有的表加列时仍是 `varchar(n)`。

## 备选

1. 约定把有长度的字符串列定为 SQL Server `nvarchar(n)`、Oracle `nvarchar2(n)`：建表与加列都走实体的列类型。
2. 应用自己打开 SqlSugar 的两个开关：每个应用都要知道，Oracle 加列的问题还在。
3. Oracle 上连接打开后执行 `ALTER SESSION SET NLS_LENGTH_SEMANTICS = CHAR`：`varchar2(n CHAR)` 按字符计长，但一列仍不能超过 4000 字节，3 字节的汉字最多 1333 个；每个连接还要多一次往返。

## 决策

采用备选 1：类型为 `string`、有长度（`Length > 0`）、实体没有给列类型的列，SQL Server 上 `DataType` 定为 `nvarchar`（长度超过 4000 时 SqlSugar 建成 `nvarchar(max)`），Oracle 上定为 `nvarchar2`。长文本（`[LongText]`：SQL Server `nvarchar(max)`、Oracle `clob`）与实体自己给了列类型的不动。Oracle 的 `nvarchar2` 最长 2000 个字符，更长的列用 `[LongText]`；超过 2000 的列建表时 Oracle 以 ORA-00910 拒绝。

新增 `TextColumnTests`：在 SQL Server 与 Oracle 上，建表的列与之后加的列都是 Unicode 类型、长度 10，都装得下 10 个汉字。

## 理由

- 列长按字符解释、存 Unicode，与 PostgreSQL、MySQL（utf8mb4）的 `varchar(n)` 一致。
- `nvarchar` / `nvarchar2` 用 UTF-16，任何文字都存得下；Oracle 上一个字符固定 2 字节，2000 个字符一定装得下，备选 3 的上限随文字而变。
- 列类型写在实体映射上，建表与加列走同一条路，不依赖连接开关或会话设置。

## 影响

- 建表变化：SQL Server 与 Oracle 上新建的表与新加的列，有长度的字符串列是 `nvarchar(n)` / `nvarchar2(n)`。已经建成 `varchar(n)` 的列不改，里面已经变成 `?` 的数据也找不回来。
- 长度超过 2000 的字符串列在 Oracle 上建不出来，要改成 `[LongText]`。
- 公开 API、错误码、消息键、配置键不变。
