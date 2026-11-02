# ADR-0094 SQLite 时间列启动核对与领导选举的应用生命周期一致性用例

## 状态

已采纳（`spec/rfcs/0005-persistence.md`、`spec/conventions/error-codes.md` 随之修订；补充 ADR-0091 第三节「影响」：已有 SQLite 库被静默误读的风险改为启动时查出）。

## 日期

2026-11-02

## 背景

第六批收口时数据分区留下几项待定（`j-round-summary.md` 第 5 节、`j-data.md` 第 4 节），用户逐条拍板，全按推荐（`k-batch-backlog.md` 第 7、8、9 条）。本 ADR 记其中的两项决定，每项一节。第 8 条（Microsoft.Data.Sqlite 清池竞态、SqlSugar 在 SQLite 上不释放命令，各起草一份上游 issue）只产出共享笔记里的 issue 草稿，不改框架，不进本 ADR。

## 一、EF Core 在 SQLite 上启动时核对时间列的存储形式

### 背景

ADR-0091 第三节让 `ApplyPersistenceConventions(Database)` 在 SQLite 上把模型里全部 `DateTimeOffset` 属性存成 UTC ticks 整数。在那之前建出的库里，没配转换的列是 `TEXT`，存 EF Core 的文本（`2026-11-01 08:00:00+08:00`）；升级后按整数读，SQLite 把文本开头的 `2026` 当整数，旧行被读成 `0001-01-01T00:00:00.0002026`，不报错。第六批只把这条写进文档与 ADR，要不要加启动核对记为待定；用户拍板：加，时间列在库里还是文本就启动失败。

还有一种情况更隐蔽：EF Core 迁移在 SQLite 上改列类型是重建表——建 `INTEGER` 列的新表，原样 `INSERT … SELECT` 搬数据。文本搬进 INTEGER 亲和性的列，因为不是合法的数字字面量，仍按文本存。表结构看起来已经对了，旧行照样被误读。

### 备选

判定方式：

1. 只看表结构：列的声明类型不是 INTEGER 亲和性就不符。开销与行数无关，空表也能查出；查不出「重建了表但没换算」。
2. 只看数据：抽样或全表扫描找 `typeof(列) = 'text'`。能查出重建未换算；空的旧表放过，可 TEXT 亲和性的列会把之后写入的整数也存成文本，下一次启动就被查出来——失败晚到、发生在写过数据之后；全表扫描在大表上每次启动都要读完整张表（没有索引时）。
3. 两者都看：先看声明类型，是 INTEGER 亲和性的再抽样最早写入的固定行数。

抽样怎么取：

1. `SELECT 列 FROM 表 LIMIT n`：表上有覆盖这些列的索引时，查询规划器可能走索引，按值排序；SQLite 里整数排在文本之前，升级后新写的整数行够多时，抽到的全是整数，旧行看不到。
2. `… FROM 表 NOT INDEXED LIMIT n`：强制按 rowid 顺序扫表，取到的是最早写入的行。旧行一定写在升级之前，重建表时 `INSERT … SELECT` 也按原顺序搬，排在最前。

范围：

1. 每个连接都核对模型里全部的表：命名连接的库里那些不归它的表（`EnsureCreated` 按整个模型建、或旧版本建出来的空表）也要求重建，误报。
2. 每个连接只核对落在它上面的实体的表：默认连接是没有登记到命名连接的实体，命名连接是登记到它的实体。

### 决策

判定选 3，抽样选 2，范围选 2；放在 EF Core 特性的启动里，取模型与 `EnsureCreated` 之后，默认连接一次、登记了实体的每个命名连接各一次。

- 核对的列：落在这个连接上的实体映射的表里（取关系模型，从属类型、复杂类型与表拆分的列都在表上），由约定施加了 `UtcTicksConverter` 的列；应用自己配了转换的列不核对。新增内部类型 `SqliteTimestampCheck`。
- 读表结构：每个连接一次 `SELECT m.name, p.name, p.type FROM sqlite_master AS m JOIN pragma_table_info(m.name) AS p WHERE m.type = 'table'`，表名列名按不区分大小写比较。库里没有的表或列跳过。
- 逐列：声明类型不含 `INT`（SQLite 亲和性规则的第一条）→ 不符，空表也算；含 `INT` 的列，每张表一次查询抽样最早的 1000 行（`NOT INDEXED`），每列取查到的非 `integer`、非 `null` 的存储类，有就不符。
- 不打开连接的情况：Provider 不是 SQLite；这个连接上没有要核对的列；库文件还不存在（`IRelationalDatabaseCreator.Exists` 为假）——SQLite 打开不存在的文件会建出空库，核对不能有这个副作用，也就保住「不开 `EnsureCreated` 时启动不碰数据库」在库不存在时的原有行为（健康项照旧 Degraded）。
- 失败：一个连接上的不符列一次列出，抛出携带新错误码 `PERSISTENCE_VALIDATION_008`（`PersistenceErrors.StorageFormatMismatch`）的 `FrameworkException`，消息键 `persistence.schema.timestamp_format_mismatch`（`PersistenceMessages.TimestampFormatMismatch`，10 种语言），参数 `connection`（`default` 或命名连接名）、`columns`（`表.列 (声明类型)`，抽样不符的另带存储类，如 `TimedRow.Due (INTEGER, text)`），文案指向 ADR-0091 第三节的换算式。默认连接不符时不再看命名连接。打不开已有库文件的 SQLite 异常原样抛出。核对只读。
- 不核对：带租户分片的调用里的分片库——调用时才解析，启动时枚举不到，表结构本来由应用管理。
- 不加开关：核对没有关闭的配置项。它只在 SQLite 上、有约定转换的列时才动作，新库与迁移正确的库不受影响；能关掉的核对就是静默误读的后门。

顺带补上 ADR-0091 第三节换算式里丢失的单引号（`'-'`、`'.'`、`'0000000'`，原文缺了引号，照抄执行会报语法错误）；补后的式子由新增测试逐字执行，换算后读回的时刻与原值一致。docs-next 的 EF Core 包页新增「已有 SQLite 库的时间列」，写明核对的范围、失败的样子、开销与修法（换算式与重建表的语句）。

### 理由

- 静默误读比启动失败糟得多：错的时刻会被业务继续使用、写回、参与到期判定，事后难以追查；启动失败把问题留在部署那一刻，修法明确。
- 先看声明类型让最常见的情况（旧版本直接升级、没迁移）零扫描查出，空的旧表也在重建最便宜的时候失败，不会写了数据之后的某次启动才突然失败；抽样只补「重建未换算」这一种，最早的行就是旧行，不需要全表扫描。
- 开销有上界、与表的大小无关：每个连接一次读表结构，每张表至多读 1000 行。实测（Microsoft.Data.Sqlite 10.0.13，同样的两条 SQL，本地 SSD）：1 张 100 万行的表约 1 毫秒，200 张表各 1 万行约 100 毫秒，1000 张表各 100 行约 75 毫秒。所以不设表数上限：只核对一部分表，就有一部分表可能静默误读。
- 按落点分连接，与 `EnsureCreated` 按连接建表、仓储按落点取连接的规则一致，不让不归这个连接的空表制造误报。

### 影响

- 公开 API：`PersistenceErrors.StorageFormatMismatch`、`PersistenceMessages.TimestampFormatMismatch` 两个常量（`Persistence.Abstractions` 的 PublicAPI 同步）；EF Core 包新增内部类型 `SqliteTimestampCheck`，没有公开签名变化，没有新配置键。
- 启动期新增的失败条件：EF Core + SQLite，库里由约定映射成 UTC ticks 的列声明为 `TEXT`（含空表），或最早写入的 1000 行里有文本——启动失败，`KERNEL_HOSTING_002`，原因 `PERSISTENCE_VALIDATION_008`。SqlSugar、内存实现与 EF Core 的其余数据库不受影响。
- 行为：不开 `EnsureCreated` 的 SQLite 应用，库文件存在时启动多一次读表结构与每表一次抽样；库文件被别的进程长时间独占写锁时，启动等到忙等超时失败（原来启动不碰数据库）。
- 测试：EF Core 测试项目新增 14 例——旧库（有行与空表）启动失败、消息列出两列、核对不改库；按 EF Core 迁移的方式重建未换算（另追加 1000 行整数、建覆盖索引）启动失败，并演示直接读确实读成第 1 年；按换算式重建后启动成功、读回同一时刻并按时刻排序；新库空表与写过行后再启动都成功；库文件不存在时启动成功且不建出文件；应用自己配了转换的列不核对；命名连接上的旧库失败、消息是那个连接名；亲和性判定六例。修改验证：去掉 `NOT INDEXED`，重建未换算一例失败；去掉核对，四例失败。
