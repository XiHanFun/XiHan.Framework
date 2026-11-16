// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 返回 NaN 的操作
/// </summary>
/// <remarks>
/// 响应写不出：System.Text.Json 缺省不写非有限的浮点
/// </remarks>
internal sealed class RatioOperation : IOperation<EchoRequest, RatioResponse>
{
    /// <summary>
    /// 操作 ID
    /// </summary>
    public const string Id = "ratios.compute";

    /// <inheritdoc />
    public ValueTask<Result<RatioResponse>> HandleAsync(EchoRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(new RatioResponse(double.NaN)));
}
