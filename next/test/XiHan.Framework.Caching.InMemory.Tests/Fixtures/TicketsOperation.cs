// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory.Tests.Fixtures;

/// <summary>
/// 取号操作。
/// </summary>
/// <remarks>
/// 每执行一次号码加一；取号人为 <c>fail</c> 时计一次执行后返回冲突。同一实现登记在 <c>tickets.create</c> 与 <c>tickets.update</c> 下。
/// </remarks>
internal sealed class TicketsOperation(TicketCounter counter) : IOperation<TicketRequest, TicketReply>
{
    /// <inheritdoc />
    public ValueTask<Result<TicketReply>> HandleAsync(TicketRequest request, InvocationContext context)
    {
        var number = counter.Next();
        return request.Name == "fail"
            ? ValueTask.FromResult<Result<TicketReply>>(Error.Conflict("TICKETS_CONFLICT_001", "tickets.ticket.rejected"))
            : ValueTask.FromResult(Result.Success(new TicketReply(number, request.Name)));
    }
}
