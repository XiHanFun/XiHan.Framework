// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory.Tests.Fixtures;

/// <summary>
/// 取号操作
/// </summary>
/// <remarks>
/// 每执行一次号码加一；取号人为 <c>fail</c> 时计一次执行后返回冲突，为 <see cref="Unencodable"/> 时计一次执行后成功、交回的名字是一个孤立的代理项（写不出）。同一实现登记在 <c>tickets.create</c> 与 <c>tickets.update</c> 下
/// </remarks>
internal sealed class TicketsOperation(TicketCounter counter) : IOperation<TicketRequest, TicketReply>
{
    /// <summary>
    /// 让操作交回写不出的响应的取号人
    /// </summary>
    public const string Unencodable = "unencodable";

    /// <inheritdoc />
    public ValueTask<Result<TicketReply>> HandleAsync(TicketRequest request, InvocationContext context)
    {
        var number = counter.Next();
        return request.Name switch
        {
            "fail" => ValueTask.FromResult<Result<TicketReply>>(Error.Conflict("TICKETS_CONFLICT_001", "tickets.ticket.rejected")),
            // 请求本身合法、处理器成功，只有响应写不出：孤立的代理项编不成 UTF-8（RFC-0001「写出与转交」）
            Unencodable => ValueTask.FromResult(Result.Success(new TicketReply(number, "\uD800"))),
            _ => ValueTask.FromResult(Result.Success(new TicketReply(number, request.Name))),
        };
    }
}
