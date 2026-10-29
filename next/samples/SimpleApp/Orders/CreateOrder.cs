// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

/// <summary>
/// 下单操作。
/// </summary>
/// <remarks>
/// ApiKey 认证 → 主体声明解析租户 → 按契约判定 <c>orders.write</c> 权限 → Validate 阶段按 Schema 与自校验拒绝坏请求 → 工作单元内写订单。订单记录的 <see cref="OrderPlaced"/> 领域事件由 Transaction 阶段在提交前经 <see cref="OrdersFeature"/> 登记的映射发成 CloudEvents（装配了发件箱时与订单写入同事务），操作里不再手动发布。一份契约多协议暴露：HTTP 上是 <c>POST /orders</c>，gRPC 上是 <c>Orders/Create</c>，MCP 上是 <c>orders_create</c> tool。
/// </remarks>
/// <param name="orders">订单仓储。</param>
/// <param name="ids">标识生成器。</param>
/// <param name="clock">时间源。</param>
[Operation(Id, Stability = Stability.Experimental, Expose = Expose.Http | Expose.Grpc | Expose.Mcp, Permission = "orders.write")]
public sealed class CreateOrder(IRepository<Order, Guid> orders, IIdGenerator<Guid> ids, TimeProvider clock) : IOperation<PlaceOrderRequest, OrderResponse>
{
    /// <summary>
    /// 操作 ID。
    /// </summary>
    public const string Id = "orders.create";

    /// <inheritdoc />
    public async ValueTask<Result<OrderResponse>> HandleAsync(PlaceOrderRequest request, InvocationContext context)
    {
        var order = new Order { Id = ids.NewId(), Name = request.Name, Amount = request.Amount };
        order.Place(clock.GetUtcNow());

        // 仓储返回 Result 而不是抛异常：失败直接作为操作结果，由宿主统一回写 Problem Details；
        // 仓储把订单登记到工作单元，订单上的 OrderPlaced 在提交前由 Transaction 阶段派发
        var added = await orders.AddAsync(order, context.CancellationToken);
        return added.IsFailure ? added.Error! : new OrderResponse(order.Id, order.Name, order.Amount, order.CreatedAt);
    }
}
