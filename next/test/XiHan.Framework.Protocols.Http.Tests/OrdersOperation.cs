// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 同一实现登记在多个操作 ID 下：回显上下文里的操作 ID 与绑定到的字段；名字为 <c>fail</c> 时返回冲突。
/// </summary>
internal sealed class OrdersOperation : IOperation<OrderRequest, OrderReply>
{
    /// <inheritdoc />
    public ValueTask<Result<OrderReply>> HandleAsync(OrderRequest request, InvocationContext context)
    {
        if (request.Name == "fail")
        {
            return ValueTask.FromResult<Result<OrderReply>>(Error.Conflict("ORDERS_CONFLICT_001", "orders.order.rejected"));
        }

        var operation = context.GetOperation().Map(static d => d.Id).ValueOr("-");
        return ValueTask.FromResult(Result.Success(new OrderReply(operation, request.Id, request.Name, request.Page, request.Tags ?? [], request.Reason)));
    }
}
