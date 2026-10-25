// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// 回显操作：<c>fail</c> 返回校验错误，<c>throw</c> 抛异常，其余把文本重复 <c>Repeat</c> 次。
/// </summary>
internal sealed class EchoOperation : IOperation<EchoRequest, EchoResponse>
{
    /// <summary>
    /// 请求 Schema。
    /// </summary>
    public const string RequestSchema = """{"type":"object","properties":{"text":{"type":["string","null"]},"repeat":{"type":"integer"}},"required":["text"]}""";

    /// <summary>
    /// 响应 Schema。
    /// </summary>
    public const string ResponseSchema = """{"type":"object","properties":{"echo":{"type":"string"},"traceId":{"type":"string"},"tenant":{"type":["string","null"]}}}""";

    /// <inheritdoc />
    public ValueTask<Result<EchoResponse>> HandleAsync(EchoRequest request, InvocationContext context)
    {
        if (request.Text == "fail")
        {
            return ValueTask.FromResult<Result<EchoResponse>>(Error.Validation("ECHOES_VALIDATION_001", "echoes.text.invalid"));
        }

        if (request.Text == "throw")
        {
            throw new InvalidOperationException("The echo operation crashed.");
        }

        context.TryGetBaggage(BaggageKeys.Tenant, out var tenant);
        return ValueTask.FromResult(Result.Success(new EchoResponse(string.Concat(Enumerable.Repeat(request.Text ?? string.Empty, Math.Max(1, request.Repeat))), context.TraceId, tenant)));
    }
}
