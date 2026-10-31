# 工作单元与事务

本页讲 `IUnitOfWork` 的生命周期、Transaction 阶段怎样开启、提交与回滚、领域事件在提交前的派发，以及在管道之外（单例服务、后台任务、停机刷写）自己开工作单元的写法，适用于需要控制事务边界的应用开发者。完整 API 见 [Persistence.Abstractions 包](../packages/persistence-abstractions)。

## 概念

工作单元是一次调用的事务边界，作用域生命周期：同一个服务作用域里，仓储取到的是同一个 `IUnitOfWork`。它有四个状态，只能按 `NotStarted → Active → Committed | RolledBack` 推进；重复开始、未开始就提交这类违反顺序的调用抛出携带 `PERSISTENCE_VALIDATION_002` 的 `FrameworkException`。仓储只在 `Active` 状态下工作，否则抛出携带 `PERSISTENCE_UNAVAILABLE_001` 的 `FrameworkException`。

| 成员 | 说明 |
| --- | --- |
| `State` | 当前状态（`UnitOfWorkState`） |
| `TenantId` | 当前租户，无租户为 `null`；多租户实体据此写入与过滤 |
| `IsReadOnly` | 是否只读 |
| `DomainEvents` | 本工作单元写过的聚合（`DomainEventTracker`），按首次写入顺序、同一实例只记一次 |
| `BeginAsync(options, ct)` | 开始，只能调用一次；`UnitOfWorkOptions(TenantId, IsReadOnly)`，`UnitOfWorkOptions.Default` 为无租户、可写 |
| `CommitAsync(ct)` | 提交，返回 `Result`；并发冲突等以失败结果返回 |
| `RollbackAsync(ct)` | 回滚 |

工作单元由持久化 Provider 实现并注册；Provider 的特性同时把 `UnitOfWorkMiddleware` 登记到 Transaction 阶段。

## 管道里的工作单元

经管道的每次调用（HTTP、gRPC、MCP、命令行、消息派发）都经过 Transaction 阶段，`UnitOfWorkMiddleware` 按下面的顺序处理：

1. 从调用的作用域取 `IUnitOfWork`，以 Tenant 阶段写入安全上下文的租户开始它。
2. 调用下一步（Handle 阶段的处理器）。
3. 处理器抛出异常：用 `CancellationToken.None` 回滚，异常原样抛出，由内核转为 Unexpected 结果。
4. 调用结果是失败：回滚，保留原失败结果。
5. 有聚合带着未派发的领域事件：没有注册 `IDomainEventDispatcher` 时以 `PERSISTENCE_VALIDATION_003` 结束并回滚；派发器返回失败时回滚，调用以该失败结束；派发成功清空登记，进入提交。
6. 提交；提交失败时用它覆盖调用结果，调用方看到真实的持久化错误。

所以操作代码不调用提交，只决定成败：返回 `Result` 失败或抛出异常，本次调用的全部写入都回滚，包括批量方法里已通过校验的项。

数据库事务是延迟开启的：EF Core 与 SqlSugar Provider 在第一次写入时才开事务，只读过数据的调用不开事务，提交时也无事可做。这让多个只读调用与一个写入调用能在 SQLite 这类单写者数据库上并存。

中间件开启的工作单元总是可写的。操作契约上的 `ReadOnly`（以及 `get`、`list` 动词）只影响协议推导（HTTP 方法、MCP 的提示），不会让工作单元只读；只读工作单元只能在管道外经 `UnitOfWorkOptions` 显式开启。

## 提交与失败

| 情况 | 工作单元 | 调用结果 |
| --- | --- | --- |
| 处理器成功，没有待派发事件 | 提交 | 处理器的结果 |
| 处理器返回失败 | 回滚 | 原失败 |
| 处理器抛出异常 | 回滚 | Unexpected |
| 有待派发事件但没有派发器 | 回滚 | `PERSISTENCE_VALIDATION_003` |
| 派发器返回失败 | 回滚 | 派发器的失败 |
| 提交时并发冲突（EF Core 的并发令牌在提交时不匹配） | 回滚 | `PERSISTENCE_CONFLICT_001` |
| 提交时内存表超出容量 | 回滚 | `PERSISTENCE_UNAVAILABLE_002` |
| 只读工作单元有未提交的变更 | 回滚 | `PERSISTENCE_VALIDATION_002` |

标识重复在 `AddAsync` / `AddRangeAsync` 时就按项返回 `PERSISTENCE_CONFLICT_002`，不留到提交。

## 各 Provider 的事务

| | 内存 | EF Core | SqlSugar |
| --- | --- | --- | --- |
| 写入去向 | 本工作单元的覆盖层 | 每次写入立即 `SaveChanges`，在数据库事务内 | 每次写入立即执行，在数据库事务内 |
| 事务开启 | 不需要 | 第一次写入时 `BeginTransaction` | 第一次写入时经 `AsTenant()` 在客户端的全部连接上开事务 |
| 读己之写 | 覆盖层叠在已提交的表上 | 同一事务 | 同一事务 |
| 提交 | 逐表原子应用覆盖层 | 保存残余变更后提交事务 | 逐连接顺序提交 |
| 回滚 | 丢弃覆盖层 | 回滚事务并清空变更跟踪器 | 逐连接回滚 |
| 原子性边界 | 单张表；跨表的提交不是原子的 | 一个 `DbContext` 一个连接 | 每个连接各自原子；跨连接是顺序提交，不是两阶段提交 |
| 作用域结束仍未提交 | — | 释放事务即回滚 | 回滚 |

SqlSugar 跨连接的顺序提交意味着：后一个连接提交失败时，前一个已经提交。需要严格原子性的写入放在同一个库，详见 [Persistence.SqlSugar 包](../packages/persistence-sqlsugar#多库落点)。

## 领域事件与发件箱

仓储在新增、更新、删除（单条与批量里通过校验的项）成功后，把实现 `IHasDomainEvents` 的实体登记到 `IUnitOfWork.DomainEvents`。处理器成功后、提交前，中间件取出带有未派发事件的聚合（`DomainEventTracker.Pending()`），连同工作单元的租户交给 `IDomainEventDispatcher.DispatchAsync`。

派发器由 Messaging 子系统实现：`AddDomainEvent<TEvent>(...)` 登记一种事件的主题、CloudEvents 类型、源生成的 `JsonTypeInfo` 与可选的 `subject`，并注册派发器。派发器把事件映射成信封，同一主题一次 `PublishManyAsync`，全部成功后清空各聚合的缓冲。SimpleApp 的 `OrdersFeature` 这样登记 `OrderPlaced`：

```csharp
// 聚合记录的 OrderPlaced 在提交前发到 orders 主题，类型 orders.placed，subject 是订单
context.Services.AddDomainEvent<OrderPlaced>(Topic, "orders.placed", SampleJsonContext.Default.OrderPlaced, static placed => $"order/{placed.OrderId:N}");
```

派发发生在提交之前，事件去向取决于是否装配了发件箱：

| 装配 | 事件的去向 | 与业务写入的关系 |
| --- | --- | --- |
| `OutboxFeature` | 写进发件箱表，与业务写入在同一事务；提交后由投递循环发到代理 | 一起提交、一起回滚 |
| 没有发件箱 | 直接发布到代理 | 发布失败回滚业务写入；发布成功而随后提交失败时，事件已经发出 |

要让事件只随成功的写入发出，装配发件箱：`builder.AddXiHan(x => x.UseStandardProfile().UseFeature<OutboxFeature>())`。发件箱、投递循环与消息处理见[消息与事件](./messaging)。

## 在管道外开工作单元

单例服务、`IHostedFeature` 的启动与停止、后台循环、定时清理这些代码不经管道，没有 Transaction 阶段替它们开工作单元。框架自己的发件箱投递循环与审计记录落库就是这样做的：每次操作建一个服务作用域，在里面开一个工作单元。

1. 从根服务提供者 `CreateAsyncScope()`，一个作用域只承载一个工作单元。
2. 按租户分库时，先用 `TenantShardScope.BindTenantAsync(tenantId, ct)` 绑定分片，再解析任何持久化服务。
3. 取 `IUnitOfWork`，`BeginAsync(UnitOfWorkOptions.Default, ct)`；读写租户数据时传 `new UnitOfWorkOptions(tenantId)`。
4. 取仓储，读写。
5. `CommitAsync` 并检查返回的 `Result`。
6. 提交前任何一步抛出异常时，若工作单元仍是 `Active`，用 `CancellationToken.None` 回滚。

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;

namespace SimpleApp.Orders;

// 单例：每次清理自己建作用域与工作单元，不经管道
public sealed class OrderPurger(IServiceProvider services, TimeProvider clock)
{
    public async Task<int> PurgeAsync(string tenantId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        // 订单是多租户实体：工作单元不带租户就既看不到也写不了
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(tenantId), cancellationToken);
        try
        {
            var orders = scope.ServiceProvider.GetRequiredService<IRepository<Order, Guid>>();
            var cutoff = clock.GetUtcNow().AddDays(-30);
            var stale = await orders.ListAsync(EntityQuery.All<Order>().Where(Specification.Of<Order>(o => o.CreatedAt < cutoff)), cancellationToken);
            foreach (var result in await orders.RemoveRangeAsync(stale, cancellationToken))
            {
                result.ThrowIfFailure();
            }

            (await unitOfWork.CommitAsync(cancellationToken)).ThrowIfFailure();
            return stale.Count;
        }
        catch
        {
            // 取消也可能是异常的原因，回滚用 None 保证一定执行
            if (unitOfWork.State == UnitOfWorkState.Active)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
    }
}
```

登记为单例：`builder.Services.AddSingleton<OrderPurger>();`。`Result.ThrowIfFailure()` 把失败转成携带同一错误码的 `FrameworkException`，进入 `catch` 回滚。

按租户分库时，绑定必须在作用域里第一次创建持久化客户端之前完成；客户端建好后再绑定或改绑，`TenantShardScope` 抛出 `InvalidOperationException`。绑定只选连接，工作单元的 `TenantId` 仍由 `UnitOfWorkOptions` 给出，两者应一致：

```csharp
await using var scope = services.CreateAsyncScope();
// 先绑定分片再解析 IUnitOfWork 与仓储；停用的租户也能绑定
var shard = await scope.ServiceProvider.GetRequiredService<TenantShardScope>().BindTenantAsync(tenantId, cancellationToken);
_ = shard.ValueOrThrow();
var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
await unitOfWork.BeginAsync(new UnitOfWorkOptions(tenantId), cancellationToken);
```

`TenantShardScope` 在 `XiHan.Framework.MultiTenancy.Abstractions.Sharding` 命名空间，由多租户的实现包登记，见[多租户](./multi-tenancy#管道外绑定分片)。

::: warning 管道外的写入不自动派发领域事件
领域事件只由 Transaction 阶段的中间件派发。管道外写入带事件的聚合时，调用方在提交前自己派发，否则事件留在聚合的缓冲里，不会发出。
:::

管道外需要派发时，按中间件的做法在提交前调用派发器：

```csharp
var pending = unitOfWork.DomainEvents.Pending();
if (pending.Count > 0)
{
    // 派发器由 AddDomainEvent 注册；派发成功会清空各聚合的缓冲
    var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
    (await dispatcher.DispatchAsync(pending, unitOfWork.TenantId, cancellationToken)).ThrowIfFailure();
}

(await unitOfWork.CommitAsync(cancellationToken)).ThrowIfFailure();
```

## 只读工作单元

`new UnitOfWorkOptions(IsReadOnly: true)` 开启只读工作单元，查询照常，写入被拒绝并返回 `PERSISTENCE_VALIDATION_002`：

| Provider | 拒绝的时机 |
| --- | --- |
| 内存 | 写入先进覆盖层，提交时统一拒绝并记为回滚 |
| EF Core | 仓储写入时返回失败；绕过仓储改了跟踪实体的，提交时拒绝 |
| SqlSugar | 仓储写入时返回失败 |

只读工作单元不开数据库事务。框架读取租户目录与权限授予、调度与工作流探测租约时都用它，后台的统计、报表这类只读路径也建议用它。

## 错误码

| 错误码 | 含义 | 形式 |
| --- | --- | --- |
| `PERSISTENCE_VALIDATION_002` | 工作单元状态不允许该操作；只读工作单元写入 | 状态错误抛 `FrameworkException`；只读写入返回 `Result` |
| `PERSISTENCE_VALIDATION_003` | 聚合有未派发的领域事件，但没有注册派发器 | 调用结果 |
| `PERSISTENCE_UNAVAILABLE_001` | 仓储在没有活动工作单元时使用 | `FrameworkException` |
| `PERSISTENCE_CONFLICT_001` | 提交时乐观并发冲突 | 提交结果 |
| `PERSISTENCE_UNAVAILABLE_002` | 内存表在提交时超出容量 | 提交结果 |

其余持久化错误码见[持久化](./persistence#失败与错误码)。

## 与其他能力的关系

- Transaction 阶段在十一个阶段里的位置与中间件机制：[执行管道](./pipeline)。
- 仓储的读写语义与批量方法：[持久化](./persistence)。
- 领域事件的记录：[领域模型](./domain#领域事件)。
- 发件箱、消息发布与处理器：[消息与事件](./messaging)。
- 当前租户与分片绑定：[多租户](./multi-tenancy)。

## 相关

- [Persistence.Abstractions 包](../packages/persistence-abstractions)
- [Messaging.Abstractions 包](../packages/messaging-abstractions)
- [MultiTenancy.Abstractions 包](../packages/multitenancy-abstractions)
- 规范：RFC-0005《领域协议与持久化》
- 决策：ADR-0022、ADR-0025、ADR-0057
