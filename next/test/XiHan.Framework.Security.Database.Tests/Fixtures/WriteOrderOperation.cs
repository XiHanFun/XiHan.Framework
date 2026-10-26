// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Database.Tests.Fixtures;

/// <summary>
/// 要求权限 <c>orders.write</c> 的操作：到达处理器即返回当前主体与租户，授权在 Authorize 阶段按契约判定。
/// </summary>
internal sealed class WriteOrderOperation : IOperation<OrderRequest, OrderResponse>
{
    /// <summary>
    /// 操作 ID。
    /// </summary>
    public const string Id = "orders.write";

    /// <summary>
    /// 契约要求的权限码。
    /// </summary>
    public const string Permission = "orders.write";

    /// <summary>
    /// 运行期契约：权限 <see cref="Permission"/>。
    /// </summary>
    public static OperationContract Contract { get; } = new(Id, permission: Permission);

    /// <inheritdoc />
    public ValueTask<Result<OrderResponse>> HandleAsync(OrderRequest request, InvocationContext context)
    {
        var security = context.GetSecurity();
        return ValueTask.FromResult(Result.Success(new OrderResponse(security.Principal.Id, security.Tenant?.Id)));
    }
}
