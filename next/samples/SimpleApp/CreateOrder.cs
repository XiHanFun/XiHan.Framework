// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace SimpleApp;

/// <summary>
/// 下单操作：ApiKey 认证 → 主体声明解析租户 → 按契约判定 <c>orders.write</c> 权限 → 工作单元内写订单并经发件箱发消息。
/// 一份契约多协议暴露：HTTP 上是 <c>POST /orders</c>，MCP 上是 <c>orders_create</c> tool。
/// </summary>
/// <param name="orders">订单仓储。</param>
/// <param name="ids">标识生成器。</param>
/// <param name="publisher">消息发布者（发件箱）。</param>
/// <param name="clock">时间源。</param>
[Operation(Id, Stability = Stability.Experimental, Expose = Expose.Http | Expose.Mcp, Permission = "orders.write")]
public sealed class CreateOrder(IRepository<Order, Guid> orders, IIdGenerator<Guid> ids, IMessagePublisher publisher, TimeProvider clock) : IOperation<PlaceOrderRequest, OrderResponse>
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

        // 仓储返回 Result 而不是抛异常：失败直接作为操作结果，由宿主统一回写 Problem Details
        var added = await orders.AddAsync(order, context.CancellationToken);
        if (added.IsFailure)
        {
            return added.Error!;
        }

        // 消息走发件箱：与订单写入同属一个工作单元，事务提交后才真正投递
        var response = new OrderResponse(order.Id, order.Name, order.Amount, order.CreatedAt);
        var envelope = new MessageEnvelope
        {
            Id = order.Id.ToString("N"),
            Source = "/orders",
            Type = "orders.placed",
            Subject = $"order/{order.Id:N}",
            Time = order.CreatedAt,
            Data = JsonSerializer.SerializeToUtf8Bytes(response, SampleJsonContext.Default.OrderResponse),
        }.WithExtension(EnvelopeExtensions.Tenant, order.TenantId);

        var published = await publisher.PublishAsync(OrdersFeature.Topic, envelope, context.CancellationToken);
        return published.IsFailure ? published.Error! : response;
    }
}
