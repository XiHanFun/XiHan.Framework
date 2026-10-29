// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

/// <summary>
/// 列出当前租户订单的操作。
/// </summary>
/// <remarks>
/// 多租户过滤由持久化子系统按上下文里的租户自动施加。HTTP 上是 <c>GET /orders</c>。
/// </remarks>
/// <param name="orders">订单仓储。</param>
[Operation(Id, Stability = Stability.Experimental, Expose = Expose.Http | Expose.Grpc | Expose.Mcp, Permission = "orders.read")]
public sealed class ListOrders(IRepository<Order, Guid> orders) : IOperation<ListOrdersRequest, OrderList>
{
    /// <summary>
    /// 操作 ID。
    /// </summary>
    public const string Id = "orders.list";

    /// <inheritdoc />
    public async ValueTask<Result<OrderList>> HandleAsync(ListOrdersRequest request, InvocationContext context)
    {
        var list = await orders.ListAsync(EntityQuery.All<Order>().OrderBy(static o => o.CreatedAt), context.CancellationToken);
        return new OrderList(list.Select(static o => new OrderResponse(o.Id, o.Name, o.Amount, o.CreatedAt)).ToList());
    }
}
