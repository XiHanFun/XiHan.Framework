// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 回显操作：同一实现登记在多个操作 ID 下；名字为 <c>fail</c> 时返回冲突。
/// </summary>
internal sealed class EchoOperation : IOperation<EchoRequest, EchoReply>
{
    /// <inheritdoc />
    public ValueTask<Result<EchoReply>> HandleAsync(EchoRequest request, InvocationContext context)
    {
        context.Services.GetService<OriginLog>()?.Record(context);
        if (request.Name == "fail")
        {
            return ValueTask.FromResult<Result<EchoReply>>(Error.Conflict("ORDERS_CONFLICT_001", "orders.order.rejected"));
        }

        var operation = context.GetOperation().Map(static d => d.Id).ValueOr("-");
        var tenant = context.TryGetBaggage(BaggageKeys.Tenant, out var tenantId) ? tenantId : null;
        var correlation = context.TryGetBaggage(BaggageKeys.Correlation, out var correlationId) ? correlationId : null;
        return ValueTask.FromResult(Result.Success(new EchoReply(operation, request.Name, request.Page, request.Tags ?? [], tenant, correlation, context.Deadline is not null)));
    }
}
