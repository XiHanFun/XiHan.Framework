// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Benchmarks;

/// <summary>
/// 把请求原样作为响应交回的操作
/// </summary>
public sealed class OrderEchoOperation : IOperation<OrderSummary, OrderSummary>
{
    /// <summary>
    /// 操作 ID
    /// </summary>
    public const string Id = "orders.echo";

    /// <inheritdoc />
    public ValueTask<Result<OrderSummary>> HandleAsync(OrderSummary request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(request));
}
