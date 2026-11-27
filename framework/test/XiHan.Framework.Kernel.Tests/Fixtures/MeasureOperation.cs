// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 把请求原样作为响应交回的操作
/// </summary>
internal sealed class MeasureOperation : IOperation<MeasureRequest, MeasureRequest>
{
    /// <summary>
    /// 操作 ID
    /// </summary>
    public const string Id = "measures.echo";

    /// <inheritdoc />
    public ValueTask<Result<MeasureRequest>> HandleAsync(MeasureRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(request));
}
