// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 载荷路径向量的 order 请求
/// </summary>
/// <remarks>
/// 只用来解码载荷，不会被执行
/// </remarks>
internal sealed class VectorOrderOperation : IOperation<VectorOrder, VectorOrder>
{
    /// <summary>
    /// 运行期契约
    /// </summary>
    public static OperationContract Contract { get; } = new("vector-orders.create");

    /// <inheritdoc />
    public ValueTask<Result<VectorOrder>> HandleAsync(VectorOrder request, InvocationContext context)
        => throw new NotSupportedException("The vector operation only decodes payloads.");
}
