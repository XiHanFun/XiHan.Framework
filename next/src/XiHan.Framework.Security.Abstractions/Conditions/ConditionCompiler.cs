// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件编译器（无状态，线程安全）。
/// </summary>
/// <remarks>
/// 编译是保存前的唯一关卡：属性要登记、运算符要配得上值种类、条件值要解析得了；错误一律是 <see cref="SecurityErrors.ConditionInvalid"/>，消息键说明具体原因。值语法：集合写 JSON 数组或以逗号（含全角逗号）、换行分隔，区间写 <c>a,b</c>、<c>[a,b]</c> 或 <c>{"min":a,"max":b}</c>，标量可写成 JSON 字符串（去掉引号）。
/// </remarks>
/// <param name="registry">属性登记表。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ConditionCompiler(IConditionAttributeRegistry registry) : IConditionCompiler
{
    /// <summary>
    /// 集合的分隔符：逗号、全角逗号与换行。
    /// </summary>
    private static readonly char[] _listSeparators = [',', '，', '\n', '\r'];

    /// <summary>
    /// 区间的分隔符：逗号与全角逗号。
    /// </summary>
    private static readonly char[] _rangeSeparators = [',', '，'];

    /// <summary>
    /// 属性登记表。
    /// </summary>
    private readonly IConditionAttributeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    /// <inheritdoc />
    public CompiledConditionSet Compile(ConditionSet conditions, ConditionCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        options ??= ConditionCompileOptions.Default;

        var additional = options.AdditionalAttributes.Count > 0 ? new ConditionAttributeRegistry(options.AdditionalAttributes) : null;
        var errors = new List<ConditionCompileError>();
        var groups = new List<CompiledConditionGroup>();
        foreach (var group in conditions.Groups)
        {
            if (group.Clauses.Count == 0)
            {
                continue;
            }

            var clauses = new List<CompiledConditionClause>(group.Clauses.Count);
            foreach (var clause in group.Clauses)
            {
                var compiled = CompileClause(clause, additional, options.TimeZone);
                if (compiled.Error is { } error)
                {
                    errors.Add(new ConditionCompileError(clause.Key, clause.AttributeName, error));
                }

                clauses.Add(compiled);
            }

            groups.Add(new CompiledConditionGroup(group.Key, clauses));
        }

        return new CompiledConditionSet(groups, errors, options.TimeZone);
    }

    /// <summary>
    /// 编译一个子句。
    /// </summary>
    /// <remarks>
    /// 解析属性（额外登记项优先），检查运算符搭配，按运算符把条件值解析成标量、集合或区间。
    /// </remarks>
    private CompiledConditionClause CompileClause(ConditionClause clause, ConditionAttributeRegistry? additional, TimeZoneInfo timeZone)
    {
        if (!Enum.IsDefined(clause.Operator))
        {
            return new CompiledConditionClause(clause, null, Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionOperatorUnsupported, MessageArguments.Of(("operator", (int)clause.Operator))));
        }

        if (string.IsNullOrWhiteSpace(clause.AttributeName))
        {
            return new CompiledConditionClause(clause, null, Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionAttributeRequired));
        }

        ConditionAttributeResolution? resolution = null;
        if (additional is not null && additional.TryResolve(clause.AttributeName, out var additionalResolution))
        {
            resolution = additionalResolution;
        }
        else if (_registry.TryResolve(clause.AttributeName, out var registeredResolution))
        {
            resolution = registeredResolution;
        }

        if (resolution is null)
        {
            return new CompiledConditionClause(clause, null, Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionAttributeUnknown, MessageArguments.Of(("attribute", clause.AttributeName.Trim()))));
        }

        var descriptor = resolution.Descriptor;
        if (!ConditionOperatorRules.IsAllowed(clause.Operator, descriptor))
        {
            var arguments = MessageArguments.Of(("operator", clause.Operator), ("kind", descriptor.Kind), ("attribute", resolution.CanonicalName));
            return new CompiledConditionClause(clause, resolution, descriptor.IsMultiValued
                ? Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionOperatorMismatchMultiValued, arguments)
                : Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionOperatorMismatch, arguments));
        }

        var raw = clause.Value?.Trim() ?? string.Empty;
        switch (clause.Operator)
        {
            case ConditionOperator.IsNull or ConditionOperator.IsNotNull:
                return new CompiledConditionClause(clause, resolution, null);

            case ConditionOperator.In or ConditionOperator.NotIn:
                return TryParseList(raw, descriptor.Kind, timeZone, out var items, out var listError)
                    ? new CompiledConditionClause(clause, resolution, null, items: items)
                    : new CompiledConditionClause(clause, resolution, listError);

            case ConditionOperator.Between:
                return TryParseRange(raw, descriptor.Kind, timeZone, out var range, out var rangeError)
                    ? new CompiledConditionClause(clause, resolution, null, range: range)
                    : new CompiledConditionClause(clause, resolution, rangeError);

            default:
                // 标量按属性的值种类解析；单值字符串的包含、开头、结尾在求值时按子串比较
                var text = UnquoteJsonString(raw);
                if (text.Length == 0)
                {
                    return new CompiledConditionClause(clause, resolution, Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionValueRequired));
                }

                return ConditionValues.TryParseText(text, descriptor.Kind, timeZone, allowNetwork: false, out var scalar)
                    ? new CompiledConditionClause(clause, resolution, null, scalar: scalar)
                    : new CompiledConditionClause(clause, resolution, Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionValueInvalid, MessageArguments.Of(("kind", descriptor.Kind), ("value", text))));
        }
    }

    /// <summary>
    /// 集合：JSON 数组或分隔文本，至少一个元素，每个元素按值种类解析（IP 可写网段）。
    /// </summary>
    private static bool TryParseList(string raw, ConditionValueKind kind, TimeZoneInfo timeZone, out IReadOnlyList<object> items, out Error? error)
    {
        items = [];
        if (!TryReadElements(raw, _listSeparators, out var elements, out error))
        {
            return false;
        }

        if (elements.Count == 0)
        {
            error = Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionListEmpty);
            return false;
        }

        var parsed = new List<object>(elements.Count);
        foreach (var element in elements)
        {
            if (!ConditionValues.TryParseText(element, kind, timeZone, allowNetwork: kind == ConditionValueKind.IpAddress, out var value))
            {
                error = Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionListElementInvalid, MessageArguments.Of(("kind", kind), ("element", element)));
                return false;
            }

            parsed.Add(value);
        }

        items = parsed;
        return true;
    }

    /// <summary>
    /// 区间。
    /// </summary>
    /// <remarks>
    /// <c>{"min","max"}</c> 对象、JSON 数组或分隔文本，正好两个值；下限不得大于上限（时刻例外，表示跨零点）。
    /// </remarks>
    private static bool TryParseRange(string raw, ConditionValueKind kind, TimeZoneInfo timeZone, out (object Min, object Max) range, out Error? error)
    {
        range = default;
        error = null;
        List<string> bounds;
        if (raw.StartsWith('{'))
        {
            if (!TryReadMinMax(raw, out bounds))
            {
                error = Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionRangeObjectInvalid);
                return false;
            }
        }
        else if (!TryReadElements(raw, _rangeSeparators, out bounds, out error))
        {
            return false;
        }

        if (bounds.Count != 2)
        {
            error = Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionRangeArity);
            return false;
        }

        if (!ConditionValues.TryParseText(bounds[0], kind, timeZone, allowNetwork: false, out var min) || !ConditionValues.TryParseText(bounds[1], kind, timeZone, allowNetwork: false, out var max))
        {
            error = Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionRangeValueInvalid, MessageArguments.Of(("kind", kind), ("value", raw)));
            return false;
        }

        // 时刻允许下限大于上限，表示跨零点（如 22:00,06:00）
        if (kind != ConditionValueKind.TimeOfDay && ConditionValues.Compare(min, max, kind) > 0)
        {
            error = Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionRangeReversed, MessageArguments.Of(("value", raw)));
            return false;
        }

        range = (min, max);
        return true;
    }

    /// <summary>
    /// 读集合元素。
    /// </summary>
    /// <remarks>
    /// 以 <c>[</c> 开头按 JSON 数组读（元素只能是字符串、数字、布尔），否则按分隔符切分并去掉空项。
    /// </remarks>
    private static bool TryReadElements(string raw, char[] separators, out List<string> elements, out Error? error)
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
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (!TryReadScalar(element, out var text))
                {
                    error = Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionListElementUnsupported, MessageArguments.Of(("element", element.GetRawText())));
                    return false;
                }

                elements.Add(text);
            }

            return true;
        }
        catch (JsonException exception)
        {
            error = Error.Validation(SecurityErrors.ConditionInvalid, SecurityMessages.ConditionListJsonInvalid, MessageArguments.Of(("reason", exception.Message)));
            return false;
        }
    }

    /// <summary>
    /// 读 <c>{"min":…,"max":…}</c>（键不区分大小写，值可以是字符串、数字或布尔）。
    /// </summary>
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

    /// <summary>
    /// JSON 标量的文本。
    /// </summary>
    /// <remarks>
    /// 字符串取值，数字与布尔取原文；其他类型不认。
    /// </remarks>
    private static bool TryReadScalar(JsonElement element, out string text)
    {
        text = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            _ => string.Empty,
        };
        return element.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;
    }

    /// <summary>
    /// 写成 JSON 字符串的标量去掉引号并解开转义。
    /// </summary>
    /// <remarks>
    /// 不是合法 JSON 字符串时原样返回。
    /// </remarks>
    private static string UnquoteJsonString(string raw)
    {
        if (raw.Length < 2 || raw[0] != '"' || raw[^1] != '"')
        {
            return raw;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return (document.RootElement.GetString() ?? string.Empty).Trim();
        }
        catch (JsonException)
        {
            return raw;
        }
    }
}
