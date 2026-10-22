// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 默认条件编译器（无状态，线程安全）
/// </summary>
public class DefaultConditionCompiler : IConditionCompiler
{
    private static readonly char[] ListSeparators = [',', '，', '\n', '\r'];
    private static readonly char[] RangeSeparators = [',', '，'];

    private readonly IConditionAttributeRegistry _registry;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="registry">属性登记表</param>
    public DefaultConditionCompiler(IConditionAttributeRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public CompiledConditionSet Compile(ConditionSet set, ConditionCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(set);
        options ??= ConditionCompileOptions.Default;

        var additional = options.AdditionalAttributes.Count > 0
            ? new DefaultConditionAttributeRegistry(options.AdditionalAttributes)
            : null;
        var errors = new List<ConditionCompileError>();
        var groups = new List<CompiledConditionGroup>();

        foreach (var group in set.Groups)
        {
            if (group.Clauses.Count == 0)
            {
                continue;
            }

            var clauses = new List<CompiledConditionClause>(group.Clauses.Count);
            foreach (var clause in group.Clauses)
            {
                var compiled = CompileClause(clause, additional, options.TimeZone);
                if (compiled.Error is not null)
                {
                    errors.Add(new ConditionCompileError(clause.Key, clause.AttributeName, compiled.Error));
                }

                clauses.Add(compiled);
            }

            groups.Add(new CompiledConditionGroup(group.Key, clauses));
        }

        return new CompiledConditionSet(groups, errors, options.TimeZone);
    }

    private CompiledConditionClause CompileClause(ConditionClause clause, IConditionAttributeRegistry? additional, TimeZoneInfo timeZone)
    {
        if (!Enum.IsDefined(clause.Operator))
        {
            return new CompiledConditionClause(clause, null, $"不支持的运算符：{(int)clause.Operator}");
        }

        if (string.IsNullOrWhiteSpace(clause.AttributeName))
        {
            return new CompiledConditionClause(clause, null, "属性名不能为空");
        }

        ConditionAttributeResolution? resolution = null;
        if (additional?.TryResolve(clause.AttributeName, out var additionalResolution) == true)
        {
            resolution = additionalResolution;
        }
        else if (_registry.TryResolve(clause.AttributeName, out var registeredResolution))
        {
            resolution = registeredResolution;
        }

        if (resolution is null)
        {
            return new CompiledConditionClause(clause, null, $"未登记的属性：{clause.AttributeName.Trim()}");
        }

        var descriptor = resolution.Descriptor;
        if (!IsOperatorAllowed(clause.Operator, descriptor))
        {
            return new CompiledConditionClause(
                clause,
                resolution,
                $"运算符 {clause.Operator} 不能用于{(descriptor.IsMultiValued ? "多值" : string.Empty)}{descriptor.Kind}属性 {resolution.CanonicalName}");
        }

        var raw = clause.Value?.Trim() ?? string.Empty;
        switch (clause.Operator)
        {
            case ConditionOperator.IsNull or ConditionOperator.IsNotNull:
                return new CompiledConditionClause(clause, resolution, null);

            case ConditionOperator.In or ConditionOperator.NotIn:
                if (!TryParseList(raw, descriptor.Kind, timeZone, out var items, out var listError))
                {
                    return new CompiledConditionClause(clause, resolution, listError);
                }

                return new CompiledConditionClause(clause, resolution, null, items: items);

            case ConditionOperator.Between:
                if (!TryParseRange(raw, descriptor.Kind, timeZone, out var range, out var rangeError))
                {
                    return new CompiledConditionClause(clause, resolution, rangeError);
                }

                return new CompiledConditionClause(clause, resolution, null, range: range);

            default:
                // 标量按属性的值种类解析；单值字符串的包含、开头、结尾在求值时按子串比较
                var kind = descriptor.Kind;
                var text = UnquoteJsonString(raw);
                if (text.Length == 0)
                {
                    return new CompiledConditionClause(clause, resolution, "条件值不能为空");
                }

                if (!ConditionValues.TryParseText(text, kind, timeZone, allowNetwork: false, out var scalar))
                {
                    return new CompiledConditionClause(clause, resolution, $"条件值不是合法的{kind}：{text}");
                }

                return new CompiledConditionClause(clause, resolution, null, scalar: scalar);
        }
    }

    private static bool IsOperatorAllowed(ConditionOperator @operator, ConditionAttributeDescriptor descriptor)
    {
        return @operator switch
        {
            ConditionOperator.Equals or ConditionOperator.NotEquals
                or ConditionOperator.In or ConditionOperator.NotIn
                or ConditionOperator.IsNull or ConditionOperator.IsNotNull => true,
            ConditionOperator.GreaterThan or ConditionOperator.GreaterThanOrEquals
                or ConditionOperator.LessThan or ConditionOperator.LessThanOrEquals
                or ConditionOperator.Between => ConditionValues.IsOrdered(descriptor.Kind),
            ConditionOperator.Contains or ConditionOperator.NotContains => descriptor.IsMultiValued || descriptor.Kind == ConditionValueKind.String,
            ConditionOperator.StartsWith or ConditionOperator.EndsWith => descriptor.Kind == ConditionValueKind.String,
            _ => false
        };
    }

    private static bool TryParseList(string raw, ConditionValueKind kind, TimeZoneInfo timeZone, out IReadOnlyList<object> items, out string? error)
    {
        items = [];
        error = null;
        if (!TryReadElements(raw, ListSeparators, out var elements, out error))
        {
            return false;
        }

        if (elements.Count == 0)
        {
            error = "集合不能为空";
            return false;
        }

        var parsed = new List<object>(elements.Count);
        foreach (var element in elements)
        {
            if (!ConditionValues.TryParseText(element, kind, timeZone, allowNetwork: kind == ConditionValueKind.IpAddress, out var value))
            {
                error = $"集合元素不是合法的{kind}：{element}";
                return false;
            }

            parsed.Add(value);
        }

        items = parsed;
        return true;
    }

    private static bool TryParseRange(string raw, ConditionValueKind kind, TimeZoneInfo timeZone, out (object Min, object Max) range, out string? error)
    {
        range = default;
        error = null;
        List<string> bounds;
        if (raw.StartsWith('{'))
        {
            if (!TryReadMinMax(raw, out bounds))
            {
                error = "区间写成 {\"min\":下限,\"max\":上限}";
                return false;
            }
        }
        else if (!TryReadElements(raw, RangeSeparators, out bounds, out error))
        {
            return false;
        }

        if (bounds.Count != 2)
        {
            error = "区间要正好两个值：下限,上限";
            return false;
        }

        if (!ConditionValues.TryParseText(bounds[0], kind, timeZone, allowNetwork: false, out var min)
            || !ConditionValues.TryParseText(bounds[1], kind, timeZone, allowNetwork: false, out var max))
        {
            error = $"区间的值不是合法的{kind}：{raw}";
            return false;
        }

        // 时刻允许下限大于上限，表示跨零点（如 22:00,06:00）
        if (kind != ConditionValueKind.TimeOfDay && ConditionValues.Compare(min, max, kind) > 0)
        {
            error = $"区间下限大于上限：{raw}";
            return false;
        }

        range = (min, max);
        return true;
    }

    private static bool TryReadElements(string raw, char[] separators, out List<string> elements, out string? error)
    {
        elements = [];
        error = null;
        if (!raw.StartsWith('['))
        {
            elements = [.. raw.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                error = "集合要写成 JSON 数组或逗号分隔";
                return false;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (!TryReadScalar(element, out var text))
                {
                    error = $"集合元素只能是字符串、数字或布尔：{element.GetRawText()}";
                    return false;
                }

                elements.Add(text);
            }

            return true;
        }
        catch (JsonException exception)
        {
            error = $"集合不是合法的 JSON 数组：{exception.Message}";
            return false;
        }
    }

    private static bool TryReadMinMax(string raw, out List<string> bounds)
    {
        bounds = [];
        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            string? min = null;
            string? max = null;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "min", StringComparison.OrdinalIgnoreCase) && TryReadScalar(property.Value, out var minText))
                {
                    min = minText;
                }
                else if (string.Equals(property.Name, "max", StringComparison.OrdinalIgnoreCase) && TryReadScalar(property.Value, out var maxText))
                {
                    max = maxText;
                }
            }

            if (min is null || max is null)
            {
                return false;
            }

            bounds = [min, max];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadScalar(JsonElement element, out string text)
    {
        text = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            _ => string.Empty
        };
        return element.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;
    }

    private static string UnquoteJsonString(string raw)
    {
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            try
            {
                return (JsonSerializer.Deserialize<string>(raw) ?? string.Empty).Trim();
            }
            catch (JsonException)
            {
                return raw;
            }
        }

        return raw;
    }
}
