// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 枚举载荷用例的操作
/// </summary>
/// <remarks>
/// 只用来解码载荷，不会被执行
/// </remarks>
internal sealed class ShapeOperation : IOperation<ShapeRequest, ShapeRequest>
{
    /// <summary>
    /// 运行期契约
    /// </summary>
    public static OperationContract Contract { get; } = new("shapes.create");

    /// <inheritdoc />
    public ValueTask<Result<ShapeRequest>> HandleAsync(ShapeRequest request, InvocationContext context)
        => throw new NotSupportedException("The shape operation only decodes payloads.");
}
