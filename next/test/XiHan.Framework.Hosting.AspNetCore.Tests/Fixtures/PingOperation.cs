// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Tests.Fixtures;

/// <summary>
/// 问候操作：名字为空返回校验失败，否则回显问候与上下文里的操作 ID。
/// </summary>
internal sealed class PingOperation : IOperation<PingRequest, PingResponse>
{
    /// <summary>
    /// 操作 ID。
    /// </summary>
    public const string Id = "pings.create";

    /// <inheritdoc />
    public ValueTask<Result<PingResponse>> HandleAsync(PingRequest request, InvocationContext context)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            return ValueTask.FromResult<Result<PingResponse>>(Error.Validation("PINGS_VALIDATION_001", "pings.name.required"));
        }

        return ValueTask.FromResult(Result.Success(new PingResponse($"Hello, {request.Name}!", context.GetOperation().Map(static d => d.Id).ValueOr("-"))));
    }
}
