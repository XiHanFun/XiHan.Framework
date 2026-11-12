// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 宿主侧的操作：把这次调用的租户回显在名字里，供回调测试断言租户
/// </summary>
/// <remarks>
/// 没有租户时回显 <c>-</c>
/// </remarks>
internal sealed class TenantOperation : IOperation<PingRequest, PingResponse>
{
    /// <inheritdoc />
    public ValueTask<Result<PingResponse>> HandleAsync(PingRequest request, InvocationContext context)
    {
        var security = context.GetSecurity();
        return ValueTask.FromResult(Result.Success(new PingResponse(security.Tenant?.Id ?? "-", security.Principal.Id ?? "-", security.Principal.AuthenticationScheme, context.Deadline)));
    }
}
