// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 筛选操作的测试实现：把绑定到的类型化字段原样回显。
/// </summary>
internal sealed class OrderFilterOperation : IOperation<OrderFilterRequest, OrderFilterRequest>
{
    /// <inheritdoc />
    public ValueTask<Result<OrderFilterRequest>> HandleAsync(OrderFilterRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(request));
}
