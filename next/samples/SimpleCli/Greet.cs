// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace SimpleCli;

/// <summary>
/// 问候操作：一份契约同时是命令行命令与 MCP tool。
/// </summary>
[Operation(Id, Stability = Stability.Experimental, Expose = Expose.CommandLine | Expose.Mcp)]
public sealed class Greet : IOperation<GreetRequest, GreetResponse>
{
    /// <summary>
    /// 操作 ID。
    /// </summary>
    public const string Id = "greetings.create";

    /// <inheritdoc />
    public ValueTask<Result<GreetResponse>> HandleAsync(GreetRequest request, InvocationContext context)
    {
        if (request.Times is < 1 or > 10)
        {
            return ValueTask.FromResult<Result<GreetResponse>>(Error.Validation("SAMPLES_VALIDATION_001", CliMessages.TimesInvalid, MessageArguments.Of(("times", request.Times))));
        }

        var message = string.Join(" ", Enumerable.Repeat($"Hello, {request.Name}!", request.Times));
        return ValueTask.FromResult(Result.Success(new GreetResponse(message, context.TraceId)));
    }
}
