// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Validation;

/// <summary>
/// 按操作契约的请求 JSON Schema校验载荷。
/// </summary>
/// <remarks>
/// draft 2020-12 的子集，即 <c>Kernel.Generators</c> 产出的关键字。<c>type</c>（含 <c>[type, "null"]</c>）、<c>properties</c>、<c>required</c>、<c>additionalProperties</c>、<c>items</c>、<c>enum</c>、<c>const</c>、<c>anyOf</c> / <c>oneOf</c> / <c>allOf</c>、<c>$ref</c>（<c>#/$defs/…</c>）、<c>minimum</c> / <c>maximum</c> / <c>exclusiveMinimum</c> / <c>exclusiveMaximum</c>、<c>minLength</c> / <c>maxLength</c>、<c>pattern</c>、<c>minItems</c> / <c>maxItems</c>。其他关键字（<c>format</c>、<c>contentEncoding</c> 等）不断言。零反射，可 AOT。每条违规带英文原因与 <c>kernel.validation.*</c> 消息键及参数，宿主按调用方区域翻译成字段级错误。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class JsonSchemaValidator
{
    /// <summary>
    /// 一次校验最多记录的违规数。
    /// </summary>
    /// <remarks>
    /// 超过即停止，避免恶意载荷制造海量诊断。
    /// </remarks>
    public const int MaxViolations = 32;

    /// <summary>
    /// 嵌套深度上限（Schema 递归与载荷嵌套都受它约束）。
    /// </summary>
    public const int MaxDepth = 64;

    /// <summary>
    /// <c>pattern</c> 的正则缓存容量。
    /// </summary>
    /// <remarks>
    /// 超过后新模式每次现编译，不再缓存。
    /// </remarks>
    private const int PatternCacheCapacity = 256;

    /// <summary>
    /// 单次正则匹配的超时。
    /// </summary>
    /// <remarks>
    /// 模式来自应用的注解、输入来自调用方，超时是对灾难性回溯的保险。
    /// </remarks>
    private static readonly TimeSpan _matchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// <c>$ref</c> 的本地前缀。
    /// </summary>
    private const string DefinitionsPrefix = "#/$defs/";

    /// <summary>
    /// 已编译的 <c>pattern</c> 正则，按模式文本缓存。
    /// </summary>
    private static readonly ConcurrentDictionary<string, Regex> _patterns = new(StringComparer.Ordinal);

    /// <summary>
    /// 校验载荷。
    /// </summary>
    /// <remarks>
    /// 返回违规列表，空表示通过。<paramref name="schema"/> 是 Schema 文档的根对象（<c>$ref</c> 相对它解析）。
    /// </remarks>
    public static IReadOnlyList<SchemaViolation> Validate(JsonElement schema, JsonElement instance)
    {
        var violations = new List<SchemaViolation>();
        Visit(schema, schema, instance, "$", 0, violations);
        return violations;
    }

    /// <summary>
    /// 校验一个节点。
    /// </summary>
    /// <remarks>
    /// 先解析 <c>$ref</c>，再依次看类型、组合、枚举与各类约束；每个关键字只在实例类型匹配时生效（与 JSON Schema 语义一致）。
    /// </remarks>
    private static void Visit(JsonElement root, JsonElement schema, JsonElement instance, string path, int depth, List<SchemaViolation> violations)
    {
        // 违规数与深度都有上限：超限即停止，既不抛异常也不无限递归
        if (violations.Count >= MaxViolations)
        {
            return;
        }

        if (depth > MaxDepth)
        {
            violations.Add(Violation(path, KernelMessages.ValidationDepth, $"nesting deeper than {MaxDepth} levels is not accepted", ("max", MaxDepth)));
            return;
        }

        // 布尔 Schema：true 全部通过，false 全部拒绝
        if (schema.ValueKind == JsonValueKind.True)
        {
            return;
        }

        if (schema.ValueKind == JsonValueKind.False)
        {
            violations.Add(Violation(path, KernelMessages.ValidationNotAllowed, "no value is allowed here"));
            return;
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            if (!TryResolve(root, reference, out var target))
            {
                var text = reference.GetString();
                violations.Add(Violation(path, KernelMessages.ValidationUnresolvedReference, $"schema reference '{text}' cannot be resolved", ("reference", text)));
                return;
            }

            Visit(root, target, instance, path, depth + 1, violations);
        }

        if (schema.TryGetProperty("type", out var type) && !MatchesType(type, instance))
        {
            var expected = Describe(type);
            var actual = Kind(instance);
            violations.Add(Violation(path, KernelMessages.ValidationType, $"expected type {expected} but found {actual}", ("expected", expected), ("actual", actual)));
            // 类型不符时其余约束没有意义，避免同一位置堆叠多条
            return;
        }

        VisitComposition(root, schema, instance, path, depth, violations);
        VisitValue(schema, instance, path, violations);

        if (instance.ValueKind == JsonValueKind.Object)
        {
            VisitObject(root, schema, instance, path, depth, violations);
        }
        else if (instance.ValueKind == JsonValueKind.Array)
        {
            VisitArray(root, schema, instance, path, depth, violations);
        }
    }

    /// <summary>
    /// <c>allOf</c> 全部满足。
    /// </summary>
    /// <remarks>
    /// <c>anyOf</c> 至少一个；<c>oneOf</c> 恰好一个。候选的违规不逐条列出，只报组合本身。
    /// </remarks>
    private static void VisitComposition(JsonElement root, JsonElement schema, JsonElement instance, string path, int depth, List<SchemaViolation> violations)
    {
        if (schema.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in allOf.EnumerateArray())
            {
                Visit(root, candidate, instance, path, depth + 1, violations);
            }
        }

        if (schema.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array && CountMatching(root, anyOf, instance, depth) == 0)
        {
            violations.Add(Violation(path, KernelMessages.ValidationAnyOf, "value matches none of the allowed schemas"));
        }

        if (schema.TryGetProperty("oneOf", out var oneOf) && oneOf.ValueKind == JsonValueKind.Array && CountMatching(root, oneOf, instance, depth) != 1)
        {
            violations.Add(Violation(path, KernelMessages.ValidationOneOf, "value must match exactly one of the allowed schemas"));
        }
    }

    /// <summary>
    /// 候选 Schema 里有几个接受该实例。
    /// </summary>
    private static int CountMatching(JsonElement root, JsonElement candidates, JsonElement instance, int depth)
    {
        var matching = 0;
        foreach (var candidate in candidates.EnumerateArray())
        {
            var scratch = new List<SchemaViolation>();
            Visit(root, candidate, instance, "$", depth + 1, scratch);
            if (scratch.Count == 0)
            {
                matching++;
            }
        }

        return matching;
    }

    /// <summary>
    /// 标量约束：<c>enum</c> / <c>const</c>、数值范围、字符串长度与模式。
    /// </summary>
    private static void VisitValue(JsonElement schema, JsonElement instance, string path, List<SchemaViolation> violations)
    {
        if (schema.TryGetProperty("enum", out var allowed) && allowed.ValueKind == JsonValueKind.Array && !allowed.EnumerateArray().Any(candidate => JsonElement.DeepEquals(candidate, instance)))
        {
            violations.Add(Violation(path, KernelMessages.ValidationEnum, "value is not one of the allowed values"));
        }

        if (schema.TryGetProperty("const", out var constant) && !JsonElement.DeepEquals(constant, instance))
        {
            var expected = constant.GetRawText();
            violations.Add(Violation(path, KernelMessages.ValidationConst, $"value must be {expected}", ("expected", expected)));
        }

        if (instance.ValueKind == JsonValueKind.Number)
        {
            VisitNumber(schema, instance, path, violations);
        }
        else if (instance.ValueKind == JsonValueKind.String)
        {
            VisitString(schema, instance, path, violations);
        }
    }

    /// <summary>
    /// 数值范围。
    /// </summary>
    /// <remarks>
    /// Schema 与实例都按 decimal 比较，超出 decimal 的按 double。
    /// </remarks>
    private static void VisitNumber(JsonElement schema, JsonElement instance, string path, List<SchemaViolation> violations)
    {
        if (Bound(schema, "minimum") is { } minimum && Compare(instance, minimum) < 0)
        {
            violations.Add(Limit(path, KernelMessages.ValidationMinimum, "value must be >= ", minimum.GetRawText()));
        }

        if (Bound(schema, "maximum") is { } maximum && Compare(instance, maximum) > 0)
        {
            violations.Add(Limit(path, KernelMessages.ValidationMaximum, "value must be <= ", maximum.GetRawText()));
        }

        if (Bound(schema, "exclusiveMinimum") is { } exclusiveMinimum && Compare(instance, exclusiveMinimum) <= 0)
        {
            violations.Add(Limit(path, KernelMessages.ValidationExclusiveMinimum, "value must be > ", exclusiveMinimum.GetRawText()));
        }

        if (Bound(schema, "exclusiveMaximum") is { } exclusiveMaximum && Compare(instance, exclusiveMaximum) >= 0)
        {
            violations.Add(Limit(path, KernelMessages.ValidationExclusiveMaximum, "value must be < ", exclusiveMaximum.GetRawText()));
        }
    }

    /// <summary>
    /// 字符串长度（按 Unicode 标量计）与 <c>pattern</c>（ECMA 风格、不锚定）。
    /// </summary>
    private static void VisitString(JsonElement schema, JsonElement instance, string path, List<SchemaViolation> violations)
    {
        var text = instance.GetString() ?? string.Empty;
        var length = Length(text);
        if (Count(schema, "minLength") is { } minLength && length < minLength)
        {
            violations.Add(Limit(path, KernelMessages.ValidationMinLength, "length must be >= ", minLength));
        }

        if (Count(schema, "maxLength") is { } maxLength && length > maxLength)
        {
            violations.Add(Limit(path, KernelMessages.ValidationMaxLength, "length must be <= ", maxLength));
        }

        if (schema.TryGetProperty("pattern", out var pattern) && pattern.ValueKind == JsonValueKind.String && pattern.GetString() is { Length: > 0 } expression)
        {
            try
            {
                if (!PatternFor(expression).IsMatch(text))
                {
                    violations.Add(Violation(path, KernelMessages.ValidationPattern, $"value does not match pattern '{expression}'", ("pattern", expression)));
                }
            }
            catch (RegexMatchTimeoutException)
            {
                violations.Add(Violation(path, KernelMessages.ValidationPatternTimeout, $"matching pattern '{expression}' timed out", ("pattern", expression)));
            }
            catch (ArgumentException)
            {
                violations.Add(Violation(path, KernelMessages.ValidationPatternInvalid, $"pattern '{expression}' is not a valid regular expression", ("pattern", expression)));
            }
        }
    }

    /// <summary>
    /// 对象。
    /// </summary>
    /// <remarks>
    /// <c>required</c>（违规挂在缺失属性自己的路径上，让字段级错误能落到字段）、<c>properties</c> 逐个下钻、<c>additionalProperties</c>（<see langword="false"/> 拒绝未声明的属性，Schema 则按它校验）。
    /// </remarks>
    private static void VisitObject(JsonElement root, JsonElement schema, JsonElement instance, string path, int depth, List<SchemaViolation> violations)
    {
        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var name in required.EnumerateArray())
            {
                if (name.ValueKind == JsonValueKind.String && name.GetString() is { } member && !instance.TryGetProperty(member, out _))
                {
                    violations.Add(Violation(Child(path, member), KernelMessages.ValidationRequired, $"required property '{member}' is missing", ("name", member)));
                }
            }
        }

        var hasProperties = schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object;
        var hasAdditional = schema.TryGetProperty("additionalProperties", out var additional);
        foreach (var member in instance.EnumerateObject())
        {
            var memberPath = Child(path, member.Name);
            if (hasProperties && properties.TryGetProperty(member.Name, out var propertySchema))
            {
                Visit(root, propertySchema, member.Value, memberPath, depth + 1, violations);
            }
            else if (hasAdditional)
            {
                if (additional.ValueKind == JsonValueKind.False)
                {
                    violations.Add(Violation(memberPath, KernelMessages.ValidationUndeclared, "property is not declared by the contract"));
                }
                else
                {
                    Visit(root, additional, member.Value, memberPath, depth + 1, violations);
                }
            }
        }
    }

    /// <summary>
    /// 数组：元素数范围与 <c>items</c> 逐元素下钻。
    /// </summary>
    private static void VisitArray(JsonElement root, JsonElement schema, JsonElement instance, string path, int depth, List<SchemaViolation> violations)
    {
        var count = instance.GetArrayLength();
        if (Count(schema, "minItems") is { } minItems && count < minItems)
        {
            violations.Add(Violation(path, KernelMessages.ValidationMinItems, $"must have at least {minItems} items", ("limit", minItems)));
        }

        if (Count(schema, "maxItems") is { } maxItems && count > maxItems)
        {
            violations.Add(Violation(path, KernelMessages.ValidationMaxItems, $"must have at most {maxItems} items", ("limit", maxItems)));
        }

        if (schema.TryGetProperty("items", out var items))
        {
            var index = 0;
            foreach (var element in instance.EnumerateArray())
            {
                Visit(root, items, element, $"{path}[{index++}]", depth + 1, violations);
            }
        }
    }

    /// <summary>
    /// 构造带消息键与参数的违规。
    /// </summary>
    private static SchemaViolation Violation(string path, string messageKey, string reason, params ReadOnlySpan<(string Name, object? Value)> arguments)
        => new(path, reason, messageKey, arguments.Length == 0 ? null : MessageArguments.Of(arguments));

    /// <summary>
    /// 带单个 <c>limit</c> 参数的违规：原因 = 前缀 + 界限。
    /// </summary>
    private static SchemaViolation Limit(string path, string messageKey, string prefix, object limit)
        => new(path, prefix + limit, messageKey, MessageArguments.Of(("limit", limit)));

    /// <summary>
    /// 解析本地 <c>$ref</c>（只支持 <c>#/$defs/名字</c>）。
    /// </summary>
    private static bool TryResolve(JsonElement root, JsonElement reference, out JsonElement target)
    {
        target = default;
        var text = reference.ValueKind == JsonValueKind.String ? reference.GetString() : null;
        return text is not null
            && text.StartsWith(DefinitionsPrefix, StringComparison.Ordinal)
            && root.TryGetProperty("$defs", out var definitions)
            && definitions.ValueKind == JsonValueKind.Object
            && definitions.TryGetProperty(text[DefinitionsPrefix.Length..], out target);
    }

    /// <summary>
    /// 实例是否满足 <c>type</c>（单个名字或名字数组）。
    /// </summary>
    private static bool MatchesType(JsonElement type, JsonElement instance)
    {
        if (type.ValueKind == JsonValueKind.String)
        {
            return MatchesType(type.GetString(), instance);
        }

        return type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(candidate => candidate.ValueKind == JsonValueKind.String && MatchesType(candidate.GetString(), instance));
    }

    /// <summary>
    /// 单个类型名的判定。
    /// </summary>
    /// <remarks>
    /// <c>integer</c> 接受没有小数部分的数（<c>1.0</c> 也算）。
    /// </remarks>
    private static bool MatchesType(string? name, JsonElement instance) => name switch
    {
        "null" => instance.ValueKind == JsonValueKind.Null,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "string" => instance.ValueKind == JsonValueKind.String,
        "number" => instance.ValueKind == JsonValueKind.Number,
        "integer" => instance.ValueKind == JsonValueKind.Number && (instance.TryGetInt64(out _) || (instance.TryGetDecimal(out var value) && decimal.Truncate(value) == value)),
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        _ => false,
    };

    /// <summary>
    /// 比较实例与边界：两者都能表示为 decimal 时按 decimal，否则按 double。
    /// </summary>
    private static int Compare(JsonElement instance, JsonElement bound)
    {
        if (instance.TryGetDecimal(out var left) && bound.TryGetDecimal(out var right))
        {
            return left.CompareTo(right);
        }

        return instance.GetDouble().CompareTo(bound.GetDouble());
    }

    /// <summary>
    /// 读数值边界关键字。
    /// </summary>
    /// <remarks>
    /// 不是数字视为没有。
    /// </remarks>
    private static JsonElement? Bound(JsonElement schema, string keyword)
        => schema.TryGetProperty(keyword, out var value) && value.ValueKind == JsonValueKind.Number ? value : null;

    /// <summary>
    /// 读非负整数关键字（长度与元素数）。
    /// </summary>
    /// <remarks>
    /// 不是非负整数视为没有。
    /// </remarks>
    private static long? Count(JsonElement schema, string keyword)
        => schema.TryGetProperty(keyword, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var count) && count >= 0 ? count : null;

    /// <summary>
    /// 取（或编译并缓存）<c>pattern</c> 的正则。
    /// </summary>
    /// <remarks>
    /// 缓存满后不再加入，避免无界增长。
    /// </remarks>
    private static Regex PatternFor(string pattern)
    {
        if (_patterns.TryGetValue(pattern, out var cached))
        {
            return cached;
        }

        var regex = new Regex(pattern, RegexOptions.None, _matchTimeout);
        if (_patterns.Count < PatternCacheCapacity)
        {
            _patterns.TryAdd(pattern, regex);
        }

        return regex;
    }

    /// <summary>
    /// 按 Unicode 标量计长度（JSON Schema 的 <c>minLength</c> / <c>maxLength</c> 语义）。
    /// </summary>
    /// <remarks>
    /// 代理对算一个字符。
    /// </remarks>
    private static int Length(string text)
    {
        var length = 0;
        foreach (var _ in text.EnumerateRunes())
        {
            length++;
        }

        return length;
    }

    /// <summary>
    /// 子属性的路径：名字只含字母数字下划线时用点，否则用括号加引号。
    /// </summary>
    private static string Child(string path, string name)
        => name.All(static c => char.IsAsciiLetterOrDigit(c) || c == '_') ? $"{path}.{name}" : $"{path}['{name}']";

    /// <summary>
    /// 把 <c>type</c> 关键字描述成 <c>'integer'</c> 或 <c>'string' or 'null'</c>。
    /// </summary>
    private static string Describe(JsonElement type)
        => type.ValueKind == JsonValueKind.String
            ? $"'{type.GetString()}'"
            : string.Join(" or ", type.EnumerateArray().Select(static t => $"'{t.GetString()}'"));

    /// <summary>
    /// 实例的 JSON 种类名。
    /// </summary>
    private static string Kind(JsonElement instance) => instance.ValueKind switch
    {
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null => "null",
        _ => instance.ValueKind.ToString().ToLowerInvariant(),
    };
}
