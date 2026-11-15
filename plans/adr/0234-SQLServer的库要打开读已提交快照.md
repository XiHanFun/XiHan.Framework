# ADR-0234 SQL Server 的库要打开读已提交快照

## 状态

已采纳（`spec/rfcs/0005-persistence.md` 随之修订）。

## 日期

2026-11-16

## 背景

RFC-0005 规定工作单元的写入提交前对其他工作单元不可见。仓储一致性套件在 SQL Server 上跑时，`UncommittedWrite_IsInvisibleToOtherUnitOfWork` 与 `RolledBackWrites_LeaveStoredEntitiesUntouched` 都卡满 5 分钟的语句超时才失败：用例在一个工作单元里写了行、还没提交，另一个工作单元去读这一行。

SQL Server 缺省的读已提交是加锁读：读要取共享锁，碰上未提交写入持有的排他锁就等，直到写入方提交或回滚。用例（以及应用里在调用事务之外另开连接的读，例如读穿缓存的加载、失败计数）是先读再结束写入，读写互相等待，只能等到超时。PostgreSQL、MySQL（InnoDB）、Oracle 的读已提交都是多版本读，读到提交前的值、不等待。

打开 `READ_COMMITTED_SNAPSHOT` 之后，SQL Server 的读已提交也按行版本读。实测打开之后，SQL Server 上 SqlSugar Provider 的 48 个用例（仓储一致性、库结构、字面匹配、时间精度、字符串列）全部通过。

## 备选

1. 要求 SQL Server 的库打开 `READ_COMMITTED_SNAPSHOT`，写进规范与 Provider 文档。
2. Provider 在每个工作单元开始时把隔离级别设为 `SNAPSHOT`：库同样要先打开 `ALLOW_SNAPSHOT_ISOLATION`，而且快照隔离下的写写冲突以错误 3960 报出，与其他库的读已提交语义不同。
3. Provider 启动时核对库的设置，没打开即启动失败：要另立错误码、消息键与一致性用例；先由使用 Provider 的应用在建库与升级时核对。

## 决策

采用备选 1。RFC-0005「工作单元」的读己之写一条补上：其他工作单元读到的是提交前的值、不等写入方结束，SQL Server 的库要打开 `READ_COMMITTED_SNAPSHOT`。SqlSugar Provider 的测试在建临时库时打开它，并新增 MySQL 与 SQL Server 的仓储一致性测试类（与 PostgreSQL 的同一套用例）；Provider 文档写明这条部署要求。

## 理由

- 这是 SQL Server 自己提供的、与其他三种库同语义的读已提交，改的是库的设置，Provider 的代码与语义都不用分库处理。
- 快照隔离（备选 2）改变的是写写冲突的报错方式，比要求的更多。

## 影响

- 部署要求：SQL Server 的库要执行一次 `ALTER DATABASE [库名] SET READ_COMMITTED_SNAPSHOT ON`（要求库上没有别的连接，或带 `WITH ROLLBACK IMMEDIATE` 断开它们）。行版本存放在 tempdb，写多的库要留出 tempdb 的空间。
- 公开 API、错误码、消息键、配置键不变。
