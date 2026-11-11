// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// 回答"我是谁"的操作：回显执行它的主体 ID 与安全上下文里的租户
/// </summary>
internal sealed class WhoAmIOperation : IOperation<EchoRequest, EchoResponse>
{
    /// <inheritdoc />
    public ValueTask<Result<EchoResponse>> HandleAsync(EchoRequest request, InvocationContext context)
    {
        var security = context.GetSecurity();
        return ValueTask.FromResult(Result.Success(new EchoResponse(security.Principal.Id ?? "-", context.TraceId, security.Tenant?.Id)));
    }
}
