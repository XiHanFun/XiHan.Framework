// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 默认条件属性登记表（不可变，线程安全）
/// </summary>
public class DefaultConditionAttributeRegistry : IConditionAttributeRegistry
{
    private readonly Dictionary<string, ConditionAttributeDescriptor> _exact = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ConditionAttributeDescriptor> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Prefix, ConditionAttributeDescriptor Descriptor)> _families = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">登记选项</param>
    /// <exception cref="InvalidOperationException">登记名为空、重名，或别名与登记名、其他别名冲突</exception>
    public DefaultConditionAttributeRegistry(IOptions<XiHanConditionAttributeOptions> options)
        : this(options.Value.Attributes)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="attributes">登记项</param>
    /// <exception cref="InvalidOperationException">登记名为空、重名，或别名与登记名、其他别名冲突</exception>
    public DefaultConditionAttributeRegistry(IEnumerable<ConditionAttributeDescriptor> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var all = attributes.ToList();
        foreach (var descriptor in all)
        {
            var name = descriptor.Name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Any(char.IsWhiteSpace))
            {
                throw new InvalidOperationException($"条件属性登记名不能为空或含空白：'{descriptor.Name}'");
            }

            if (descriptor.IsFamily)
            {
                var prefix = name[..^1];
                if (prefix.Length <= 1 || _families.Any(family => string.Equals(family.Prefix, prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"条件属性族登记无效或重复：'{name}'");
                }

                _families.Add((prefix, descriptor));
                continue;
            }

            if (!_exact.TryAdd(name, descriptor))
            {
                throw new InvalidOperationException($"条件属性重复登记：'{name}'");
            }
        }

        foreach (var descriptor in all.Where(static item => !item.IsFamily))
        {
            foreach (var alias in descriptor.Aliases ?? [])
            {
                var trimmed = alias?.Trim();
                if (string.IsNullOrEmpty(trimmed) || _exact.ContainsKey(trimmed) || !_aliases.TryAdd(trimmed, descriptor))
                {
                    throw new InvalidOperationException($"条件属性别名为空或与已有名称冲突：'{alias}'（登记名 '{descriptor.Name}'）");
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
    public bool TryResolve(string attributeName, out ConditionAttributeResolution resolution)
    {
        resolution = null!;
        var name = attributeName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (_exact.TryGetValue(name, out var exact))
        {
            resolution = new ConditionAttributeResolution(exact.Name.Trim(), exact, false);
            return true;
        }

        if (_aliases.TryGetValue(name, out var aliased))
        {
            resolution = new ConditionAttributeResolution(aliased.Name.Trim(), aliased, true);
            return true;
        }

        foreach (var (prefix, descriptor) in _families)
        {
            if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                resolution = new ConditionAttributeResolution(name, descriptor, false);
                return true;
            }
        }

        return false;
    }
}
