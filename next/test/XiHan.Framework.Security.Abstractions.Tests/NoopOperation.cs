// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Tests;

/// <summary>
/// 什么都不做的操作，只用来给授权中间件一个带契约的描述。
/// </summary>
internal sealed class NoopOperation : IOperation<NoopRequest, NoopResponse>
{
    /// <inheritdoc />
    public ValueTask<Result<NoopResponse>> HandleAsync(NoopRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(new NoopResponse()));
}
