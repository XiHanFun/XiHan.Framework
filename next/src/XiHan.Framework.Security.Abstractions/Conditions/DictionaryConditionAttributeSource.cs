// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 以字典提供属性值的来源（试算、模拟上下文与测试用）。
/// </summary>
/// <remarks>
/// 字典里没有的属性取不到；值为 <see cref="ConditionAttributeValue"/> 时原样返回（可表达暂不可得），否则按 <see cref="ConditionAttributeValue.Of"/> 包装。
/// </remarks>
/// <param name="values">规范名到值；键要不区分大小写时由调用方传入相应比较器的字典。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DictionaryConditionAttributeSource(IReadOnlyDictionary<string, object?> values) : IConditionAttributeSource
{
    /// <summary>
    /// 规范名到值。
    /// </summary>
    private readonly IReadOnlyDictionary<string, object?> _values = values ?? throw new ArgumentNullException(nameof(values));

    /// <inheritdoc />
    public ValueTask<ConditionAttributeValue> GetAsync(string canonicalName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_values.TryGetValue(canonicalName, out var value) ? value as ConditionAttributeValue ?? ConditionAttributeValue.Of(value) : ConditionAttributeValue.Missing);
    }
}
