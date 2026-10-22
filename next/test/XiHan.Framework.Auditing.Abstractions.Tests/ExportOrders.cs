// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Tests;

/// <summary>
/// 未声明权限但显式声明审计动作的导出操作。
/// </summary>
[Operation(Id)]
internal sealed class ExportOrders : IOperation<ListOrdersRequest, OrderResponse>
{
    /// <summary>
    /// 操作 ID。
    /// </summary>
    public const string Id = "orders.export";

    /// <summary>
    /// 显式声明的审计动作。
    /// </summary>
    public const string AuditAction = "orders.export-report";

    /// <inheritdoc />
    public ValueTask<Result<OrderResponse>> HandleAsync(ListOrdersRequest request, InvocationContext context)
    {
        context.SetAuditAction(AuditAction);
        return ValueTask.FromResult(Result.Success(new OrderResponse("export")));
    }
}
