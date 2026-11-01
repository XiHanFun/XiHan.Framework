# 持久化

本页讲仓储 `IRepository<TEntity, TKey>` 的读写与批量方法、`EntityQuery<T>` 查询、Provider 的选择、实体登记、软删除与租户过滤以及列与建表约定，适用于在操作契约里读写数据的应用开发者。完整 API 见 [Persistence.Abstractions 包](../packages/persistence-abstractions)。

## 概念

仓储是聚合的集合视图：查找、查询、计数、新增、更新、删除，加上对应的批量方法。它不暴露 `IQueryable`，复杂查询交给 Provider 专属的入口（EF Core 的 `DbContext`、SqlSugar 的 `ISqlSugarClient`）。仓储按实体实现的协议维护创建时间、修改时间、租户、并发戳与软删除（见[领域模型](./domain#实体协议)），可恢复的失败（标识重复、不存在、并发冲突、缺租户）以 `Result` 返回，不抛异常。

仓储只在活动的工作单元里工作。管道的 Transaction 阶段为每次调用开启工作单元，操作契约里注入的仓储直接可用；在 Transaction 阶段之外使用仓储是装配错误，每个方法都抛出携带 `PERSISTENCE_UNAVAILABLE_001` 的 `FrameworkException`。在后台任务里读写见[工作单元与事务](./uow#在管道外开工作单元)。

持久化能力的 ID 是 `persistence.relational@1`，由 Provider 提供；`persistence.document@1` 是预留的能力 ID，5.0 没有提供它的 Provider。

## 选择 Provider

| Provider | 包 | 清单角色 | 适用 | Native AOT |
| --- | --- | --- | --- | --- |
| 内存 | `XiHan.Framework.Persistence.InMemory` | `default` | 开发、原型与测试；进程退出即丢失 | 兼容 |
| EF Core | `XiHan.Framework.Persistence.EntityFrameworkCore` | `provider` | 新项目；应用持有自己的 `DbContext`，用 EF Core 迁移管理表结构 | 不兼容 |
| SqlSugar | `XiHan.Framework.Persistence.SqlSugar` | `provider` | 已有 SqlSugar 代码的项目；多库、按时间分表、差异日志 | 不兼容 |

`Profiles.Standard` 已包含内存实现。应用再 `UseFeature` 一个显式 Provider 就顶替它，被顶替的默认实现不装配、不启动。三个 Provider 通过同一套 `Persistence.Conformance` 用例，业务代码（实体、仓储调用、查询）换 Provider 时不改。

::: warning 内存仓储只用于开发与测试
内存实现每种实体一张有界的表（缺省 100000 条），数据只在进程内。读写交出与存入的都是浅复制的副本，集合、嵌套对象等引用类型字段在副本之间共享，不在隔离范围内。生产环境装配 EF Core 或 SqlSugar Provider。
:::

各 Provider 的装配与差异见 [Persistence.InMemory](../packages/persistence-inmemory)、[Persistence.EntityFrameworkCore](../packages/persistence-entityframeworkcore)、[Persistence.SqlSugar](../packages/persistence-sqlsugar)。

## 登记实体

Provider 不扫描程序集，也不注册开放泛型：应用逐个登记实体，Provider 在配置阶段为每个登记注册闭合泛型的 `IRepository<TEntity, TKey>`。这样换 Provider 不改登记代码，也不需要反射，Native AOT 可用。

1. 实体实现 `IEntity<TKey>`（或 `IAggregateRoot<TKey>`）。
2. 调用 `services.AddRepository<TEntity, TKey>()`。可以在 `AddXiHan()` 之前调用，也可以在特性的 `Configure` 里调用；Provider 装配之后再登记的实体立即注册。
3. 装配一个持久化 Provider 的特性。

```csharp
using SimpleApp.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 由当前装配的持久化 Provider 注册 IRepository<Order, Guid>
builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

实体多了以后，可以引用源生成器 `Persistence.Generators`，由它为本程序集的每个实体生成 `AddGeneratedRepositories()`。SimpleApp 的 `OrdersFeature` 就是这样登记的：

```csharp
public void Configure(FeatureContext context)
{
    // 登记代码由 Persistence.Generators 从实体类生成（Order : IAggregateRoot<Guid>）
    context.Services.AddGeneratedRepositories();
}
```

生成器的引用方式与诊断见 [Persistence.Generators 包](../packages/persistence-generators)。

## 读取

| 方法 | 语义 |
| --- | --- |
| `FindAsync(id, ct)` | 返回 `ValueTask<Maybe<TEntity>>`；已软删除或不属于当前租户的实体视为不存在 |
| `FindManyAsync(ids, ct)` | 一次往返；只返回存在且可见的实体，顺序按入参，缺失的省略，重复的标识只返回一次 |
| `ListAsync(query, ct)` | 按 `EntityQuery<TEntity>` 过滤、排序、分页 |
| `CountAsync(query, ct)` | 计数，忽略查询的排序与分页 |

`EntityQuery<T>` 是不可变的 `record`，用链式方法或 `with` 派生：

| 成员 | 缺省 | 说明 |
| --- | --- | --- |
| `EntityQuery.All<T>()` | — | 无条件、缺省分页，按类型缓存同一实例 |
| `Where(spec)` | 不过滤 | 过滤规约；多次调用按 And 组合 |
| `OrderBy(key, descending)` | 不排序 | 追加一级排序，多次调用按声明顺序叠加 |
| `Page(skip, take)` | `Skip` 0、`Take` 1000 | `take` 大于 `EntityQuery.DefaultMaxTake`（1000）抛出携带 `PERSISTENCE_VALIDATION_005` 的 `FrameworkException`，调用里由 Transaction 阶段转为同码的校验结果（HTTP 400）；`skip` 小于 0、`take` 小于 1 是编程错误，抛出 `ArgumentOutOfRangeException`。不截断；缺省 `Take` 就是这个上限，要更多行就分页 |
| `IncludeDeleted` | `false` | 为 `true` 时包含已软删除的实体 |
| `Within(from, to)` | 不限定 | 时间范围提示，两端都含；只影响按时间分表的 Provider 扫描哪些表，不过滤行 |

下面的操作按金额过滤、按创建时间倒序分页，并返回总数。分页参数来自请求时用 `[Range]` 约束：超出范围由 Validate 阶段返回 `KERNEL_VALIDATION_003` 并指出字段，进不到处理器。没有约束时，超过上限的 `take` 在构造查询时被拒绝，调用以 `PERSISTENCE_VALIDATION_005`（HTTP 400，参数 `take`、`max`）结束、已做的写入回滚；负的 `skip` 与小于 1 的 `take` 则是 `ArgumentOutOfRangeException`，成为 `KERNEL_UNEXPECTED_001`，所以两头都要约束。`OrderResponse` 来自 SimpleApp；新的请求与响应类型要登记进应用的 `JsonSerializerContext`（SimpleApp 的 `SampleJsonContext`），否则 `Kernel.Generators` 报 `XHG002`。

```csharp
using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

public sealed record SearchOrdersRequest(int MinAmount, [Range(0, int.MaxValue)] int Skip, [Range(1, EntityQuery.DefaultMaxTake)] int Take);

public sealed record OrderPage(IReadOnlyList<OrderResponse> Items, long Total);

[Operation("orders.search", Permission = "orders.read")]
public sealed class SearchOrders(IRepository<Order, Guid> orders) : IOperation<SearchOrdersRequest, OrderPage>
{
    public async ValueTask<Result<OrderPage>> HandleAsync(SearchOrdersRequest request, InvocationContext context)
    {
        // 租户与软删除过滤由仓储施加，这里只写业务条件
        var query = EntityQuery.All<Order>()
            .Where(Specification.Of<Order>(o => o.Amount >= request.MinAmount))
            .OrderBy(static o => o.CreatedAt, descending: true)
            .Page(request.Skip, request.Take);

        var items = await orders.ListAsync(query, context.CancellationToken);
        var total = await orders.CountAsync(query, context.CancellationToken);
        return new OrderPage(items.Select(static o => new OrderResponse(o.Id, o.Name, o.Amount, o.CreatedAt)).ToList(), total);
    }
}
```

要看到已软删除的实体，或给分表实体限定扫描范围：

```csharp
var withDeleted = EntityQuery.All<Order>() with { IncludeDeleted = true };
var lastMonth = EntityQuery.All<Order>().Within(DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow);
```

`Within` 不是过滤条件：不分表的实体与不支持范围的 Provider 忽略它，范围之外的行照常返回，要按时间过滤另写规约。目前只有 SqlSugar Provider 的分表实体使用它。

## 写入

| 方法 | 写入的字段 | 可恢复失败 |
| --- | --- | --- |
| `AddAsync(entity, ct)` | 创建时间、当前租户、新的并发戳 | `PERSISTENCE_CONFLICT_002` 标识已存在（含已软删除与其他租户的行）；`PERSISTENCE_VALIDATION_001` 多租户实体没有当前租户 |
| `UpdateAsync(entity, ct)` | 修改时间、新的并发戳；创建时间、租户与删除标记沿用存储值 | `PERSISTENCE_NOTFOUND_001` 不存在或不可见；`PERSISTENCE_CONFLICT_001` 游离实例的并发戳与当前值不同 |
| `RemoveAsync(entity, ct)` | 软删除实体在存储的当前行上写删除标记与删除时间；其余物理删除 | `PERSISTENCE_NOTFOUND_001` |

写入成功后，调用方传入的实例上就是协议字段的值：新增后的创建时间与并发戳、更新后的修改时间与并发戳（以及存储里的创建时间与租户）、软删除后的删除标记与删除时间。同一工作单元里，写入对随后的查找与查询可见（读己之写），提交前对其他工作单元不可见，回滚后存储里不留痕迹。

更新时**修改查找得到的实例再写回**。也可以传一个自己构造的游离实例：三个 Provider 都按它写入业务字段；实现 `IHasConcurrencyStamp` 的实体会先比对并发戳，戳不同即冲突。协议字段只由 Provider 写：创建时间、租户与删除标记沿用存储里当前行的值，调用方实例上的这些值（游离实例带的，或读出后改的）一律不生效，更新不能把实体改到别的租户或改掉创建时间。已软删除的实体对仓储不可见，再更新或删除都返回 `PERSISTENCE_NOTFOUND_001`。下例的错误码与消息键 `SampleMessages.ResourceMissing` 是 SimpleApp 自己定义的。

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

public sealed record RenameOrderRequest(Guid Id, string Name);

[Operation("orders.rename", Permission = "orders.write")]
public sealed class RenameOrder(IRepository<Order, Guid> orders) : IOperation<RenameOrderRequest, OrderResponse>
{
    public async ValueTask<Result<OrderResponse>> HandleAsync(RenameOrderRequest request, InvocationContext context)
    {
        // 不存在、已软删除、属于其他租户都是同一种结果
        var found = await orders.FindAsync(request.Id, context.CancellationToken);
        if (!found.TryGetValue(out var order))
        {
            return Error.NotFound("SAMPLES_NOTFOUND_001", SampleMessages.ResourceMissing);
        }

        order.Name = request.Name;
        var updated = await orders.UpdateAsync(order, context.CancellationToken);
        // 失败直接作为操作结果返回，Transaction 阶段据此回滚
        return updated.IsFailure ? updated.Error! : new OrderResponse(order.Id, order.Name, order.Amount, order.CreatedAt);
    }
}
```

操作不调用提交：处理器返回成功，Transaction 阶段提交；返回失败或抛出异常，整个工作单元回滚。

### 并发新增同一标识

两个调用同时新增同一标识，检查时对方都还没提交，各自的重复判定都会通过。先提交的保留，后到的以 `PERSISTENCE_CONFLICT_002` 失败，不会静默覆盖：

| Provider | 后到者在哪里失败 |
| --- | --- |
| 内存 | 提交时：提交按每个写过的标识首次读到的状态校验，发现标识已被提交 |
| EF Core | 写入时：插入撞上主键约束（PostgreSQL 等要等对方提交后才报错），仓储在同一事务里核对标识确已存在，返回标识重复 |
| SqlSugar | 写入时：同上；PostgreSQL 语句失败后事务已中止，仓储另开连接核对 |

数据库报错后核对不到这个标识，再核对实体的唯一索引（见下文[索引](#索引)），撞上返回 `PERSISTENCE_CONFLICT_003`；两者都不是（外键冲突等），异常照常抛出，调用以 `KERNEL_UNEXPECTED_001` 结束。关系型 Provider 返回标识重复或唯一键重复之后，这个工作单元只能回滚：处理器忽略这次失败、返回成功，提交也会回滚并返回同一错误。

## 批量方法

对多个实体的读写用批量方法，不在循环里调用单条方法（ADR-0012《批量优先》）。单条是批量的特例，两者语义相同：

- `AddRangeAsync`、`UpdateRangeAsync`、`RemoveRangeAsync` 返回与入参等长、顺序一一对应的 `IReadOnlyList<Result>`。标识重复、缺租户、不存在、并发冲突按项报告。
- 通过校验的项一次写入：内存实现一次进覆盖层；EF Core 用一次 `IN` 查询取回目标行、逐项校验后一次 `SaveChanges`；SqlSugar 用一次主键 `IN` 查询取回目标行，再一次多行插入、更新或删除。写入阶段的失败对本次写入的全部项统一报告，包括写入时才撞上的标识重复（见下文「并发新增同一标识」），这时 `id` 是第一个撞上的标识。
- 批内重复项按入参顺序逐项施加：同一标识新增两次，第二次返回 `PERSISTENCE_CONFLICT_002`；同一实体删除两次，第二次返回 `PERSISTENCE_NOTFOUND_001`；实现 `IHasConcurrencyStamp` 的实体，同一标识的游离实例更新两次，第二次因并发戳已刷新返回 `PERSISTENCE_CONFLICT_001`。
- 空集合不访问数据库、返回空列表，但仍要求活动的工作单元。
- 进入数据库之前检查取消；写入是一步，不在中途取消留下半批。

逐项失败不会让调用自动失败：通过校验的项已经写进本次事务，处理器返回成功就会提交。要求全有或全无时，遇到失败项就把它作为操作结果返回，整批随之回滚：

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

public sealed record ArchiveOrdersRequest(IReadOnlyList<Guid> Ids);

public sealed record ArchiveOrdersResponse(int Archived);

[Operation("orders.archive", Permission = "orders.write")]
public sealed class ArchiveOrders(IRepository<Order, Guid> orders) : IOperation<ArchiveOrdersRequest, ArchiveOrdersResponse>
{
    public async ValueTask<Result<ArchiveOrdersResponse>> HandleAsync(ArchiveOrdersRequest request, InvocationContext context)
    {
        // 一次往返取回可见的订单；Order 实现 ISoftDeletable，删除改为打标
        var found = await orders.FindManyAsync(request.Ids, context.CancellationToken);
        var results = await orders.RemoveRangeAsync(found, context.CancellationToken);
        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                // 返回失败让整批回滚，已通过校验的项也不落库
                return result.Error!;
            }
        }

        return new ArchiveOrdersResponse(found.Count);
    }
}
```

仓储不提供按查询条件的批量更新或删除（`UPDATE … WHERE`）：它绕过审计字段、并发戳与软删除协议。确实需要时，应用直接用 Provider 的客户端，并自己维护这些字段。

## 软删除与租户过滤

两项过滤都由仓储显式施加在每个查询上（EF Core 用 `EF.Property`，SqlSugar 用表达式），不用 ORM 的全局查询过滤器，`FindAsync`、`FindManyAsync` 与 `ListAsync` 的行为一致。

- 实现 `ISoftDeletable` 的实体：删除只打标；查询默认排除，`IncludeDeleted = true` 时包含。已软删除的实体不能经仓储恢复，需要恢复时用 Provider 的客户端。
- 实现 `IMultiTenant` 的实体：当前租户是工作单元的 `TenantId`，管道里取自 Tenant 阶段写入的安全上下文。新增时写入当前租户；查询只返回同一租户的行；没有当前租户的调用看不到任何多租户实体，新增返回 `PERSISTENCE_VALIDATION_001`。
- 新增时的重复判定不看可见性：已软删除或属于其他租户的同键行同样与新标识冲突。
- 租户只在新增时写入：更新与删除沿用存储的租户，不能借更新把实体搬到别的租户。

租户如何解析见[多租户](./multi-tenancy)，按租户分库时连接怎么选见[多租户的租户分片](./multi-tenancy#租户分片)。SqlSugar 还支持在一次调用里让平台表落默认库、租户表落分片库，见 [Persistence.SqlSugar 包](../packages/persistence-sqlsugar#多库落点)。

## 列与建表约定

字符串列的长度用 DataAnnotations 的 `[StringLength(n)]` 声明，不限长度的文本列用 `Persistence.Abstractions` 的 `[LongText]`：

```csharp
using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace SimpleApp.Orders;

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

| 约定 | EF Core | SqlSugar |
| --- | --- | --- |
| `[StringLength(n)]` | EF Core 原生识别为长度 n | 约定映射为长度 n |
| `[LongText]` | 没有上限的字符串本来就建长文本，不需要额外映射 | 映射为各数据库的长文本类型 |
| 没有注解的字符串 | EF Core 缺省（长文本） | SqlSugar 缺省（PostgreSQL 为 `varchar(255)`，超长写入整批失败） |
| `ConcurrencyStamp` | `ApplyPersistenceConventions(Database)` 设为并发令牌、长度 32 | 长度 32 |
| `TenantId`、`IsDeleted` | `ApplyPersistenceConventions(Database)` 建索引 | — |
| `[EntityIndex]` | `ApplyPersistenceConventions(Database)` 映射为 `HasIndex`，名字与 SqlSugar 相同；软删除实体的唯一索引加 `HasFilter` | `InitTables` 建出；软删除实体的部分唯一索引由 `SqlSugarConventions.InitTables` 补建 |
| `DateTimeOffset` | EF Core 缺省 | 经转换器按 UTC 时刻往返 |
| 可空类型（`int?`、`string?`） | EF Core 缺省 | 列可空 |

SqlSugar 约定也认 DataAnnotations 的 `[MaxLength(n)]` 与无参 `[MaxLength]`，但它们的构造函数带 `RequiresUnreferencedCode`，Native AOT 项目的分析器拒绝，这类项目只用 `[StringLength(n)]` 与 `[LongText]`。框架自己的数据库记录全部显式声明字符串长度，架构测试门禁检查这一点（ADR-0036），应用实体建议同样声明。

### 索引

索引同样声明在实体上，用 `Persistence.Abstractions` 的 `[EntityIndex]`，不标 SqlSugar 的 `[SugarIndex]` 或 EF Core 的 `[Index]`，换 Provider 不改实体（ADR-0072）：

```csharp
[EntityIndex(nameof(TenantId), nameof(Title), IsUnique = true)]   // UX_OrderNote_TeId_Ti
[EntityIndex(nameof(CreatedAt))]                                   // IX_OrderNote_CrAt
public sealed class OrderNote : IEntity<Guid>, IMultiTenant, IHasCreationTime, IHasConcurrencyStamp { … }
```

- 列按声明顺序、全部升序，一个实体可以声明多个；`IsUnique = true` 是唯一索引，业务上唯一的键（租户 + 名字之类）这样声明。
- 名字由规则推出，不手写：`IX_`（唯一 `UX_`）+ 表名 + 每列一段缩写（属性名每个词取前两个字符，`TenantId → TeId`），全名不超过 63 个字符，超过即报错。两个 Provider 建出同名的索引。
- 软删除实体（`ISoftDeletable`）的唯一索引只约束未删除的行（部分唯一索引：列就是声明的列，条件 `IsDeleted = false`，ADR-0081）——同一业务键删除、重建、再删除任意多次都不冲突，未删除的行之间唯一。只有 PostgreSQL、SQLite、SQL Server 支持部分唯一索引，其余数据库（MySQL、Oracle 等）上有这样的声明即启动失败（`PERSISTENCE_UNAVAILABLE_008`），不退化成普通唯一索引；在那些数据库上要唯一约束，就把 `IsDeleted` 显式写进列（普通唯一索引，同一业务键最多一行已删除），或不声明唯一。SqlSugar 的部分唯一索引由 `SqlSugarConventions.InitTables` 在建表之后补建（应用自己给租户库建表时也要经它），EF Core 由 `ApplyPersistenceConventions(Database)` 加 `HasFilter`。
- 写入撞上唯一索引（并发写入同一业务键、同一批两项同键、把键改成已有的值）返回 `PERSISTENCE_CONFLICT_003`，这次写入的项统一报告，调用随后回滚；重试时先查到已有的行，按更新处理。内存实现不建索引、不校验唯一性。
- 唯一索引里有可空列时，PostgreSQL、SQLite、MySQL 不约束含 NULL 的行；要让"全局"这类空值参与唯一性，存成非空的哨兵值。
- 分表实体（SqlSugar `[SplitTable]`）不用 `[EntityIndex]`，直接用 `[SugarIndex]` 与 `{split_table}` 占位符。

表结构的创建：

| Provider | 配置键 | 行为 |
| --- | --- | --- |
| 内存 | — | 不需要 |
| EF Core | `XiHan:Persistence:EntityFrameworkCore:EnsureCreated` | 为 `true` 时启动即 `EnsureCreated`，只作用于默认连接；只用于开发与测试，生产用 EF Core 迁移 |
| SqlSugar | `XiHan:Persistence:SqlSugar:InitTables` | 为 `true` 时启动按已登记的实体建缺表、加缺列（经 `SqlSugarConventions.InitTables`，软删除实体的部分唯一索引随之建出）；删列、改名、改类型不在范围内；只宜在开发与测试开启 |

## 连接

EF Core 与 SqlSugar Provider 声明一个可选的连接绑定 `db`（类型 `connection:relational`），宿主从配置节 `XiHan:Bindings:db` 解析它，交给特性的配置回调：

```json
{
  "XiHan": {
    "Bindings": {
      "db": "Data Source=app.db"
    }
  }
}
```

回调的第一个参数是 `Maybe<BindingValue>`：绑定缺失时由回调自己决定连接（`binding.ValueOrThrow()` 在缺失时抛出）。装配了 `multitenancy.sharding@1` 时，带分片的调用把租户的连接作为绑定交给同一个回调。

### 命名连接

一部分平台数据要放在另一个库里、并在业务事务进行中另行提交时（RFC-0005「命名连接」，ADR-0084），用两样东西：

- 实体登记到命名连接：`services.AddRepository<TEntity, TKey>(connection: "leases")`。它在那个连接上读写、建表；普通工作单元里照常参与整体事务。同一实体登记到两个不同的连接抛 `ArgumentException`。
- 绑定连接的工作单元：管道外开 `new UnitOfWorkOptions(Connection: "leases")`，工作单元只用这个连接，事务只开在它上面，不牵动默认连接；操作落在别处的实体抛 `PERSISTENCE_VALIDATION_004`。

SQLite 只有一个写者：业务事务写过之后，同一个库文件上的另一个事务要等它结束。工作流的租约表就是这种数据，SQLite 上要配 `XiHan:Workflow:Database:LockConnection` 把它放到另一个库文件，见 [Workflow.Database](../packages/workflow-database#sqlite-上的租约表)。只有 SqlSugar 支持命名连接（方式一在 `XiHan:Persistence:SqlSugar:Connections:<name>` 声明，见 [SqlSugar 的命名连接](../packages/persistence-sqlsugar#命名连接)）；EF Core 一个 `DbContext` 一个连接、内存实现没有连接，登记到命名连接在装配时、绑定连接的工作单元在开始时以 `PERSISTENCE_UNAVAILABLE_008` 失败。

## 运行时数据源

应用在运行时按自己的记录（名字、数据库类型、连接串，通常存在应用自己的库里、口令加密保存）连外部库，比如代码生成要导入的外部数据源、零代码运行时要查的外部表。这些连接由单例 `DataSourceRegistry` 管，与上面的约定连接 `default` / `shard`、租户分库互不相干：不进作用域客户端、不参与实体落点、不进工作单元。

```csharp
public sealed class CodeGenDataSources(DataSourceRegistry registry)
{
    // 记录里读出的定义：名字用记录的标识，连接串里放解密后的口令
    public ValueTask<Result> TestAsync(DataSourceDefinition definition, CancellationToken ct)
        => registry.TestAsync(definition, ct);                      // 打开一次连接再关闭，不登记

    public async Task<Result<ISqlSugarClient>> ClientAsync(DataSourceDefinition definition, CancellationToken ct)
        => (await registry.GetOrAddAsync(definition, ct)).Map(static s => s.GetSqlSugarClient());

    // 记录改了、停用或删了：显式失效，旧连接串不再被本进程使用
    public ValueTask<IReadOnlyList<bool>> InvalidateAsync(IReadOnlyList<string> names, CancellationToken ct)
        => registry.RemoveAsync(names, ct);
}
```

| 项 | 规则 |
| --- | --- |
| 取用 | `GetOrAddAsync(definition)`：同名同定义复用；定义变了建新实例替换旧的并释放旧的。建实例不连库，连不上的库照样登记，使用时报告 |
| 失效 | 显式 `RemoveAsync(names)`（批量，逐项返回是否登记过）加定义比对，不用配置变化令牌。注册表是进程内的：每次使用都以记录里当前的定义调 `GetOrAddAsync`，别的实例改了记录，本实例下一次取用即替换；只按名字 `GetAsync` 看不到别的实例上的修改 |
| 容量 | `XiHan:Persistence:DataSources:MaxCount`（缺省 64），满了再登记新名字返回 `PERSISTENCE_UNAVAILABLE_007`，不淘汰已有的 |
| 测试连接 | `TestAsync(definition)`：建临时实例、打开一次连接再关闭、释放；不登记、不占名额。等多久由取消令牌与连接串里驱动的连接超时决定 |
| 健康 | 每个数据源一条 `persistence.datasource.<name>`，后台按间隔打开连接再关闭；连不上 Degraded，不影响 readiness |
| 机密 | 连接串不进日志、错误与健康详情：错误原因是驱动异常的消息链，整串连接串与口令类键（`password`、`pwd`、`secret`、`token`、`key`）的值换成 `***`；主机、端口、库名、用户名保留，排查要用 |
| Provider | SqlSugar 支持（数据库类型是 SqlSugar `DbType` 的名字，`DatabaseTypes` 列出常用的；客户端是每个数据源一个的 `SqlSugarScope`，不施加实体约定、PostgreSQL 不自动转小写）。EF Core 与内存实现不支持，取用与测试连接返回 `PERSISTENCE_UNAVAILABLE_008` |

## 库结构读取与实体目录

代码生成要列出库里的表、读出列结构（导入与同步结构），还要认出哪些表由已登记的实体建出（沿用已有实体）。`ISchemaReader` 与 `IEntityCatalog` 提供这两样：

```csharp
public sealed class CodeGenImporter(ISchemaReader schema, IEntityCatalog catalog)
{
    // dataSource 为 null 读应用的缺省连接，否则读运行时数据源（先按定义登记再读）
    public Task<Result<TablePage>> ListAsync(DataSourceDefinition? dataSource, string? keyword, int skip, int take, CancellationToken ct)
        => schema.ListTablesAsync(dataSource, new TableListQuery { Keyword = keyword, Skip = skip, Take = take }, ct);

    public Task<Result<IReadOnlyList<Result<TableSchema>>>> ReadAsync(DataSourceDefinition? dataSource, IReadOnlyList<string> tables, CancellationToken ct)
        => schema.GetTablesAsync(dataSource, tables, ct);           // 一次最多 100 张，逐项结果

    public async Task<EntityMapping?> ExistingEntityAsync(string table, CancellationToken ct)
        => (await catalog.ListAsync(ct)).FirstOrDefault(m => string.Equals(m.Table, table, StringComparison.OrdinalIgnoreCase));
}
```

| 项 | 规则 |
| --- | --- |
| 列表 | 按名字排序；`Keyword` 在名字与注释里不区分大小写地包含匹配；`Skip` / `Take` 分页（`Take` 最多 1000），返回过滤后的总数；库里的表一次读出、在内存里分页 |
| 读结构 | 一次最多 `SchemaLimits.MaxTablesPerRead`（100）张，超过整批 `PERSISTENCE_VALIDATION_005`；逐项结果，找不到的表该项 `PERSISTENCE_NOTFOUND_003`；连接或读取失败整批 `PERSISTENCE_UNAVAILABLE_006`；空集合不连库 |
| 列 | 类型名、序号、主键、自增、可空、长度、精度与小数位、注释、缺省值（数据库报告的原文）；主键列一律不可空 |
| 分表 | 已登记的分表实体按表名模板把 `Order_20261101` 这类物理表折叠成逻辑表 `Order`，`PhysicalTables` 新到旧，列取最新的物理表 |
| 实体目录 | 每个登记的实体一条：表名（映射声明的写法，分表是逻辑名）、列映射、`EntityProtocols`（聚合根、创建 / 修改时间、并发戳、软删除、多租户）、`[EntityIndex]` 推出的索引；PostgreSQL 的物理名可能是小写，对照时不区分大小写 |
| 缺省连接 | 另开一个不在事务里的连接读，PostgreSQL 系语句失败不会中止调用的事务 |
| SQLite 的限制 | 没有表与列注释；`INTEGER PRIMARY KEY` 报告可空（按主键改为不可空）；长度与精度取自声明的类型；自增只认 `AUTOINCREMENT` |
| Provider | SqlSugar 两者都支持。EF Core 的实体目录取 DbContext 模型、读结构不支持；内存实现的实体目录表名取类型名、读结构不支持；不支持的返回 `PERSISTENCE_UNAVAILABLE_008` |

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Persistence:InMemory:MaxEntitiesPerType` | 正整数 | `100000` | 内存实现每种实体的容量上限，超出时提交返回 `PERSISTENCE_UNAVAILABLE_002` |
| `XiHan:Persistence:EntityFrameworkCore:EnsureCreated` | 布尔 | `false` | 启动时建表 |
| `XiHan:Persistence:EntityFrameworkCore:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项重探间隔，也是单次探测的超时 |
| `XiHan:Persistence:SqlSugar:InitTables` | 布尔 | `false` | 启动时建表与加列 |
| `XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项重探间隔，也是单次探测的超时 |
| `XiHan:Persistence:DataSources:MaxCount` | 1–1024 | `64` | 运行时数据源的登记数上限 |
| `XiHan:Persistence:DataSources:HealthProbeIntervalSeconds` | 正数 | `15` | 运行时数据源健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Bindings:db` | 字符串 | — | 连接绑定的主值，见上文 |

非法值在启动配置阶段抛出 `ArgumentException`，不回退到缺省值。雪花标识的配置节 `XiHan:Persistence:Snowflake` 见[标识生成](./distributed-ids#配置)。

## 失败与错误码

| 错误码 | 何时出现 | 形式 |
| --- | --- | --- |
| `PERSISTENCE_VALIDATION_001` | 多租户实体在没有当前租户的调用里新增 | `Result` |
| `PERSISTENCE_VALIDATION_002` | 工作单元状态不允许（重复开始、未开始就提交）；同一服务作用域里再经管道的嵌套调用 | 状态错误抛异常；嵌套调用是调用结果 |
| `PERSISTENCE_VALIDATION_003` | 聚合有未派发的领域事件，但没有注册派发器 | 调用结果 |
| `PERSISTENCE_VALIDATION_004` | 实体落点不成立（装配错误）：SqlSugar 实体的 `[Tenant]` 或登记的命名连接不在客户端里、两者不一致；工作单元绑定的连接不存在，或绑定连接的工作单元里操作的实体不落在它上面 | 启动失败；运行时抛 `FrameworkException` |
| `PERSISTENCE_VALIDATION_005` | 查询或表列表的 `Take` 超过 `EntityQuery.DefaultMaxTake`（1000），参数 `take`、`max`；一次读结构超过 100 张表，参数 `count`、`max` | 构造查询时抛 `FrameworkException`；调用里由 Transaction 阶段转为校验结果（HTTP 400） |
| `PERSISTENCE_VALIDATION_006` | 运行时数据源的数据库类型 Provider 不认识、连接串格式错 | `Result` |
| `PERSISTENCE_NOTFOUND_001` | 更新或删除的实体不存在（含已软删除、不属于当前租户） | `Result` |
| `PERSISTENCE_NOTFOUND_002` | 运行时数据源没有在本进程登记（`GetAsync`） | `Result` |
| `PERSISTENCE_NOTFOUND_003` | 要读结构的表不在库里 | 逐项 `Result` |
| `PERSISTENCE_CONFLICT_001` | 乐观并发冲突：并发戳不匹配；内存实现提交时发现更新或删除的行已被别人改过或删掉 | `Result`；后者是提交结果 |
| `PERSISTENCE_CONFLICT_002` | 新增的标识已存在；含并发新增同一标识 | `Result`；内存实现的并发新增是提交结果 |
| `PERSISTENCE_UNEXPECTED_001` | 只读工作单元里新增、更新、删除（只读操作里的写入，是操作实现的缺陷，HTTP 500） | 仓储写入返回 `Result` 且提交返回同一错误；`EnsureTransactionAsync` 抛异常、Transaction 阶段转为结果 |
| `PERSISTENCE_CONFLICT_003` | 写入撞上实体声明的唯一索引（关系型 Provider） | `Result`，工作单元随后只能回滚 |
| `PERSISTENCE_UNAVAILABLE_001` | 仓储在 Transaction 阶段之外使用 | `FrameworkException` |
| `PERSISTENCE_UNAVAILABLE_002` | 内存实现的表已满 | 提交结果、健康项 |
| `PERSISTENCE_UNAVAILABLE_003` | 数据库不可达 | 健康项 |
| `PERSISTENCE_UNAVAILABLE_006` | 数据源连不上或读取失败（测试连接、健康探测、库结构读取），参数 `name`、`reason`（已脱敏） | `Result`、健康项 |
| `PERSISTENCE_UNAVAILABLE_007` | 运行时数据源登记已满 | `Result` |
| `PERSISTENCE_UNAVAILABLE_008` | 当前的持久化 Provider 不支持该能力（EF Core 与内存实现的运行时数据源、库结构读取与命名连接） | `Result`；命名连接在装配时或 `BeginAsync` 抛 `FrameworkException` |

`PERSISTENCE_UNAVAILABLE_004` 与 `005` 属于雪花标识生成器，见[标识生成](./distributed-ids#失败与错误码)。错误怎样经协议适配器变成 HTTP 状态与 Problem Details，见[结果与错误](./errors)。

每个 Provider 还登记一个健康项：内存实现 `persistence.inmemory.capacity`（任一张表满即 Degraded），EF Core `persistence.entityframeworkcore.database` 与 SqlSugar `persistence.sqlsugar.database`（启动时探一次，之后按间隔在后台重探，连不上即 Degraded，应用仍在服务）。健康项的汇总见[生命周期与健康](./lifecycle)。

## 与其他能力的关系

- 事务的开启、提交与领域事件派发：[工作单元与事务](./uow)。
- 实体协议与规约：[领域模型](./domain)。
- 当前租户与按租户分库：[多租户](./multi-tenancy)。
- 发件箱用仓储与工作单元把消息与业务写入放进同一事务：[消息与事件](./messaging)。
- 自己实现 Provider 时要通过的用例：[测试与一致性套件](./testing)。

## 相关

- [Persistence.Abstractions 包](../packages/persistence-abstractions)
- [Persistence.InMemory 包](../packages/persistence-inmemory)
- [Persistence.EntityFrameworkCore 包](../packages/persistence-entityframeworkcore)
- [Persistence.SqlSugar 包](../packages/persistence-sqlsugar)
- [Persistence.Generators 包](../packages/persistence-generators)
- 规范：RFC-0005《领域协议与持久化》、`conventions/batch-first.md`《批量优先》
- 决策：ADR-0005、ADR-0012、ADR-0013、ADR-0023、ADR-0036、ADR-0078
