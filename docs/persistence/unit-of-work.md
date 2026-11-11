# 工作单元与事务

这一页讲 `IUnitOfWork`：它的状态与成员、Transaction 阶段怎样为每次调用开启、提交与回滚、数据库事务为什么延迟到第一次写入、领域事件在提交前怎样派发，以及在管道之外自己开工作单元的写法。读完它，你能判断一次调用里哪些写入会一起提交、失败时会回滚什么，并能在后台任务里正确地读写数据。前置知识是[仓储读写](/persistence/repositories)与[执行管道](/startup/pipeline)。

## 为什么需要

没有工作单元时，每个操作都要自己开事务、决定何时提交、异常时记得回滚，忘了任何一步都会留下半截数据；领域事件发早了事务可能回滚，发晚了又可能丢。框架把事务边界放进管道的 Transaction 阶段：每次调用一个工作单元，处理器只决定成败，提交与回滚由中间件按固定顺序执行，领域事件在提交前派发。数据库事务延迟到第一次写入才开，只读调用不占事务。

## 概念

工作单元是一次调用的事务边界，作用域生命周期：同一个服务作用域里，仓储取到的是同一个 `IUnitOfWork`。它有四个状态，只能按 `NotStarted → Active → Committed | RolledBack` 推进；重复开始、未开始就提交这类违反顺序的调用抛出携带 `PERSISTENCE_VALIDATION_002` 的 `FrameworkException`。仓储只在 `Active` 状态下工作，否则抛出携带 `PERSISTENCE_UNAVAILABLE_001` 的 `FrameworkException`。

| 成员 | 说明 |
| --- | --- |
| `State` | 当前状态（`UnitOfWorkState`） |
| `TenantId` | 当前租户，无租户为 `null`；多租户实体据此写入与过滤 |
| `IsReadOnly` | 是否只读 |
| `HasTransaction` | 此刻是否已开数据库事务：第一次写入时开启，提交或回滚后复位 |
| `DomainEvents` | 本工作单元写过的聚合（`DomainEventTracker`），按首次写入顺序、同一实例只记一次 |
| `BeginAsync(options, ct)` | 开始，只能调用一次 |
| `CommitAsync(ct)` | 提交，返回 `Result`；并发冲突、标识重复、容量已满以失败结果返回 |
| `RollbackAsync(ct)` | 回滚 |

`UnitOfWorkOptions { TenantId, IsReadOnly, Connection, ReadsPrimary }` 是开启选项：`TenantId` 是当前租户；`IsReadOnly` 开只读工作单元（不开事务、拒绝写入）；`Connection` 绑定一个命名连接，只在它上面读写、开事务（见[连接与运行时数据源](/persistence/connections)）；`ReadsPrimary` 让只读工作单元读主库；`UnitOfWorkOptions.Default` 是无租户、可写、不绑定连接。

工作单元由持久化 Provider 实现并注册；Provider 的特性同时把 `UnitOfWorkMiddleware` 登记到 Transaction 阶段。

## 管道里的工作单元

经管道的每次调用（HTTP、gRPC、MCP、命令行、消息派发）都经过 Transaction 阶段，`UnitOfWorkMiddleware` 按下面的顺序处理：

1. 从调用的作用域取 `IUnitOfWork`。它已经开始过（同一个服务作用域里的嵌套调用）时，以 `PERSISTENCE_VALIDATION_002` 结束本次调用，不执行处理器。
2. 以 Tenant 阶段写入安全上下文的租户开始它：操作契约只读（动词 `get` / `list`，或 `ReadOnly = true`）时开只读工作单元，其余开可写工作单元。
3. 调用 Handle 阶段的处理器。
4. 处理器抛出异常：用 `CancellationToken.None` 回滚，异常原样抛出，由内核转为 Unexpected 结果。
5. 调用结果是失败：回滚，保留原失败结果。
6. 有聚合带着未派发的领域事件：没有派发器时以 `PERSISTENCE_VALIDATION_003` 结束并回滚；派发失败时回滚，调用以该失败结束；派发成功清空登记。
7. 提交；提交失败时用它覆盖调用结果，调用方看到真实的持久化错误。

所以操作代码不调用提交，只决定成败：返回 `Result` 失败或抛出异常，本次调用的全部写入都回滚，包括批量方法里已通过校验的项。

### 是否已开数据库事务

EF Core 与 SqlSugar Provider 在第一次写入时才开事务，只读过数据的调用不开事务，提交时也无事可做。写操作的工作单元从调用开始就是 `Active`，但事务要到第一次写入才开，`State` 说明不了这一点，`IUnitOfWork.HasTransaction` 报告此刻是否开着数据库事务：查找、查询、计数与空集合的批量方法之后为假；仓储的单条写入或非空批量写入之后（不论成败）、`EnsureTransactionAsync` 之后为真；提交（成功或返回失败）与回滚之后为假；只读工作单元里恒为假。多连接时一个值，任一连接开着即为真；绑定命名连接的工作单元只报告那个连接；内存实现没有数据库事务，第一次写入调用起同样为真。不经仓储、也不经 `EnsureTransactionAsync` 直接对客户端执行的写入不在事务里，也不让它变真。

调用里要立即对外产生、不随回滚撤回的副作用（立即推送、调用外部系统）之前据此判断：为假时本工作单元还没有可能回滚的写入，可以先推送、最后写库；为真时改用随提交生效的做法（发件箱、提交之后再发）。`HasTransaction` 只在 `Active` 时为真，不必再判 `State`。

## 最小示例

一个操作里既读又写，看 Transaction 阶段替它做了什么。`SampleMessages.ResourceMissing` 是 SimpleApp 自己的消息键。

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

public sealed record AdjustAmountRequest(Guid Id, int Delta);

[Operation("orders.adjust", Permission = "orders.write")]
public sealed class AdjustAmount(IRepository<Order, Guid> orders) : IOperation<AdjustAmountRequest, OrderResponse>
{
    public async ValueTask<Result<OrderResponse>> HandleAsync(AdjustAmountRequest request, InvocationContext context)
    {
        // 读：不开数据库事务
        var found = await orders.FindAsync(request.Id, context.CancellationToken);
        if (!found.TryGetValue(out var order))
        {
            return Error.NotFound("SAMPLES_NOTFOUND_001", SampleMessages.ResourceMissing);
        }

        order.Amount += request.Delta;
        // 写：第一次写入时开事务；失败作为结果返回，中间件回滚；成功则中间件派发领域事件（如有）并提交
        var updated = await orders.UpdateAsync(order, context.CancellationToken);
        return updated.IsFailure ? updated.Error! : new OrderResponse(order.Id, order.Name, order.Amount, order.CreatedAt);
    }
}
```

时间线：Transaction 阶段以当前租户开可写工作单元；`FindAsync` 不开事务；`UpdateAsync` 开事务并立即写入（EF Core 与 SqlSugar）或进覆盖层（内存）；处理器返回成功，中间件提交。更新失败（如并发戳过期）时处理器返回失败，中间件回滚，存储里不留痕迹。

## 提交与失败

| 情况 | 工作单元 | 调用结果 |
| --- | --- | --- |
| 处理器成功，没有待派发事件 | 提交 | 处理器的结果 |
| 处理器返回失败 / 抛出异常 | 回滚 | 原失败 / Unexpected |
| 有待派发事件但没有派发器 | 回滚 | `PERSISTENCE_VALIDATION_003` |
| 派发器返回失败 | 回滚 | 派发器的失败 |
| 提交时并发冲突 / 标识重复 / 内存表超出容量 | 回滚 | `PERSISTENCE_CONFLICT_001` / `PERSISTENCE_CONFLICT_002` / `PERSISTENCE_UNAVAILABLE_002` |
| 只读工作单元拒绝过写入、`EnsureTransactionAsync` 抛出、EF Core 有未保存的跟踪变更 | 回滚 | `PERSISTENCE_UNEXPECTED_001` |
| 同一服务作用域里再经管道的嵌套调用 | 不动外层的工作单元 | `PERSISTENCE_VALIDATION_002`（只结束内层） |

标识重复通常在 `AddAsync` 时就按项返回，只有并发新增同一标识的后到者在内存实现上于提交时得到它，见[仓储读写](/persistence/repositories)。

## 各 Provider 的事务

- 内存：写入进本工作单元的覆盖层，覆盖层叠在已提交的表上实现读己之写；提交时给涉及的表依次加锁、逐表校验首次读到的状态后应用覆盖层，一次提交涉及的全部表原子；回滚丢弃覆盖层。
- EF Core：第一次写入时 `BeginTransaction`，每次写入立即 `SaveChanges`，同一事务内可见；提交先保存残余变更再提交事务，回滚同时清空变更跟踪器；一个 `DbContext` 一个连接。
- SqlSugar：第一次写入时经 `AsTenant()` 在客户端的全部连接上开事务，每次写入立即执行；提交逐连接顺序提交，回滚逐连接回滚；每个连接各自原子，跨连接是顺序提交，不是两阶段提交，后一个连接提交失败时前一个已经提交，需要严格原子性的写入放在同一个库。

作用域结束时事务仍未提交（中间件之外直接使用），释放即回滚。

## 领域事件与发件箱

仓储在新增、更新、删除（单条与批量里通过校验的项）成功后，把实现 `IHasDomainEvents` 的实体登记到 `IUnitOfWork.DomainEvents`。处理器成功后、提交前，中间件取出带有未派发事件的聚合（`DomainEventTracker.Pending()`），连同工作单元的租户交给 `IDomainEventDispatcher.DispatchAsync`。

派发器由 Messaging 子系统实现：`AddDomainEvent<TEvent>(topic, type, jsonTypeInfo, subject)` 登记一种事件的主题、CloudEvents 类型、源生成的 `JsonTypeInfo` 与可选的 `subject`，并注册派发器；SimpleApp 的 `OrdersFeature` 就这样登记 `OrderPlaced`。派发发生在提交之前，事件去向取决于是否装配了发件箱：

| 装配 | 事件的去向 | 与业务写入的关系 |
| --- | --- | --- |
| `OutboxFeature` | 写进发件箱表，与业务写入在同一事务；提交后由投递循环发到代理 | 一起提交、一起回滚 |
| 没有发件箱 | 直接发布到代理 | 发布失败回滚业务写入；发布成功而随后提交失败时，事件已经发出 |

要让事件只随成功的写入发出，装配发件箱：`builder.AddXiHan(x => x.UseStandardProfile().UseFeature<OutboxFeature>())`。发件箱表、投递循环、事件到消息的映射与消费端的写法见[领域事件与发件箱](/messaging/domain-events)。

## 在管道外开工作单元

单例服务、`IHostedFeature` 的启动与停止、后台循环、定时清理这些代码不经管道，没有 Transaction 阶段替它们开工作单元。框架自己的发件箱投递循环与审计落库就是这样做的：每次操作建一个服务作用域，在里面开一个工作单元。

1. 从根服务提供者 `CreateAsyncScope()`，一个作用域只承载一个工作单元；按租户分库时先用 `TenantShardScope.BindTenantAsync(tenantId, ct)` 绑定分片。
2. 取 `IUnitOfWork`，`BeginAsync(UnitOfWorkOptions.Default, ct)`；读写租户数据时传 `new UnitOfWorkOptions { TenantId = tenantId }`。
3. 取仓储读写，`CommitAsync` 并检查返回的 `Result`。
4. 提交前任何一步抛出异常时，若工作单元仍是 `Active`，用 `CancellationToken.None` 回滚。

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
        await unitOfWork.BeginAsync(new UnitOfWorkOptions { TenantId = tenantId }, cancellationToken);
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

按租户分库时，绑定必须在作用域里第一次创建持久化客户端之前完成；绑定只选连接，工作单元的 `TenantId` 仍由 `UnitOfWorkOptions` 给出，两者应一致。见[租户分片与分库](/multi-tenancy/sharding)。

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
    (await dispatcher.DispatchAsync(pending, unitOfWork.Options.TenantId, cancellationToken)).ThrowIfFailure();
}

(await unitOfWork.CommitAsync(cancellationToken)).ThrowIfFailure();
```

## 只读操作与只读工作单元

只读操作，也就是动词是 `get` / `list`，或以 `[Operation(..., ReadOnly = true)]` 声明的查询变体（`departments.tree`、`users.list-online`），在 Transaction 阶段得到只读工作单元。管道外用 `new UnitOfWorkOptions { IsReadOnly = true }` 开启的也一样：

- 不开数据库事务；查找、查询、计数照常。
- 仓储的新增、更新、删除与批量形式在写入时就拒绝、不碰后端，返回 `PERSISTENCE_UNEXPECTED_001`，并把工作单元记为失败：处理器忽略这个失败、照常返回成功时，提交回滚并返回同一错误，误写不会被悄悄吞掉。
- 直接用 `DbContext` 或 SqlSugar 客户端写入之前要调用工作单元的 `EnsureTransactionAsync`：只读工作单元里它抛出同码的 `FrameworkException`，Transaction 阶段转为结果。不经它直接执行的写入不在守卫之内，也不在事务里。
- EF Core 提交时还查变更跟踪器：只读操作里把读出的实体改了却没保存，同样以 `PERSISTENCE_UNEXPECTED_001` 回滚。只读操作里要变换就映射成响应对象，不要把读出的实体当草稿改。

`PERSISTENCE_UNEXPECTED_001` 是操作实现的缺陷，不是调用方能改正的输入：HTTP 500、gRPC `Internal`。修正的办法是让操作不再写入，或者它本来就要写，就换一个非只读的动词，或把写入放进新服务作用域里的另一次调用。

框架读取租户目录与权限授予、审计查询、探测租约时自己开只读工作单元；后台的统计、报表这类只读路径也建议用它，配了从库又要读主库时设 `ReadsPrimary`：

```csharp
await unitOfWork.BeginAsync(new UnitOfWorkOptions { IsReadOnly = true, ReadsPrimary = true }, cancellationToken);
```

## 嵌套调用

一次调用一个工作单元，各调用按自己的契约开自己的。在操作里经管道再调另一个操作（`IOperationInvoker.InvokeAsync`），要新建服务作用域，内层才有自己的工作单元：

```csharp
// 在 orders.get 的处理器里：记一次查看，写入交给 order-views.create（scopes 是注入的 IServiceScopeFactory）
await using var scope = scopes.CreateAsyncScope();
var call = App.CreateContext(scope.ServiceProvider, context.CancellationToken);
call.SetSecurity(context.GetSecurity());   // 以调用方的身份执行，Authorize 阶段照常按契约判定
var recorded = await scope.ServiceProvider.GetRequiredService<IOperationInvoker>()
    .InvokeAsync<RecordViewRequest, RecordViewResponse>(call, RecordView.Id, new RecordViewRequest(request.Id));
```

只读不向内、也不向外传播：只读操作里调用的写操作在自己的可写工作单元里提交，外层随后失败也不会把它回滚；要与外层同进退的写入不应拆成嵌套调用。用同一个服务作用域再经管道（传入外层的 `context`）时，内层以 `PERSISTENCE_VALIDATION_002` 结束，处理器不执行，外层不受影响。AI 工具与 MCP 的每次 `tools/call` 都按这个方式执行。

## 错误码

本页涉及的错误码：`PERSISTENCE_VALIDATION_002`（状态不允许，抛 `FrameworkException`；嵌套调用是调用结果）、`PERSISTENCE_UNEXPECTED_001`（只读工作单元拒绝写入，HTTP 500）、`PERSISTENCE_VALIDATION_003`（有事件没有派发器）、`PERSISTENCE_UNAVAILABLE_001`（没有活动工作单元，抛 `FrameworkException`），以及提交结果里的 `PERSISTENCE_CONFLICT_001` / `002` 与 `PERSISTENCE_UNAVAILABLE_002`。完整表见[持久化与 Provider](/persistence/overview)。

## 常见问题

### 我需要在操作里手动调 `CommitAsync` 吗

不需要，也不应该。Transaction 阶段在处理器成功返回后提交、失败或异常时回滚，操作里只返回 `Result`。手动提交会让中间件的提交撞上 `PERSISTENCE_VALIDATION_002`。

### 为什么我的 `GET` 操作写入时得到 500

`get` / `list` 动词的操作开只读工作单元，写入被拒绝并以 `PERSISTENCE_UNEXPECTED_001` 结束。这是操作实现的缺陷：要写就换一个非只读的动词，或把写入放进新服务作用域里的另一次调用。

### 领域事件在没有装配发件箱时安全吗

不完全安全：事件在提交前直接发布到代理，发布成功而随后提交失败时事件已经发出。要「事件只随成功的写入发出」就装配 `OutboxFeature`，事件与业务写入同事务。

## 相关包与下一步

- 相关包：[Persistence.Abstractions](/packages/persistence-abstractions)、[Persistence.InMemory](/packages/persistence-inmemory)、[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)、[Persistence.SqlSugar](/packages/persistence-sqlsugar)、[Messaging.Abstractions](/packages/messaging-abstractions)、[MultiTenancy.Abstractions](/packages/multitenancy-abstractions)
- 规范：RFC-0005《领域协议与持久化》
- 下一页：[EF Core Provider](/persistence/entity-framework-core)，讲怎样让应用自己的 `DbContext` 接入仓储与工作单元
