# ADR-0037 SqlSugar 的 `DateTimeOffset` 读取按 `DateTime.Kind` 还原 UTC；一致性套件在真实 PostgreSQL 上门控运行

## 状态

已采纳（`spec/rfcs/0005-persistence.md` 修订）。

## 日期

2026-10-28

## 背景

BasicApp 6.0 的 N1 第三批（Tenancy）在 PostgreSQL 开发库上发现：真实 UTC 19:34 创建的租户，API 返回的 `createdAt` 是 03:34（+8h）；之后每次 `UpdateAsync`（停用 / 恢复 / 写最后登录时间）`createdAt` 再 +8h，多次登录后创建时间已经跑到"未来"。

定位：列类型正确（`timestamp with time zone`），写入正确（`DateTimeOffsetConverter.ParameterConverter` 传 `UtcDateTime`、Kind=Utc）。SqlSugar 的 PostgreSQL 提供器在静态构造里打开了 `Npgsql.EnableLegacyTimestampBehavior`，legacy 模式下 Npgsql 把 `timestamptz` 换算成本地时间、以 Kind=Local 的 `DateTime` 交出；`DateTimeOffsetConverter.QueryConverter` 对 `DateTime` 一律 `SpecifyKind(Utc)`、不看 Kind，于是把本地墙钟当成 UTC（+8h），再回写时又按 UTC 存，所以累加。SQLite 存文本、按 `AssumeUniversal` 解析，一致性套件在 SQLite 上发现不了。同样受影响的还有框架自己的 `TenantRecord` / `PermissionGrantRecord` / `AuditRecord` 等所有 `DateTimeOffset` 列。

## 备选

1. 关掉 legacy 开关：SqlSugar 在提供器的静态构造里设的，应用层关闭的时机不可控，而且 SqlSugar 其余路径按 legacy 行为写的。
2. 转换器按 Kind 还原：`Kind=Local` → `ToUniversalTime()`，`Utc` / `Unspecified` 维持按 UTC 解释。
3. 覆盖：只加转换器单测；或一致性套件再在真实 PostgreSQL 上跑一遍（环境变量门控）。

## 决策

- 采用 2：`QueryConverter` 的 `DateTime` 分支先匹配 `{ Kind: DateTimeKind.Local }` 换回 UTC；其余 Kind 不变。写入路径不动。
- 采用 3 的两项：`DateTimeOffsetConverterTests` 覆盖 Local / Utc / Unspecified / `DateTimeOffset` / 文本 / 空与参数写入；`PostgresRepositoryConformanceTests` 以 `XIHAN_TEST_POSTGRES`（服务器连接串，不含 `Database`）门控跑完整 `RepositoryConformance`，每个用例建一个随机名的临时库、结束 `DROP DATABASE … WITH (FORCE)`，未设置即跳过（与 Redis / RabbitMQ 同一套路）。套件里"新增后读回等于时钟"与"更新后创建时间不变"两条断言在真库上直接暴露本问题。CI 的 `next-ci` 加 PostgreSQL 服务并设置该变量。

## 理由

- 转换器是框架对驱动形态差异的唯一收口，按 Kind 还原是它本来就该做的；不去动 SqlSugar 的全局开关，避免和它自己的其他路径打架。
- 真库门控而不是只靠单测：这类问题的本质是"驱动在某个模式下交出什么"，只有真实驱动能回答；SQLite 永远测不出时区。

## 影响

- 行为变化只在原先错的地方：PostgreSQL 上读回的 `DateTimeOffset` 从"本地墙钟当 UTC"变为真实 UTC 时刻，回写不再累加。SQLite、SQL Server、MySQL 等交出 Unspecified / Utc 的驱动不受影响。
- 已经被累加写坏的数据不会自动修正：开发库重建即可，生产库需要按偏移回算（本 ADR 不提供脚本）。
- 公开 API 不变；`DateTimeOffsetConverter` 是内部类型。测试新增 `Npgsql` 的编译引用（经 SqlSugarCore 传递）。
