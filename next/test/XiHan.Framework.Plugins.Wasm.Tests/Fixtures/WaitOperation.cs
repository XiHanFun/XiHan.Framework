// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 一直等到被取消的宿主侧操作（最多 30 秒）。
/// </summary>
internal sealed class WaitOperation(CancellationProbe probe) : IOperation<PingRequest, PingResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<PingResponse>> HandleAsync(PingRequest request, InvocationContext context)
    {
        await probe.WaitAsync(TimeSpan.FromSeconds(30), context.CancellationToken).ConfigureAwait(false);
        return Result.Success(new PingResponse(request.Name, string.Empty, string.Empty));
    }
}
