来源：https://framework.docs.xihanfun.com/persistence/schema

# 建表约定与索引

这一页讲实体怎样映射成表：主键、并发戳、时间列、可空列与字符串长度的约定，`[EntityIndex]` 怎样声明索引、索引名怎样推出、软删除实体的唯一索引为什么是部分唯一索引，以及三个 Provider 各自怎样建表。读完它，你能写出一个在 EF Core 与 SqlSugar 上建出相同表结构与索引的实体。前置知识是[领域模型](/persistence/domain)，Provider 的差异见 [EF Core Provider](/persistence/entity-framework-core) 与 [SqlSugar Provider](/persistence/sqlsugar)。

## 为什么需要

同一个实体要在两个 ORM 上建出同样的表，否则换 Provider 时索引名、列长度、时间列的存储形式都会变，迁移脚本与排障经验也跟着作废。框架把列与索引的声明放在实体上，用与 ORM 无关的注解（DataAnnotations 的 `[StringLength]`、本包的 `[LongText]` 与 `[EntityIndex]`），由各 Provider 的约定翻译成自己的映射：EF Core 经 `ApplyPersistenceConventions`，SqlSugar 经 `SqlSugarConventions`。实体只依赖 `Persistence.Abstractions`，不标 ORM 自己的索引特性。

## 列与建表约定

| 约定 | EF Core | SqlSugar |
| --- | --- | --- |
| 主键 | `Id` 按 EF Core 约定是主键；整数主键默认当作数据库生成，应用自己生成时配 `ValueGeneratedNever()` | 名为 `Id` 的属性是主键，缺省不自增；要自增沿用实体自己的 `[SugarColumn(IsIdentity = true)]` |
| `ConcurrencyStamp` | `ApplyPersistenceConventions(Database)` 设为并发令牌、长度 32 | 长度 32 |
| `TenantId`、`IsDeleted` | `ApplyPersistenceConventions(Database)` 建索引 | 不自动建索引，需要时用 `[EntityIndex]` 声明 |
| `[StringLength(n)]` | EF Core 原生识别为长度 n | 约定映射为长度 n |
| `[LongText]` | 没有上限的字符串本来就建长文本，不需要额外映射 | 映射为各数据库的长文本类型 |
| 没有注解的字符串 | EF Core 缺省（长文本） | SqlSugar 缺省（PostgreSQL 为 `varchar(255)`，超长写入整批失败） |
| `DateTimeOffset` | EF Core 缺省；SQLite 上 `ApplyPersistenceConventions(Database)` 存成 UTC ticks 整数（比较与排序可用，应用已配转换的不动）；库里还是旧的文本形式时启动失败（`PERSISTENCE_VALIDATION_008`） | 经转换器按 UTC 时刻往返 |
| 可空类型（`int?`、`string?`） | EF Core 缺省 | 列可空 |
| 没有公开 setter 的属性、集合属性 | 按 EF Core 约定 | 不映射（字符串、字节数组除外） |
| 加非可空列的缺省值 | 迁移里写 `defaultValue` | PostgreSQL 上非可空的数值、枚举、布尔与 `Guid` 列由约定配 CLR 缺省值，`InitTables` 才能给已有行的表加这类列；SQLite 加成可空列；其他数据库与字符串、日期时间列新列要声明成可空 |

字符串列的长度用 DataAnnotations 的 `[StringLength(n)]` 声明，不限长度的文本列用 `Persistence.Abstractions` 的 `[LongText]`。SqlSugar 约定也认 DataAnnotations 的 `[MaxLength(n)]` 与无参 `[MaxLength]`，但它们的构造函数带 `RequiresUnreferencedCode`，Native AOT 项目的分析器拒绝，这类项目只用 `[StringLength(n)]` 与 `[LongText]`。框架自己的数据库记录全部显式声明字符串长度，架构测试检查这一点，应用实体建议同样声明。

时间列一律用 `DateTimeOffset`，Provider 按 UTC 时刻写入与读回；只有 SqlSugar 分表实体的 `[SplitField]` 列是 `DateTime`（SqlSugar 按它推表名）。

## 最小示例

一个带长度声明、长文本列、并发戳与两个索引的实体：

```csharp
using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace SimpleApp.Orders;

[EntityIndex(nameof(TenantId), nameof(Title), IsUnique = true)]   // UX_OrderNote_TeId_Ti
[EntityIndex(nameof(CreatedAt))]                                   // IX_OrderNote_CrAt
public sealed class OrderNote : IEntity<Guid>, IMultiTenant, IHasCreationTime, IHasConcurrencyStamp
{
    public Guid Id { get; init; }

    [StringLength(128)]
    public string TenantId { get; set; } = string.Empty;

    [StringLength(256)]
    public string Title { get; set; } = string.Empty;

    // JSON 文档、模板这类没有上限的内容用长文本列
    [LongText]
    public string Body { get; set; } = string.Empty;

    public string? Remark { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
```

登记它（`AddRepository<OrderNote, Guid>()` 或生成器）并开启建表（EF Core 的 `EnsureCreated`、SqlSugar 的 `InitTables`）后，两个 Provider 建出的表都有：`Id` 主键，`TenantId` 长度 128，`Title` 长度 256，`Body` 长文本，`Remark` 可空，`ConcurrencyStamp` 长度 32，以及两个同名索引 `UX_OrderNote_TeId_Ti`（唯一）与 `IX_OrderNote_CrAt`。EF Core 还会给 `TenantId` 单独建索引并把 `ConcurrencyStamp` 设为并发令牌。同一租户下 `Title` 重复的写入返回 `PERSISTENCE_CONFLICT_003`。

## 索引

索引声明在实体上，用 `Persistence.Abstractions` 的 `[EntityIndex]`，不标 SqlSugar 的 `[SugarIndex]` 或 EF Core 的 `[Index]`，换 Provider 不改实体。

| 规则 | 内容 |
| --- | --- |
| 列 | 按声明顺序、全部升序；列必须是实体的公开属性；一个实体可以声明多个索引 |
| 唯一 | `IsUnique = true` 是唯一索引，业务上唯一的键（租户 + 名字之类）这样声明 |
| 名字 | 由规则推出，不手写：`IX_`（唯一 `UX_`）+ 表名 + 每列一段缩写，缩写用下划线连接；属性名每个词取前两个字符（`TenantId → TeId`、`NextFireAt → NeFiAt`）；表名里字母、数字、下划线以外的字符换成下划线；全名不超过 63 个字符，超过即报错、不截断 |
| 建出 | EF Core 由 `ApplyPersistenceConventions(Database)` 映射为 `HasIndex(列).IsUnique(…).HasDatabaseName(名字)`；SqlSugar 经表服务回调转成 `SugarIndexAttribute`，`InitTables` 按名字查不到才建，已有的不改不删。两个 Provider 建出同名的索引 |
| 声明错误 | 没有列、列名重复、不是公开属性、名字超长、同名或同列：建模 / 建表时抛出 `InvalidOperationException`，启动失败 |
| 内存实现 | 不建索引、不校验唯一性 |

`EntityIndexes.Resolve(type, table)` 是两个 Provider 共用的解析入口，返回每个索引的名字、列、`IsUnique` 与 `ExcludesDeleted`；应用自己的建表工具也可以用它。

### 软删除实体的唯一索引

实现 `ISoftDeletable` 的实体上的唯一索引只约束未删除的行：它是部分唯一索引，列就是声明的列，条件 `IsDeleted = false`。同一业务键删除、重建、再删除任意多次都不冲突，未删除的行之间唯一。

| 数据库 | 支持 | 建法 |
| --- | --- | --- |
| PostgreSQL、SQLite | 支持 | EF Core 加 `HasFilter`；SqlSugar 的 `InitTables` 建表后补建 `CREATE UNIQUE INDEX IF NOT EXISTS … WHERE IsDeleted = false / 0` |
| SQL Server | 支持 | EF Core 加 `HasFilter`；SqlSugar 建筛选索引 `WHERE [IsDeleted] = 0`（键含可空列另加 `IS NOT NULL`） |
| MySQL 系、Oracle 系，以及没有验证过的 PostgreSQL 衍生库（人大金仓、openGauss 等） | 不支持 | 有这样的声明即启动失败（`PERSISTENCE_UNAVAILABLE_008`），不退化成普通唯一索引 |

在不支持的数据库上要唯一约束，就把 `IsDeleted` 显式写进列（普通唯一索引，同一业务键最多一行已删除），或不声明唯一。SqlSugar 的部分唯一索引不交给 SqlSugar（它写不出条件），由 `SqlSugarConventions.InitTables` 建表之后补建，应用自己给租户库建表时也要经它；库里已有同名的带 `IsDeleted` 列的唯一索引时 `IF NOT EXISTS` 会跳过，要先手动删掉那个索引。

### 唯一键冲突

写入撞上唯一索引（并发写入同一业务键、同一批两项同键、把键改成已有的值）返回 `PERSISTENCE_CONFLICT_003`（参数 `index` 是索引名），这次写入的项统一报告，调用随后回滚；重试时先查到已有的行，按更新处理。关系型 Provider 在写入失败后核对：这次写入里两项同键，或已提交的另一行同键；软删除实体的部分唯一索引只比较未删除的行。核对不到的（大小写不敏感排序规则下只差大小写、SqlSugar 上与本工作单元先前未提交的行冲突、键含空值）异常原样抛出。

唯一索引里有可空列时，PostgreSQL、SQLite、MySQL 不约束含 NULL 的行（SQL Server 把 NULL 当相等，EF Core 在 SQL Server 上给这种唯一索引自动加 `IS NOT NULL` 过滤）。要让「全局」这类空值参与唯一性，存成非空的哨兵值（如空字符串）。

### 分表实体

SqlSugar `[SplitTable]` 的实体不用 `[EntityIndex]`：SqlSugar 按分表改写索引名时就地改动缓存的特性，名字随建表次数变化。分表实体直接用 `[SugarIndex]` 与 `{split_table}` 占位符，声明 `[EntityIndex]` 抛 `NotSupportedException`。

## 表结构的创建

| Provider | 配置键 | 行为 |
| --- | --- | --- |
| 内存 | — | 不需要 |
| EF Core | `XiHan:Persistence:EntityFrameworkCore:EnsureCreated` | 为 `true` 时启动即 `EnsureCreated`，作用于默认连接与登记了实体的每个命名连接；只用于开发与测试，生产用 EF Core 迁移 |
| SqlSugar | `XiHan:Persistence:SqlSugar:InitTables` | 为 `true` 时启动按已登记的实体建缺表、加缺列（经 `SqlSugarConventions.InitTables`，软删除实体的部分唯一索引随之建出）；删列、改名、改类型不在范围内；只宜在开发与测试开启 |

EF Core 的迁移会把 `ApplyPersistenceConventions` 加进模型的索引、并发令牌与 SQLite 的 UTC ticks 列类型一起生成，所以迁移脚本与 `EnsureCreated` 建出的结构一致。SqlSugar 的生产库结构演进走应用自己的迁移脚本；表里已有重复数据时唯一索引建不出来，`InitTables` 失败、启动失败，先去重。

两个 Provider 建模或建表时都会校验索引声明，不论是否开着建表开关：EF Core 启动时在独立作用域里取一次模型，SqlSugar 对已登记实体检查部分唯一索引是否受当前数据库支持。

## 实体目录

已登记的实体在运行时能经 `IEntityCatalog.ListAsync` 列出：每个实体一条 `EntityMapping`，含表名（映射声明的写法，分表是逻辑名）、列映射、`Protocols`（聚合根、创建 / 修改时间、并发戳、软删除、多租户）与 `[EntityIndex]` 推出的索引。代码生成用它认出哪些表由已登记的实体建出。SqlSugar 取 `EntityMaintenance.GetEntityInfo`；EF Core 取 `DbContext` 模型（影子属性不列）；内存实现的表名取类型名、列取有公开 setter 的非集合属性。配合库结构读取的用法见[连接与运行时数据源](/persistence/connections)。

## 常见问题

### 为什么不用 EF Core 的 `[Index]` 或 SqlSugar 的 `[SugarIndex]`

它们把实体绑到某个 ORM，换 Provider 时索引名与建法都变。`[EntityIndex]` 由两个 Provider 翻译成各自的映射、建出同名索引。只有 SqlSugar 分表实体是例外，必须用 `[SugarIndex]`。

### 索引名能不能自己指定

不能。名字由规则推出，保证两个 Provider 一致、迁移脚本与排障时按同一名字查。超过 63 个字符会报错，这时缩短属性名或减少列数。

### 软删除实体的唯一索引在 MySQL 上启动失败

MySQL 没有部分唯一索引，框架不退化成普通唯一索引（那会让同一业务键删除后无法重建）。两条路：把 `IsDeleted` 显式写进 `[EntityIndex]` 的列，接受「同一业务键最多一行已删除」；或不声明唯一，在操作里先查再写。

### 字符串没写 `[StringLength]` 会怎样

EF Core 建成长文本，没有问题；SqlSugar 沿用缺省（PostgreSQL 是 `varchar(255)`），超长写入整批失败。建议所有字符串列都显式声明长度，没有上限的标 `[LongText]`。

### 已有的表加了一个非可空列，`InitTables` 报错

PostgreSQL 上数值、枚举、布尔与 `Guid` 列由约定回填 CLR 缺省值，能加；非可空的字符串与日期时间列，以及其他数据库上的非可空列，没有缺省值填不进已有行。把新列声明成可空，或走迁移脚本带 `DEFAULT` 子句。

### 值对象与 JSON 列怎样建

框架不自动展开值对象。要存成 JSON 的属性用 `string` 类型加 `[LongText]`，序列化在应用层做；EF Core 也可以用 `OwnsOne` 或 `ToJson()`，在 `OnModelCreating` 里配置，`ApplyPersistenceConventions` 不动已有配置。

## 相关包与下一步

- 相关包：[Persistence.Abstractions](/packages/persistence-abstractions)、[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)、[Persistence.SqlSugar](/packages/persistence-sqlsugar)、[Persistence.Generators](/packages/persistence-generators)
- 规范：RFC-0005《领域协议与持久化》
- 下一页：[连接与运行时数据源](/persistence/connections)，讲连接绑定、命名连接与运行时登记外部库
