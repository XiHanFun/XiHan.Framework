// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 载荷向量的 profile 请求
/// </summary>
/// <remarks>
/// 只用来解码载荷，不会被执行
/// </remarks>
internal sealed class VectorProfileOperation : IOperation<VectorProfile, VectorOrder>
{
    /// <summary>
    /// 运行期契约
    /// </summary>
    public static OperationContract Contract { get; } = new("vector-profiles.create");

    /// <inheritdoc />
    public ValueTask<Result<VectorOrder>> HandleAsync(VectorProfile request, InvocationContext context)
        => throw new NotSupportedException("The vector operation only decodes payloads.");
}
