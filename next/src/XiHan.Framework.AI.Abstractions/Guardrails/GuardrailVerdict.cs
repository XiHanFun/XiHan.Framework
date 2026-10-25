// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Guardrails;

/// <summary>
/// 护栏判定：放行，或带原因拦截。原因是消息键（<c>AIMessages.*</c> 或应用自己的键）加参数，宿主在出口翻译。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record GuardrailVerdict
{
    /// <summary>
    /// 创建。
    /// </summary>
    private GuardrailVerdict(bool blocked, string? reasonKey, IReadOnlyDictionary<string, object?>? reasonArguments)
    {
        Blocked = blocked;
        ReasonKey = reasonKey;
        ReasonArguments = reasonArguments;
    }

    /// <summary>
    /// 是否拦截。
    /// </summary>
    public bool Blocked { get; }

    /// <summary>
    /// 拦截原因的消息键。
    /// </summary>
    public string? ReasonKey { get; }

    /// <summary>
    /// 拦截原因的参数。
    /// </summary>
    public IReadOnlyDictionary<string, object?>? ReasonArguments { get; }

    /// <summary>
    /// 放行。
    /// </summary>
    public static GuardrailVerdict Allow() => new(false, null, null);

    /// <summary>
    /// 拦截。
    /// </summary>
    public static GuardrailVerdict Block(string reasonKey, IReadOnlyDictionary<string, object?>? reasonArguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonKey);
        return new GuardrailVerdict(true, reasonKey, reasonArguments);
    }

    /// <summary>
    /// 原因的本地化文案（按当前区域）。
    /// </summary>
    public string Reason => ReasonKey is null ? string.Empty : ReasonKey.L(ReasonArguments);
}
