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
内存实现每种实体一张有界的表（缺省 100000 条），存的是对象引用，不检测跨工作单元的并发冲突，一次提交跨多张表时也不是原子的。生产环境装配 EF Core 或 SqlSugar Provider。
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
| `Page(skip, take)` | `Skip` 0、`Take` 1000 | `skip` 小于 0 或 `take` 小于 1 抛出 `ArgumentOutOfRangeException`；缺省 `Take` 是常量 `EntityQuery.DefaultMaxTake` |
| `IncludeDeleted` | `false` | 为 `true` 时包含已软删除的实体 |
| `Within(from, to)` | 不限定 | 时间范围提示，两端都含；只影响按时间分表的 Provider 扫描哪些表，不过滤行 |

下面的操作按金额过滤、按创建时间倒序分页，并返回总数。`OrderResponse` 来自 SimpleApp；新的请求与响应类型要登记进应用的 `JsonSerializerContext`（SimpleApp 的 `SampleJsonContext`），否则 `Kernel.Generators` 报 `XHG002`。

```csharp
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

public sealed record SearchOrdersRequest(int MinAmount, int Skip, int Take);

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
| `UpdateAsync(entity, ct)` | 修改时间、新的并发戳 | `PERSISTENCE_NOTFOUND_001` 不存在或不可见；`PERSISTENCE_CONFLICT_001` 游离实例的并发戳与当前值不同 |
| `RemoveAsync(entity, ct)` | 软删除实体写删除标记与删除时间；其余物理删除 | `PERSISTENCE_NOTFOUND_001` |

写入成功后，调用方传入的实例上就是协议字段的新值：新增后的创建时间与并发戳、更新后的修改时间与并发戳、软删除后的删除标记与删除时间。同一工作单元里，写入对随后的查找与查询可见（读己之写），提交前对其他工作单元不可见。

更新时**修改查找得到的实例再写回**。传入一个自己构造的游离实例时，三个 Provider 都按它整行写入，`CreatedAt`、`TenantId` 等字段也会被它的值覆盖；实现 `IHasConcurrencyStamp` 的实体会先比对并发戳，戳不同即冲突。已软删除的实体对仓储不可见，再更新或删除都返回 `PERSISTENCE_NOTFOUND_001`。下例的错误码与消息键 `SampleMessages.ResourceMissing` 是 SimpleApp 自己定义的。

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

## 批量方法

对多个实体的读写用批量方法，不在循环里调用单条方法（ADR-0012《批量优先》）。单条是批量的特例，两者语义相同：

- `AddRangeAsync`、`UpdateRangeAsync`、`RemoveRangeAsync` 返回与入参等长、顺序一一对应的 `IReadOnlyList<Result>`。标识重复、缺租户、不存在、并发冲突按项报告。
- 通过校验的项一次写入：内存实现一次进覆盖层；EF Core 用一次 `IN` 查询取回目标行、逐项校验后一次 `SaveChanges`；SqlSugar 用一次主键 `IN` 查询取回目标行，再一次多行插入、更新或删除。写入阶段的失败对本次写入的全部项统一报告。
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
| `ConcurrencyStamp` | `ApplyPersistenceConventions()` 设为并发令牌、长度 32 | 长度 32 |
| `TenantId`、`IsDeleted` | `ApplyPersistenceConventions()` 建索引 | — |
| `DateTimeOffset` | EF Core 缺省 | 经转换器按 UTC 时刻往返 |
| 可空类型（`int?`、`string?`） | EF Core 缺省 | 列可空 |

SqlSugar 约定也认 DataAnnotations 的 `[MaxLength(n)]` 与无参 `[MaxLength]`，但它们的构造函数带 `RequiresUnreferencedCode`，Native AOT 项目的分析器拒绝，这类项目只用 `[StringLength(n)]` 与 `[LongText]`。框架自己的数据库记录全部显式声明字符串长度，架构测试门禁检查这一点（ADR-0036），应用实体建议同样声明。

表结构的创建：

| Provider | 配置键 | 行为 |
| --- | --- | --- |
| 内存 | — | 不需要 |
| EF Core | `XiHan:Persistence:EntityFrameworkCore:EnsureCreated` | 为 `true` 时启动即 `EnsureCreated`，只作用于默认连接；只用于开发与测试，生产用 EF Core 迁移 |
| SqlSugar | `XiHan:Persistence:SqlSugar:InitTables` | 为 `true` 时启动按已登记的实体建缺表、加缺列；删列、改名、改类型不在范围内；只宜在开发与测试开启 |

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

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Persistence:InMemory:MaxEntitiesPerType` | 正整数 | `100000` | 内存实现每种实体的容量上限，超出时提交返回 `PERSISTENCE_UNAVAILABLE_002` |
| `XiHan:Persistence:EntityFrameworkCore:EnsureCreated` | 布尔 | `false` | 启动时建表 |
| `XiHan:Persistence:EntityFrameworkCore:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项重探间隔，也是单次探测的超时 |
| `XiHan:Persistence:SqlSugar:InitTables` | 布尔 | `false` | 启动时建表与加列 |
| `XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项重探间隔，也是单次探测的超时 |
| `XiHan:Bindings:db` | 字符串 | — | 连接绑定的主值，见上文 |

非法值在启动配置阶段抛出 `ArgumentException`，不回退到缺省值。雪花标识的配置节 `XiHan:Persistence:Snowflake` 见[标识生成](./distributed-ids#配置)。

## 失败与错误码

| 错误码 | 何时出现 | 形式 |
| --- | --- | --- |
| `PERSISTENCE_VALIDATION_001` | 多租户实体在没有当前租户的调用里新增 | `Result` |
| `PERSISTENCE_VALIDATION_002` | 工作单元状态不允许（重复开始、未开始就提交）、只读工作单元写入 | 状态错误抛异常；只读写入返回 `Result` |
| `PERSISTENCE_VALIDATION_003` | 聚合有未派发的领域事件，但没有注册派发器 | 调用结果 |
| `PERSISTENCE_NOTFOUND_001` | 更新或删除的实体不存在（含已软删除、不属于当前租户） | `Result` |
| `PERSISTENCE_CONFLICT_001` | 乐观并发冲突：并发戳不匹配 | `Result` |
| `PERSISTENCE_CONFLICT_002` | 新增的标识已存在 | `Result` |
| `PERSISTENCE_UNAVAILABLE_001` | 仓储在 Transaction 阶段之外使用 | `FrameworkException` |
| `PERSISTENCE_UNAVAILABLE_002` | 内存实现的表已满 | 提交结果、健康项 |
| `PERSISTENCE_UNAVAILABLE_003` | 数据库不可达；SqlSugar 实体的 `[Tenant]` 指向客户端里不存在的连接 | 健康项；后者抛 `FrameworkException` |

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
- 决策：ADR-0005、ADR-0012、ADR-0013、ADR-0023、ADR-0036
