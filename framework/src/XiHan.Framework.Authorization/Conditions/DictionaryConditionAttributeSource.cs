// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 以字典提供属性值的来源（试运行、模拟上下文与测试用）
/// </summary>
/// <remarks>
/// 字典里没有的属性取不到；值为 <see cref="ConditionAttributeValue"/> 时原样返回（可表达暂不可得），否则按 <see cref="ConditionAttributeValue.Of"/> 包装。
/// </remarks>
public sealed class DictionaryConditionAttributeSource : IConditionAttributeSource
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="values">规范名到值；键不区分大小写时由调用方传入相应比较器的字典</param>
    public DictionaryConditionAttributeSource(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values;
    }

    /// <inheritdoc />
    public ValueTask<ConditionAttributeValue> GetAsync(string canonicalName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_values.TryGetValue(canonicalName, out var value))
        {
            return ValueTask.FromResult(ConditionAttributeValue.Missing);
        }

        return ValueTask.FromResult(value as ConditionAttributeValue ?? ConditionAttributeValue.Of(value));
    }
}
