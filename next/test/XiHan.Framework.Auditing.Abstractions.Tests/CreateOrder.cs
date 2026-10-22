// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Tests;

/// <summary>
/// 声明了权限的创建操作：补充资源与细节；金额为负返回校验失败，为 999 抛出异常。
/// </summary>
[Operation(Id, Permission = "orders.write")]
internal sealed class CreateOrder : IOperation<CreateOrderRequest, OrderResponse>
{
    /// <summary>
    /// 操作 ID。
    /// </summary>
    public const string Id = "orders.create";

    /// <inheritdoc />
    public ValueTask<Result<OrderResponse>> HandleAsync(CreateOrderRequest request, InvocationContext context)
    {
        context.SetAuditResource(new ResourceName("global", "orders", "acme", "order", "o-1"));
        context.AddAuditDetail("amount", request.Amount);
        if (request.Amount < 0)
        {
            return ValueTask.FromResult<Result<OrderResponse>>(Error.Validation("ORDERS_VALIDATION_001", "orders.amount.negative"));
        }

        if (request.Amount == 999)
        {
            throw new InvalidOperationException("The order pipeline crashed.");
        }

        return ValueTask.FromResult(Result.Success(new OrderResponse("o-1")));
    }
}
