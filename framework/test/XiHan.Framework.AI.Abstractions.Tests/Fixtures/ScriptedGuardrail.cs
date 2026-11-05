// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Guardrails;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// 按脚本抛出或返回失败的护栏
/// </summary>
internal sealed class ScriptedGuardrail : IAIGuardrail
{
    /// <inheritdoc />
    public string Name => "scripted";

    /// <summary>
    /// 要抛出的异常
    /// </summary>
    /// <remarks>
    /// 空表示不抛出
    /// </remarks>
    public Exception? Throw { get; set; }

    /// <summary>
    /// 要返回的失败
    /// </summary>
    /// <remarks>
    /// 空表示放行
    /// </remarks>
    public Error? Fail { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<GuardrailVerdict>> InspectAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken)
    {
        if (Throw is { } exception)
        {
            throw exception;
        }

        return new ValueTask<Result<GuardrailVerdict>>(Fail is { } error ? error : GuardrailVerdict.Allow());
    }
}
