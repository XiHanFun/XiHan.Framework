// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 集合级自定义动词的测试实现：请求不含 <c>id</c>，回显操作 ID 与绑定到的字段。
/// </summary>
internal sealed class OrderReportOperation : IOperation<OrderReportRequest, OrderReply>
{
    /// <inheritdoc />
    public ValueTask<Result<OrderReply>> HandleAsync(OrderReportRequest request, InvocationContext context)
    {
        var operation = context.GetOperation().Map(static d => d.Id).ValueOr("-");
        return ValueTask.FromResult(Result.Success(new OrderReply(operation, null, request.Name, request.Page, [], null)));
    }
}
