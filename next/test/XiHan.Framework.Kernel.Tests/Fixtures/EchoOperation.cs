// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 回声操作：空文本返回校验失败，<c>throw</c> 抛异常，其余把文本重复后连同 trace-id 返回。
/// </summary>
[Operation(Id, Expose = Expose.Http | Expose.Mcp, Permission = "echoes.write")]
internal sealed class EchoOperation : IOperation<EchoRequest, EchoResponse>
{
    /// <summary>
    /// 操作 ID。
    /// </summary>
    public const string Id = "echoes.create";

    /// <summary>
    /// 运行期契约，与特性上的声明一致。
    /// </summary>
    public static OperationContract Contract { get; } = new(Id, Stability.Stable, Expose.Http | Expose.Mcp, permission: "echoes.write");

    /// <inheritdoc />
    public ValueTask<Result<EchoResponse>> HandleAsync(EchoRequest request, InvocationContext context)
    {
        if (string.IsNullOrEmpty(request.Text))
        {
            return ValueTask.FromResult<Result<EchoResponse>>(Error.Validation("ECHOES_VALIDATION_001", "echoes.text.required"));
        }

        if (request.Text == "throw")
        {
            throw new InvalidOperationException("The echo operation crashed.");
        }

        return ValueTask.FromResult(Result.Success(new EchoResponse(string.Concat(Enumerable.Repeat(request.Text, request.Repeat)), context.TraceId)));
    }
}
