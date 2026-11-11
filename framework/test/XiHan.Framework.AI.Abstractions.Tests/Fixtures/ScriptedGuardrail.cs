// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

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

    /// <summary>
    /// 要拦下的方向
    /// </summary>
    /// <remarks>
    /// 空表示两个方向都放行
    /// </remarks>
    public GuardrailDirection? BlockDirection { get; set; }

    /// <summary>
    /// 收到过的检查（按调用顺序）
    /// </summary>
    public List<GuardrailInspection> Inspections { get; } = [];

    /// <inheritdoc />
    public ValueTask<Result<GuardrailVerdict>> InspectAsync(GuardrailInspection inspection, CancellationToken cancellationToken)
    {
        Inspections.Add(inspection);
        if (Throw is { } exception)
        {
            throw exception;
        }

        if (Fail is { } error)
        {
            return new ValueTask<Result<GuardrailVerdict>>(error);
        }

        return new ValueTask<Result<GuardrailVerdict>>(BlockDirection == inspection.Direction ? GuardrailVerdict.Block("scripted block") : GuardrailVerdict.Allow());
    }
}
