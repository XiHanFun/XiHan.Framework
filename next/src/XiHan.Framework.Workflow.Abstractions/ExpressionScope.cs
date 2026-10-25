// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 表达式求值的作用域：实例变量加临时值（<c>outcome</c>、<c>item</c> 等），以及 <c>now()</c> 用的时钟。临时值优先于同名变量。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ExpressionScope
{
    /// <summary>
    /// 变量。
    /// </summary>
    private readonly JsonObject _variables;

    /// <summary>
    /// 临时值。
    /// </summary>
    private readonly Dictionary<string, JsonNode?> _extras = new(StringComparer.Ordinal);

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 创建。
    /// </summary>
    public ExpressionScope(JsonObject variables, TimeProvider clock)
    {
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// 当前时刻（UTC）。
    /// </summary>
    public DateTimeOffset Now => _clock.GetUtcNow();

    /// <summary>
    /// 加一个临时值（覆盖同名变量）；返回自身便于链式调用。
    /// </summary>
    public ExpressionScope With(string name, JsonNode? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _extras[name] = value;
        return this;
    }

    /// <summary>
    /// 解析根名字：先临时值，再变量；都没有返回 <see langword="false"/>。
    /// </summary>
    public bool TryResolve(string name, out JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_extras.TryGetValue(name, out value))
        {
            return true;
        }

        return _variables.TryGetPropertyValue(name, out value);
    }
}
