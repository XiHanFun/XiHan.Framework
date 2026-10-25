// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.OpenAI.Tests;

/// <summary>
/// 回显操作：把文本重复 <c>Repeat</c> 次。
/// </summary>
internal sealed class EchoOperation : IOperation<EchoRequest, EchoResponse>
{
    /// <inheritdoc />
    public ValueTask<Result<EchoResponse>> HandleAsync(EchoRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(new EchoResponse(string.Concat(Enumerable.Repeat(request.Text, Math.Max(1, request.Repeat))))));
}
