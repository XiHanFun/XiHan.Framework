// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 载荷路径向量的 counts 请求：成员值都是整数的对象
/// </summary>
/// <remarks>
/// 只用来解码载荷，不会被执行
/// </remarks>
internal sealed class VectorCountsOperation : IOperation<Dictionary<string, int>, VectorOrder>
{
    /// <summary>
    /// 运行期契约
    /// </summary>
    public static OperationContract Contract { get; } = new("vector-counts.create");

    /// <inheritdoc />
    public ValueTask<Result<VectorOrder>> HandleAsync(Dictionary<string, int> request, InvocationContext context)
        => throw new NotSupportedException("The vector operation only decodes payloads.");
}
