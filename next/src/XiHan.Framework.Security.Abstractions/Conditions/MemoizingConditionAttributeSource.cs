// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 带记忆的属性来源。
/// </summary>
/// <remarks>
/// 同一属性只向内层取一次。按单次调用使用，不是线程安全的；<see cref="ConditionAttributeValueState.Deferred"/> 不记忆，补齐后再取能拿到值。
/// </remarks>
/// <param name="inner">内层来源。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class MemoizingConditionAttributeSource(IConditionAttributeSource inner) : IConditionAttributeSource
{
    /// <summary>
    /// 内层来源。
    /// </summary>
    private readonly IConditionAttributeSource _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>
    /// 已取到的值，按规范名（不区分大小写）。
    /// </summary>
    private readonly Dictionary<string, ConditionAttributeValue> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async ValueTask<ConditionAttributeValue> GetAsync(string canonicalName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalName);
        if (_values.TryGetValue(canonicalName, out var cached))
        {
            return cached;
        }

        var value = await _inner.GetAsync(canonicalName, cancellationToken).ConfigureAwait(false);
        if (value.State != ConditionAttributeValueState.Deferred)
        {
            _values[canonicalName] = value;
        }

        return value;
    }
}
