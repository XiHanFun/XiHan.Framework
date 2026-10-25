// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 提示词目录的实现（单例）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class AIPromptCatalog : IAIPromptCatalog
{
    /// <summary>
    /// 名字 → 模板。
    /// </summary>
    private readonly SortedDictionary<string, AIPromptTemplate> _templates = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建：先放配置里的，再用显式登记覆盖。
    /// </summary>
    public AIPromptCatalog(AIOptions options, IEnumerable<AIPromptTemplate> registered)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(registered);
        foreach (var template in options.Prompts)
        {
            _templates[template.Name] = template;
        }

        foreach (var template in registered)
        {
            _templates[template.Name] = template;
        }
    }

    /// <inheritdoc />
    public Maybe<AIPromptTemplate> Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _templates.TryGetValue(name, out var template) ? Maybe.Some(template) : Maybe.None<AIPromptTemplate>();
    }

    /// <inheritdoc />
    public IReadOnlyList<AIPromptTemplate> All() => _templates.Values.ToList();
}
