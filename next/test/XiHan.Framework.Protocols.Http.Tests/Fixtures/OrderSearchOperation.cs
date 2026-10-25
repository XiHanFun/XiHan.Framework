// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 分页查询的测试实现：回显绑定到的请求。
/// </summary>
internal sealed class OrderSearchOperation : IOperation<OrderSearchRequest, OrderSearchRequest>
{
    /// <inheritdoc />
    public ValueTask<Result<OrderSearchRequest>> HandleAsync(OrderSearchRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(request));
}
