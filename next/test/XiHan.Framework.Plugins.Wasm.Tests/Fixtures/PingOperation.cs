// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 宿主侧的操作：把调用方主体回显出来，供插件回调测试断言身份。
/// </summary>
internal sealed class PingOperation : IOperation<PingRequest, PingResponse>
{
    /// <inheritdoc />
    public ValueTask<Result<PingResponse>> HandleAsync(PingRequest request, InvocationContext context)
    {
        var principal = context.GetSecurity().Principal;
        return ValueTask.FromResult(Result.Success(new PingResponse(request.Name, principal.Id, principal.AuthenticationScheme)));
    }
}
