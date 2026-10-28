// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件属性登记表（不可变，线程安全）。容器解析时收到全部经 <see cref="ConditionServiceCollectionExtensions.AddConditionAttributes"/> 登记的项；重名、别名冲突即抛出。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ConditionAttributeRegistry : IConditionAttributeRegistry
{
    /// <summary>
    /// 精确登记名 → 登记项。
    /// </summary>
    private readonly Dictionary<string, ConditionAttributeDescriptor> _exact = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 别名 → 登记项。
    /// </summary>
    private readonly Dictionary<string, ConditionAttributeDescriptor> _aliases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 族：前缀（带末尾的点）与登记项，按前缀从长到短。
    /// </summary>
    private readonly List<(string Prefix, ConditionAttributeDescriptor Descriptor)> _families = [];

    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="attributes">登记项。</param>
    /// <exception cref="InvalidOperationException">登记名为空或含空白、重名，或别名与登记名、其他别名冲突。</exception>
    public ConditionAttributeRegistry(IEnumerable<ConditionAttributeDescriptor> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        var all = attributes.ToList();
        foreach (var descriptor in all)
        {
            ArgumentNullException.ThrowIfNull(descriptor, nameof(attributes));
            var name = descriptor.Name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Any(char.IsWhiteSpace))
            {
                throw new InvalidOperationException($"Condition attribute name '{descriptor.Name}' must not be empty or contain whitespace.");
            }

            if (descriptor.IsFamily)
            {
                var prefix = name[..^1];
                if (prefix.Length <= 1 || _families.Exists(family => string.Equals(family.Prefix, prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Condition attribute family '{name}' is invalid or registered twice.");
                }

                _families.Add((prefix, descriptor));
                continue;
            }

            if (!_exact.TryAdd(name, descriptor))
            {
                throw new InvalidOperationException($"Condition attribute '{name}' is registered twice.");
            }
        }

        foreach (var descriptor in all.Where(static item => !item.IsFamily))
        {
            foreach (var alias in descriptor.Aliases ?? [])
            {
                var trimmed = alias?.Trim();
                if (string.IsNullOrEmpty(trimmed) || _exact.ContainsKey(trimmed) || !_aliases.TryAdd(trimmed, descriptor))
                {
                    throw new InvalidOperationException($"Condition attribute alias '{alias}' of '{descriptor.Name}' is empty or collides with another name.");
                }
            }
        }

        // 族按前缀从长到短排，查找时取最长匹配
        _families.Sort(static (left, right) => right.Prefix.Length.CompareTo(left.Prefix.Length));
        Attributes = all;
    }

    /// <inheritdoc />
    public IReadOnlyList<ConditionAttributeDescriptor> Attributes { get; }

    /// <inheritdoc />
    public bool TryResolve(string attributeName, [NotNullWhen(true)] out ConditionAttributeResolution? resolution)
    {
        resolution = null;
        var name = attributeName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (_exact.TryGetValue(name, out var exact))
        {
            resolution = new ConditionAttributeResolution(exact.Name.Trim(), exact, IsAlias: false);
            return true;
        }

        if (_aliases.TryGetValue(name, out var aliased))
        {
            resolution = new ConditionAttributeResolution(aliased.Name.Trim(), aliased, IsAlias: true);
            return true;
        }

        foreach (var (prefix, descriptor) in _families)
        {
            if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                resolution = new ConditionAttributeResolution(name, descriptor, IsAlias: false);
                return true;
            }
        }

        return false;
    }
}
