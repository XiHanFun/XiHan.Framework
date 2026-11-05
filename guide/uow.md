来源：https://framework.docs.xihanfun.com/guide/uow

# 工作单元与事务

本页讲 `IUnitOfWork` 的生命周期、Transaction 阶段怎样开启、提交与回滚、领域事件在提交前的派发，以及在管道之外（单例服务、后台任务、停机刷写）自己开工作单元的写法，适用于需要控制事务边界的应用开发者。完整 API 见 [Persistence.Abstractions 包](../packages/persistence-abstractions)。

## 概念

工作单元是一次调用的事务边界，作用域生命周期：同一个服务作用域里，仓储取到的是同一个 `IUnitOfWork`。它有四个状态，只能按 `NotStarted → Active → Committed | RolledBack` 推进；重复开始、未开始就提交这类违反顺序的调用抛出携带 `PERSISTENCE_VALIDATION_002` 的 `FrameworkException`。仓储只在 `Active` 状态下工作，否则抛出携带 `PERSISTENCE_UNAVAILABLE_001` 的 `FrameworkException`。

| 成员 | 说明 |
| --- | --- |
| `State` | 当前状态（`UnitOfWorkState`） |
| `TenantId` | 当前租户，无租户为 `null`；多租户实体据此写入与过滤 |
| `IsReadOnly` | 是否只读 |
| `HasTransaction` | 此刻是否已开数据库事务：第一次写入时开启，提交或回滚后复位，见下文[是否已开数据库事务](#是否已开数据库事务) |
| `DomainEvents` | 本工作单元写过的聚合（`DomainEventTracker`），按首次写入顺序、同一实例只记一次 |
| `BeginAsync(options, ct)` | 开始，只能调用一次；`UnitOfWorkOptions(TenantId, IsReadOnly)`，`UnitOfWorkOptions.Default` 为无租户、可写 |
| `CommitAsync(ct)` | 提交，返回 `Result`；并发冲突、标识重复、容量已满以失败结果返回 |
| `RollbackAsync(ct)` | 回滚 |

工作单元由持久化 Provider 实现并注册；Provider 的特性同时把 `UnitOfWorkMiddleware` 登记到 Transaction 阶段。

## 管道里的工作单元

经管道的每次调用（HTTP、gRPC、MCP、命令行、消息派发）都经过 Transaction 阶段，`UnitOfWorkMiddleware` 按下面的顺序处理：

1. 从调用的作用域取 `IUnitOfWork`。它已经开始过（同一个服务作用域里再经管道的嵌套调用）时，以 `PERSISTENCE_VALIDATION_002` 结束本次调用，不执行处理器，见下文[嵌套调用](#嵌套调用)。
2. 以 Tenant 阶段写入安全上下文的租户开始它：本次调用的操作契约只读（`get` / `list`，或 `ReadOnly = true`）时开只读工作单元，其余调用开可写工作单元。
3. 调用下一步（Handle 阶段的处理器）。
4. 处理器抛出异常：用 `CancellationToken.None` 回滚，异常原样抛出，由内核转为 Unexpected 结果；只读工作单元里直接写入入口抛出的 `PERSISTENCE_UNEXPECTED_001` 例外，回滚后转为同码的结果。
5. 调用结果是失败：回滚，保留原失败结果。
6. 有聚合带着未派发的领域事件：没有注册 `IDomainEventDispatcher` 时以 `PERSISTENCE_VALIDATION_003` 结束并回滚；派发器返回失败时回滚，调用以该失败结束；派发成功清空登记，进入提交。
7. 提交；提交失败时用它覆盖调用结果，调用方看到真实的持久化错误。

所以操作代码不调用提交，只决定成败：返回 `Result` 失败或抛出异常，本次调用的全部写入都回滚，包括批量方法里已通过校验的项。

数据库事务是延迟开启的：EF Core 与 SqlSugar Provider 在第一次写入时才开事务，只读过数据的调用不开事务，提交时也无事可做。这让多个只读调用与一个写入调用能在 SQLite 这类单写者数据库上并存。

### 是否已开数据库事务

写操作的工作单元从调用开始就是 `Active`，但事务要到第一次写入才开，`State` 说明不了这一点。`IUnitOfWork.HasTransaction` 报告本工作单元此刻是否开着数据库事务（ADR-0109）：

| 时机 | `HasTransaction` |
| --- | --- |
| 未开始；刚开始 | 假 |
| 查找、按标识集合查找、查询、计数、空集合的批量方法之后 | 假 |
| 仓储的单条写入，或集合非空的批量写入之后（不论这次写入成功与否） | 真 |
| EF Core / SqlSugar 的 `EnsureTransactionAsync` 之后 | 真 |
| 提交（成功或返回失败）、回滚之后 | 假 |
| 只读工作单元里任何时候 | 假 |

- 多连接时一个值：SqlSugar 的第一次写入落在哪个连接都会开事务，任一连接开着即为真。
- 绑定命名连接的工作单元只报告那个连接。不同服务作用域的工作单元各报各的：业务调用里另开作用域写入并提交，不让业务调用的工作单元变真。
- 内存实现没有数据库事务，第一次写入调用起同样为真，直到提交或回滚：覆盖层里这时有未提交、可能随回滚丢弃的变更，与真实库上的含义相同，测试里就能拦住误用。
- 不经仓储、也不经 `EnsureTransactionAsync` 直接对客户端执行的写入不在事务里，也不让它变真。

调用里要立即对外产生、不随回滚撤回的副作用（立即推送、调用外部系统）之前据此判断。为假时本工作单元还没有可能回滚的写入，可以先推送、最后写库；为真时改用随提交生效的做法（发件箱、提交之后再发）：

```csharp
// 本工作单元写过数据时立即推送会描述可能回滚的数据；还没写入的调用（只读操作、先推送后写库的回复）照常立即发出
if (services.GetService<IUnitOfWork>() is { HasTransaction: true })
{
    throw new InvalidOperationException("The push is sent immediately and cannot follow a write in the same call; send it after the commit.");
}
```

`HasTransaction` 只在 `Active` 时为真，不必再判 `State`。

只读操作的工作单元从一开始就是只读的，连事务都不开；它的写入怎样被拒绝见下文[只读操作与只读工作单元](#只读操作与只读工作单元)。

## 提交与失败

| 情况 | 工作单元 | 调用结果 |
| --- | --- | --- |
| 处理器成功，没有待派发事件 | 提交 | 处理器的结果 |
| 处理器返回失败 | 回滚 | 原失败 |
| 处理器抛出异常 | 回滚 | Unexpected |
| 有待派发事件但没有派发器 | 回滚 | `PERSISTENCE_VALIDATION_003` |
| 派发器返回失败 | 回滚 | 派发器的失败 |
| 提交时并发冲突（EF Core 的并发令牌在提交时不匹配；内存实现提交时发现更新或删除的行已被别人改过或删掉） | 回滚 | `PERSISTENCE_CONFLICT_001` |
| 提交时标识重复（内存实现发现新增的标识已被别人提交；关系型 Provider 写入时已撞上主键、处理器却返回了成功） | 回滚 | `PERSISTENCE_CONFLICT_002` |
| 提交时内存表超出容量 | 回滚 | `PERSISTENCE_UNAVAILABLE_002` |
| 只读工作单元拒绝过写入（处理器忽略了那次失败而返回成功） | 回滚 | `PERSISTENCE_UNEXPECTED_001` |
| 只读工作单元里直接写入入口 `EnsureTransactionAsync` 抛出 | 回滚 | `PERSISTENCE_UNEXPECTED_001` |
| EF Core 只读工作单元有未保存的跟踪变更 | 回滚 | `PERSISTENCE_UNEXPECTED_001` |
| 同一服务作用域里再经管道的嵌套调用 | 不动外层的工作单元 | `PERSISTENCE_VALIDATION_002`（只结束内层） |

标识重复在 `AddAsync` / `AddRangeAsync` 时就按项返回 `PERSISTENCE_CONFLICT_002`，不留到提交。两个工作单元并发新增同一标识时，后到者在关系型 Provider 上于写入时得到它，之后提交只回滚并返回同一错误；在内存实现上于提交时得到它（见[持久化的并发新增同一标识](./persistence#并发新增同一标识)）。

## 各 Provider 的事务

| | 内存 | EF Core | SqlSugar |
| --- | --- | --- | --- |
| 写入去向 | 本工作单元的覆盖层 | 每次写入立即 `SaveChanges`，在数据库事务内 | 每次写入立即执行，在数据库事务内 |
| 事务开启 | 不需要（第一次写入调用起 `HasTransaction` 为真） | 第一次写入时 `BeginTransaction` | 第一次写入时经 `AsTenant()` 在客户端的全部连接上开事务 |
| 读己之写 | 覆盖层叠在已提交的表上 | 同一事务 | 同一事务 |
| 提交 | 涉及的表依次加锁，逐表校验首次读到的状态后应用覆盖层 | 保存残余变更后提交事务 | 逐连接顺序提交 |
| 回滚 | 丢弃覆盖层 | 回滚事务并清空变更跟踪器 | 逐连接回滚 |
| 原子性边界 | 一次提交涉及的全部表：任一张表校验失败，所有表都不改 | 一个 `DbContext` 一个连接 | 每个连接各自原子；跨连接是顺序提交，不是两阶段提交 |
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

## 只读操作与只读工作单元

只读操作——动词是 `get` / `list`，或以 `[Operation(..., ReadOnly = true)]` 声明的查询变体（`departments.tree`、`users.list-online`）——在 Transaction 阶段得到只读工作单元（ADR-0073）。管道外用 `new UnitOfWorkOptions(IsReadOnly: true)` 开启的也一样：

- 不开数据库事务；查找、查询、计数照常。
- 仓储的新增、更新、删除与批量形式在写入时就拒绝、不碰后端，返回 `PERSISTENCE_UNEXPECTED_001`（批量逐项都是它；空集合不算写入，仍返回空列表）。
- 拒绝同时把工作单元记为失败。处理器忽略这个失败、照常返回成功时，提交回滚并返回同一错误，调用以它结束——误写不会被悄悄吞掉。
- 直接用 `DbContext` 或 SqlSugar 客户端写入之前要调用工作单元的 `EnsureTransactionAsync`：只读工作单元里它抛出携带 `PERSISTENCE_UNEXPECTED_001` 的 `FrameworkException`，写入不会执行；Transaction 阶段把它转为同码的结果：仍是 500，但错误码直接指出是只读写入，不是笼统的 `KERNEL_UNEXPECTED_001`。不经 `EnsureTransactionAsync` 直接执行的写入不在守卫之内，也不在事务里。
- EF Core 提交时还查变更跟踪器：只读操作里把读出的实体改了却没保存，同样以 `PERSISTENCE_UNEXPECTED_001` 回滚。只读操作里不要把读出的实体当草稿修改，要变换就映射成响应对象。

`PERSISTENCE_UNEXPECTED_001` 是操作实现的缺陷，不是调用方能改正的输入，也不是权限不足：HTTP 500、gRPC `Internal`，与其他服务端缺陷一起进 5xx 告警，配置了熔断的操作照常计数（ADR-0081）。修正的办法是让操作不再写入，或者它本来就要写（记访问次数、补建缺失的行），就去掉只读声明、换一个非只读的动词，或把写入放进新服务作用域里的另一次调用（见下文）。

| Provider | 仓储写入 | `EnsureTransactionAsync` | 提交 |
| --- | --- | --- | --- |
| 内存 | 进覆盖层之前拒绝 | 没有这个入口 | 返回写入时的拒绝 |
| EF Core | 写入前拒绝 | 抛出 | 返回写入时的拒绝；有未保存的跟踪变更也拒绝 |
| SqlSugar | 写入前拒绝 | 抛出 | 返回写入时的拒绝 |

框架读取租户目录与权限授予、审计查询、调度与工作流探测租约时自己开只读工作单元，其中租户目录与权限授予设了 `ReadsPrimary`，配了从库也读主库（[读写分离](./persistence#读写分离)）；后台的统计、报表这类只读路径也建议用它。框架的发件箱、审计落库、调度存储与工作流的租约写入时开的是可写工作单元（`UnitOfWorkOptions.Default`），不受操作契约影响。工作流引擎在调用里加入调用方的工作单元（ADR-0077），只读调用里的写入在执行活动之前返回 `PERSISTENCE_UNEXPECTED_001`。

## 嵌套调用

一次调用一个工作单元，各调用按自己的契约开自己的。在操作里经管道再调另一个操作（`IOperationInvoker.InvokeAsync`、`App.InvokeAsync`），要新建服务作用域，内层才有自己的工作单元：

```csharp
// 在 orders.get 的处理器里：记一次查看，写入交给 order-views.create（scopes 是构造函数注入的 IServiceScopeFactory）
await using var scope = scopes.CreateAsyncScope();
var call = App.CreateContext(scope.ServiceProvider, context.CancellationToken, baggage: new Dictionary<string, string>(context.Baggage, StringComparer.Ordinal));
call.SetSecurity(context.GetSecurity());   // 以调用方的身份执行，Authorize 阶段照常按契约判定
var invoker = scope.ServiceProvider.GetRequiredService<IOperationInvoker>();
var recorded = await invoker.InvokeAsync<RecordViewRequest, RecordViewResponse>(call, RecordView.Id, new RecordViewRequest(request.Id));
```

- 只读不向内、也不向外传播：只读操作里调用的写操作在自己的可写工作单元里写入、成功即自行提交；外层随后失败也不会把它回滚（它不在外层的事务里），外层自己的仓储仍然只读。写操作里调用只读操作，内层只读。要与外层同进退的写入不应拆成嵌套调用。
- 用同一个服务作用域再经管道——传入外层的 `context`、由它派生的上下文，或 `App.CreateContext(context.Services, …)`——作用域里的工作单元已经开始过：内层以 `PERSISTENCE_VALIDATION_002`（消息键 `persistence.unit_of_work.nested`）结束，处理器不执行，外层的工作单元不受影响。内层不加入外层的工作单元：加入会让内层写入的成败取决于外层是否只读，内层失败时已写入的部分又会随外层一起提交。

AI 工具与 MCP 的每次 `tools/call` 都按这个方式执行，各自一个服务作用域。

## 错误码

| 错误码 | 含义 | 形式 |
| --- | --- | --- |
| `PERSISTENCE_VALIDATION_002` | 工作单元状态不允许该操作；同一服务作用域里再经管道的嵌套调用 | 状态错误抛 `FrameworkException`；嵌套调用是调用结果 |
| `PERSISTENCE_UNEXPECTED_001` | 只读工作单元拒绝写入（操作实现的缺陷，HTTP 500） | 仓储写入返回 `Result`、提交返回同一错误；`EnsureTransactionAsync` 抛 `FrameworkException`，Transaction 阶段转为结果 |
| `PERSISTENCE_VALIDATION_003` | 聚合有未派发的领域事件，但没有注册派发器 | 调用结果 |
| `PERSISTENCE_UNAVAILABLE_001` | 仓储在没有活动工作单元时使用 | `FrameworkException` |
| `PERSISTENCE_CONFLICT_001` | 提交时乐观并发冲突 | 提交结果 |
| `PERSISTENCE_CONFLICT_002` | 提交时标识重复（并发新增同一标识） | 提交结果 |
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
- 决策：ADR-0022、ADR-0025、ADR-0057、ADR-0109
