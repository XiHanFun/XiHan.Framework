# 9.3 仓储读写

这一页讲 `IRepository<TEntity, TKey>` 的每个方法：怎样查找与查询、怎样分页、新增更新删除各写哪些字段、批量方法的逐项结果与部分失败语义，以及软删除与租户过滤是怎样施加的。读完它，你能在操作契约里完成一个实体的全部读写，并正确处理并发冲突与批量失败。前置知识是[持久化与 Provider](/persistence/overview)，操作契约的写法见[操作契约](/web/operations)。

## 为什么需要

仓储要解决的不只是「把对象存进去再取出来」。真实业务里每一次读写都伴随一组容易漏掉的规则：查询要排除已删除与其他租户的行，更新要比对并发戳，删除要改成打标，批量导入要知道哪几条失败了，分页不能一次拉回全表。框架把这些规则做进仓储的每个方法：同一套语义在三个 Provider 上一致，可恢复的失败以 `Result` 返回，操作里只写业务判断。

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

要看到已软删除的实体，或给分表实体限定扫描范围：

```csharp
var withDeleted = EntityQuery.All<Order>() with { IncludeDeleted = true };
var lastMonth = EntityQuery.All<Order>().Within(DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow);
```

`Within` 不是过滤条件：不分表的实体与不支持范围的 Provider 忽略它，范围之外的行照常返回，要按时间过滤另写规约。目前只有 SqlSugar Provider 的分表实体使用它，见 [SqlSugar Provider](/persistence/sqlsugar)。

## 最小示例

下面的操作按金额过滤、按创建时间倒序分页，并返回总数。分页参数来自请求时用 `[Range]` 约束：超出范围由 Validate 阶段返回 `KERNEL_VALIDATION_003` 并指出字段，进不到处理器。没有约束时，超过上限的 `take` 在构造查询时被拒绝，调用以 `PERSISTENCE_VALIDATION_005`（HTTP 400，参数 `take`、`max`）结束、已做的写入回滚；负的 `skip` 与小于 1 的 `take` 则是 `ArgumentOutOfRangeException`，成为 `KERNEL_UNEXPECTED_001`，所以两头都要约束。`OrderResponse` 来自 SimpleApp；新的请求与响应类型要登记进应用的 `JsonSerializerContext`，否则 `Kernel.Generators` 报 `XHG002`。

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

运行后 `orders.search` 在 HTTP 上是 `POST /orders:search`（自定义动词，不是 `get` / `list`，所以开可写工作单元；在 `[Operation]` 上加 `ReadOnly = true` 就开只读工作单元，路由也随之变成 `GET /orders:search`，见[工作单元与事务](/persistence/unit-of-work)与 [HTTP 路由与载荷](/web/routing)）。响应里的 `Total` 是当前租户、未删除、金额达标的订单总数，`Items` 是其中一页。

## 写入

| 方法 | 写入的字段 | 可恢复失败 |
| --- | --- | --- |
| `AddAsync(entity, ct)` | 创建时间、当前租户、新的并发戳 | `PERSISTENCE_CONFLICT_002` 标识已存在（含已软删除与其他租户的行）；`PERSISTENCE_VALIDATION_001` 多租户实体没有当前租户 |
| `UpdateAsync(entity, ct)` | 修改时间、新的并发戳；创建时间、租户与删除标记沿用存储值 | `PERSISTENCE_NOTFOUND_001` 不存在或不可见；`PERSISTENCE_CONFLICT_001` 游离实例的并发戳与当前值不同 |
| `RemoveAsync(entity, ct)` | 软删除实体在存储的当前行上写删除标记与删除时间；其余物理删除 | `PERSISTENCE_NOTFOUND_001` |

写入成功后，调用方传入的实例上就是协议字段的值：新增后的创建时间与并发戳、更新后的修改时间与并发戳（以及存储里的创建时间与租户）、软删除后的删除标记与删除时间。同一工作单元里，写入对随后的查找与查询可见（读己之写），提交前对其他工作单元不可见，回滚后存储里不留痕迹。

更新时**修改查找得到的实例再写回**。也可以传一个自己构造的游离实例：三个 Provider 都按它写入业务字段；实现 `IHasConcurrencyStamp` 的实体会先比对并发戳，戳不同即冲突。协议字段只由 Provider 写：创建时间、租户与删除标记沿用存储里当前行的值，调用方实例上的这些值一律不生效，更新不能把实体改到别的租户或改掉创建时间。已软删除的实体对仓储不可见，再更新或删除都返回 `PERSISTENCE_NOTFOUND_001`。

下例的错误码与消息键 `SampleMessages.ResourceMissing` 是 SimpleApp 自己定义的，文案怎样走消息键见[全球化和本地化](/localization/)。

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

### 并发戳

实现 `IHasConcurrencyStamp` 的实体在新增与更新时拿到新的 32 位十六进制戳。更新游离实例（没有先 `FindAsync`，直接 `new` 一个带 `Id` 的实例）时，仓储先比对实例上的戳与存储里的戳，不同即返回 `PERSISTENCE_CONFLICT_001`。典型用法是把戳随表单带到前端、提交时带回来：两个人同时编辑同一条记录，后提交的那个得到冲突，而不是悄悄覆盖先提交的改动。

### 并发新增同一标识

两个调用同时新增同一标识，检查时对方都还没提交，各自的重复判定都会通过。先提交的保留，后到的以 `PERSISTENCE_CONFLICT_002` 失败，不会静默覆盖：

| Provider | 后到者在哪里失败 |
| --- | --- |
| 内存 | 提交时：提交按每个写过的标识首次读到的状态校验，发现标识已被提交 |
| EF Core | 写入时：插入撞上主键约束（PostgreSQL 等要等对方提交后才报错），仓储在同一事务里核对标识确已存在，返回标识重复 |
| SqlSugar | 写入时：同上；PostgreSQL 语句失败后事务已中止，仓储另开连接核对 |

数据库报错后核对不到这个标识，再核对实体的唯一索引（见[建表约定与索引](/persistence/schema)），撞上返回 `PERSISTENCE_CONFLICT_003`；两者都不是（外键冲突等），异常照常抛出，调用以 `KERNEL_UNEXPECTED_001` 结束。关系型 Provider 返回标识重复或唯一键重复之后，这个工作单元只能回滚：处理器忽略这次失败、返回成功，提交也会回滚并返回同一错误。

## 批量方法

对多个实体的读写用批量方法，不在循环里调用单条方法。单条是批量的特例，两者语义相同：

- `AddRangeAsync`、`UpdateRangeAsync`、`RemoveRangeAsync` 返回与入参等长、顺序一一对应的 `IReadOnlyList<Result>`。标识重复、缺租户、不存在、并发冲突按项报告。
- 通过校验的项一次写入：内存实现一次进覆盖层；EF Core 用一次 `IN` 查询取回目标行、逐项校验后一次 `SaveChanges`；SqlSugar 用一次主键 `IN` 查询取回目标行，再一次多行插入、更新或删除。写入阶段的失败对本次写入的全部项统一报告，包括写入时才撞上的标识重复，这时 `id` 是第一个撞上的标识。
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

接受部分成功的场景（如导入）则遍历结果、收集失败项的标识返回给调用方，通过校验的项随调用提交。

仓储不提供按查询条件的批量更新或删除（`UPDATE … WHERE`）：它绕过审计字段、并发戳与软删除协议。确实需要时，应用直接用 Provider 的客户端，并自己维护这些字段。

## 软删除与租户过滤

两项过滤都由仓储显式施加在每个查询上（EF Core 用 `EF.Property`，SqlSugar 用表达式），不用 ORM 的全局查询过滤器，`FindAsync`、`FindManyAsync` 与 `ListAsync` 的行为一致。

- 实现 `ISoftDeletable` 的实体：删除只打标；查询默认排除，`IncludeDeleted = true` 时包含。已软删除的实体不能经仓储恢复，需要恢复时用 Provider 的客户端。
- 实现 `IMultiTenant` 的实体：当前租户是工作单元的 `TenantId`，管道里取自 Tenant 阶段写入的安全上下文。新增时写入当前租户；查询只返回同一租户的行；没有当前租户的调用看不到任何多租户实体，新增返回 `PERSISTENCE_VALIDATION_001`。
- 新增时的重复判定不看可见性：已软删除或属于其他租户的同键行同样与新标识冲突。
- 租户只在新增时写入：更新与删除沿用存储的租户，不能借更新把实体搬到别的租户。

直接用 `DbContext` 或 `ISqlSugarClient` 查询时没有这两项过滤。`Persistence.Generators` 生成的 `<实体>Filters` 规约让你用同一条件补上，也能在内存里筛一批已取出的实体：

```csharp
using Microsoft.EntityFrameworkCore;
using SimpleApp.Orders;

// 内存里筛一批已经取出的订单
var active = loaded.Where(OrderFilters.ActiveFor("acme").IsSatisfiedBy).ToList();

// 直接查 DbContext 时没有仓储的过滤，用同一条规约补上
var count = await db.Set<Order>().Where(OrderFilters.ActiveFor("acme").Criteria).CountAsync(cancellationToken);
```

`loaded` 是一组 `Order`，`db` 是应用的 `DbContext`。租户如何解析见[租户解析与隔离](/multi-tenancy/resolution)，按租户分库时连接怎么选见[租户分片与分库](/multi-tenancy/sharding)。

## 规约里的字符串匹配

规约里字符串的 `Contains`、`StartsWith`、`EndsWith` 按字面匹配，与 .NET 的语义一致：关键字里的 `%`、`_`、`[`、反斜杠不是通配符或转义符，`o.Name.Contains("t_2")` 不会命中 `budgetx2026`。大小写一个库一套规则，不统一成区分或不区分：内存实现按序数比较、区分大小写；SQLite 上两个关系型 Provider 都译成带转义的 `LIKE`，只对 ASCII 字母不区分；PostgreSQL 与 Oracle 区分；MySQL 与 SQL Server 按列的排序规则。

要跨库一致地不区分大小写，存一列折叠后的值（如 `ToLowerInvariant()`）并对它匹配。要通配时不用这三个方法：SqlSugar 用 `SqlFunc.Like`，EF Core 用 `EF.Functions.Like`，模式由调用方给出、不转义。按字面匹配只在验证过的 SQLite、PostgreSQL、MySQL、SQL Server、Oracle 上；SqlSugar 的其他兼容库（TiDB、OceanBase、openGauss、人大金仓、达梦等）与运行时数据源维持 SqlSugar 原来的翻译，关键字里的 `%`、`_` 仍是通配符。

## 常见问题

### `FindAsync` 找不到，但库里明明有这行

三种可能：这行已软删除（`IsDeleted = true`）；这行属于别的租户；当前调用没有租户而实体是多租户的。前两种对仓储来说都是「不存在」。要看已删除的行用 `IncludeDeleted = true` 的查询；要跨租户看数据用 Provider 的客户端直接查。

### 为什么 `UpdateAsync` 返回 `PERSISTENCE_CONFLICT_001`

传入的实例带着旧的并发戳，存储里的行在这之间被别人改过了。正确的处理是把冲突返给调用方让它重新加载再提交，不要在循环里重试覆盖。读出后立即改再写回的实例，戳是最新的，正常情况下不会冲突。

### 批量方法里一项失败，其他项会写进去吗

会，只要处理器返回成功。批量方法只报告逐项结果，不替你决定事务成败。要全有或全无就把第一个失败项作为操作结果返回；要接受部分成功就收集失败项返回给调用方。

### 为什么分页的 `take` 最多 1000

这是对无界查询的保护：`EntityQuery.DefaultMaxTake` 是上限也是缺省值，超过它不是截断而是拒绝，防止一个漏写分页的查询拉回整张表。要更多行就分页；真正的全表导出用 Provider 的客户端流式读取。

## 相关包与下一步

- 相关包：[Persistence.Abstractions](/packages/persistence-abstractions)、[Persistence.InMemory](/packages/persistence-inmemory)、[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)、[Persistence.SqlSugar](/packages/persistence-sqlsugar)、[Persistence.Generators](/packages/persistence-generators)
- 规范：RFC-0005《领域协议与持久化》、`conventions/batch-first.md`《批量优先》
- 下一页：[工作单元与事务](/persistence/unit-of-work)，讲事务怎样开启、提交与回滚，以及在管道外怎样自己开工作单元
