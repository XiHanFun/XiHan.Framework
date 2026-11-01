// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;

/// <summary>
/// 只用策略保护的审批操作。
/// </summary>
/// <remarks>
/// 没有声明权限码、只要求策略 <see cref="Policy"/>：它同样需要授权，默认选择规则下要记审计。
/// </remarks>
[Operation(Id, Policy = Policy)]
internal sealed class ApproveOrders : IOperation<ListOrdersRequest, OrderResponse>
{
    /// <summary>
    /// 操作 ID。
    /// </summary>
    public const string Id = "orders.approve";

    /// <summary>
    /// 要求的策略名。
    /// </summary>
    public const string Policy = "order-approver";

    /// <inheritdoc />
    public ValueTask<Result<OrderResponse>> HandleAsync(ListOrdersRequest request, InvocationContext context) => ValueTask.FromResult(Result.Success(new OrderResponse("approve")));
}
