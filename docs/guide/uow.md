# 工作单元与事务

事务边界怎么划、提交时序是什么、嵌套怎么算——这一章的每条规则都对应过一个真实事故，值得完整读一遍。

完整 API 见 [Uow 包](../packages/uow)。

## 没标注就没有事务

::: danger 这是最容易误解的一条
框架判定「这个类型要不要被工作单元拦截」的条件是：

1. 类或任一方法上有 **`[UnitOfWork]`**，**或者**
2. 类型实现了 `IUnitOfWorkEnabled`。

而 **`IUnitOfWorkEnabled` 在框架里没有任何实现者**，`ApplicationServiceBase` 也没实现它。**也没有中间件替你开环境工作单元。**

所以：**不标注 = 没有事务**。多步写操作漏标注就是没有原子性——中间失败会留下半截数据，且不报错。
:::

```csharp
[UnitOfWork(true)]   // true = 需要事务
public async Task<OrderDto> CreateAsync(OrderCreateDto input)
{
    var order = await _orders.InsertReturnEntityAsync(...);
    await _items.InsertRangeAsync(...);       // 与上一步同一个事务
    return _mapper.Map(order);
}
```

## AOP 前提：服务类型必须是接口

工作单元靠 Castle 动态代理实现，而 `AddCastleDynamicProxy` 只处理 **`ServiceType.IsInterface` 为真**的服务描述器：

```csharp
services.AddScoped<MyService>();               // ❌ 注册为自身类型 → 不被代理 → [UnitOfWork] 静默失效
services.AddScoped<IMyService, MyService>();   // ✅ 接口 → 会被代理
```

标了特性却没生效时，**第一件事就是检查注册类型**。

## 手动控制

需要更细的边界时用 `IUnitOfWorkManager`：

```csharp
public class ImportService(IUnitOfWorkManager uowManager) : ITransientDependency
{
    public async Task ImportAsync(IReadOnlyList<Row> rows)
    {
        using var uow = uowManager.Begin(new XiHanUnitOfWorkOptions { IsTransactional = true });
        foreach (var row in rows) { /* … */ }
        await uow.CompleteAsync();     // 不调 Complete 就不会提交
    }
}
```

`using` 块结束时若没 `CompleteAsync`，工作单元视为未完成、不提交。

## `requiresNew`：真正独立的事务

```csharp
using var inner = uowManager.Begin(options, requiresNew: true);
```

`requiresNew` 同时意味着**新的逻辑工作单元**和**新的物理连接**——两者缺一，内层就还是跑在外层的连接和事务上。

::: warning 语义代价：内层提交后不再受外层回滚影响
所以**不要在已经修改过某些行的事务里，再用 `requiresNew` 去改同一批行**——两条独立连接改同一批行，轻则互相覆盖，重则死锁。

`requiresNew` 的正当用途是「无论主流程成败都要留下的记录」，如审计、失败日志。
:::

## 回滚之后不能再提交

```csharp
await innerUow.RollbackAsync();
// …外层继续…
await outerUow.CompleteAsync();   // ← 抛 XiHanException
```

**内层回滚 = 整体终止**。父子共用同一物理事务，所以内层判定失败就应该终止整体，而不是让外层若无其事地提交。

配套地，`SqlSugarClientResolver` 的钉住连接判定包含「是否已回滚」：检出已回滚的事务型工作单元会抛出，并提示改用 `Begin(requiresNew: true)`——避免回滚之后的写入落在一条已无事务的连接上被逐条自动提交。

## 提交时序

工作单元完成时的动作是**有先后的**：

```text
1. 本地事件发布        ← 提交之前
2. 提交事务
3. 分布式事件发布      ← 提交成功之后
4. UoW-完成后动作      ← 如缓存失效（considerUow: true 排队到这里）
```

为什么这么排：

| 步骤 | 原因 |
| --- | --- |
| **本地事件在提交前** | 其处理器可能继续写库，这些写入必须落在**同一个事务**里 |
| **分布式事件在提交后** | 否则「事务回滚了事件照发」——下游会看到一份从未落库的数据 |
| **缓存失效在提交后** | 提交前清缓存的话，并发读会回源读到**未提交的旧值**并写回缓存，事务提交后缓存反而是错的，且不会自愈 |

::: warning 分布式事件的取舍
提交成功后若投递失败，事件会丢。发件箱本就是进程内实现、进程一停全部丢失，这个改动没有引入新的丢失窗口。要强投递保证请自行接持久化发件箱。
:::

## 缓存失效必须 `considerUow: true`

```csharp
await cache.RemoveByPatternAsync(pattern, hideErrors: true, considerUow: true, token: ct);
```

`considerUow: true` 让失效排队到提交之后执行。**这不是可选优化，是正确性要求**——理由见上表第三行。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 接口返回 200 但数据没写 | 方法漏标 `[UnitOfWork]`；或内层回滚过 |
| `[UnitOfWork]` 标了没生效 | 服务注册类型不是接口 |
| 匿名端点调应用服务永久挂起 | 匿名端点没有 UoW 中间件，走代理会让拦截器急切开事务而死锁。用 `ProxyHelper.UnProxy` 取真实实例，或直接注入依赖 |
| 偶发读到旧值且不自愈 | 缓存失效没走 `considerUow: true` |
| `requiresNew` 好像没独立 | 升级到修复后的版本；注意别对同一批行用 |

## 下一步

- [数据访问](./data)：仓储与查询过滤器
- [缓存](./caching)：失效时序
- [事件总线](../packages/eventbus)：本地与分布式事件的区别
- [Uow 包](../packages/uow)：完整 API
