// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;

/// <summary>
/// 未声明权限的列表操作。
/// </summary>
[Operation(Id)]
internal sealed class ListOrders : IOperation<ListOrdersRequest, OrderResponse>
{
    /// <summary>
    /// 操作 ID。
    /// </summary>
    public const string Id = "orders.list";

    /// <inheritdoc />
    public ValueTask<Result<OrderResponse>> HandleAsync(ListOrdersRequest request, InvocationContext context) => ValueTask.FromResult(Result.Success(new OrderResponse("list")));
}
