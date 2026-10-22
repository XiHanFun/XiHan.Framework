// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 带记忆的属性来源：同一属性只向内层取一次
/// </summary>
/// <remarks>
/// 按单次请求使用，不是线程安全的；<see cref="ConditionAttributeValueState.Deferred"/> 不记忆，补齐后再取能拿到值。
/// </remarks>
public sealed class MemoizingConditionAttributeSource : IConditionAttributeSource
{
    private readonly IConditionAttributeSource _inner;
    private readonly Dictionary<string, ConditionAttributeValue> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="inner">内层来源</param>
    public MemoizingConditionAttributeSource(IConditionAttributeSource inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc />
    public async ValueTask<ConditionAttributeValue> GetAsync(string canonicalName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalName);
        if (_values.TryGetValue(canonicalName, out var cached))
        {
            return cached;
        }

        var value = await _inner.GetAsync(canonicalName, cancellationToken);
        if (value.State != ConditionAttributeValueState.Deferred)
        {
            _values[canonicalName] = value;
        }

        return value;
    }
}
