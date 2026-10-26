// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Kernel.Generators.Json;

/// <summary>
/// 一个属性上折进 Schema 的 DataAnnotations：从属性本身与位置记录的同名主构造参数两处收集（位置记录的注解默认落在参数上）。
/// </summary>
internal sealed class PropertyConstraints
{
    /// <summary>
    /// DataAnnotations 的命名空间前缀。
    /// </summary>
    private const string Namespace = "System.ComponentModel.DataAnnotations.";

    /// <summary>
    /// 是否 <c>[Required]</c>：进 <c>required</c> 且不允许 null。
    /// </summary>
    public bool Required { get; private set; }

    /// <summary>
    /// <c>[Required(AllowEmptyStrings = true)]</c>：字符串不再要求非空。
    /// </summary>
    private bool _allowEmptyStrings;

    /// <summary>
    /// 下界的 JSON 数值文本（来自 <c>[Range]</c>）。
    /// </summary>
    private string? _minimum;

    /// <summary>
    /// 上界的 JSON 数值文本（来自 <c>[Range]</c>）。
    /// </summary>
    private string? _maximum;

    /// <summary>
    /// 下界是否开区间。
    /// </summary>
    private bool _minimumExclusive;

    /// <summary>
    /// 上界是否开区间。
    /// </summary>
    private bool _maximumExclusive;

    /// <summary>
    /// 最小长度 / 元素数。
    /// </summary>
    private long? _minLength;

    /// <summary>
    /// 最大长度 / 元素数。
    /// </summary>
    private long? _maxLength;

    /// <summary>
    /// 正则模式（来自 <c>[RegularExpression]</c>）。
    /// </summary>
    private string? _pattern;

    /// <summary>
    /// 收集属性与同名主构造参数上的注解。
    /// </summary>
    public static PropertyConstraints Collect(INamedTypeSymbol type, IPropertySymbol property)
    {
        var constraints = new PropertyConstraints();
        foreach (var attribute in property.GetAttributes())
        {
            constraints.Apply(attribute);
        }

        // 位置记录 record R([Range(1, 9)] int Amount)：注解在参数上，属性是编译器合成的
        var parameter = type.Constructors
            .Where(static c => c.DeclaredAccessibility == Accessibility.Public)
            .SelectMany(static c => c.Parameters)
            .FirstOrDefault(p => string.Equals(p.Name, property.Name, StringComparison.OrdinalIgnoreCase));
        if (parameter is not null)
        {
            foreach (var attribute in parameter.GetAttributes())
            {
                constraints.Apply(attribute);
            }
        }

        return constraints;
    }

    /// <summary>
    /// 把约束关键字并进属性的 Schema 片段（一个 JSON 对象文本）；字符串用长度与模式，数组与集合用元素数。
    /// </summary>
    public string ApplyTo(string fragment, ITypeSymbol propertyType)
    {
        var isString = propertyType.SpecialType == SpecialType.System_String;
        var isCollection = !isString && (propertyType is IArrayTypeSymbol || (propertyType is INamedTypeSymbol named && IsEnumerable(named)));
        var keywords = new JsonText().StartObject();
        if (_minimum is not null)
        {
            keywords.Raw(_minimumExclusive ? "exclusiveMinimum" : "minimum", _minimum);
        }

        if (_maximum is not null)
        {
            keywords.Raw(_maximumExclusive ? "exclusiveMaximum" : "maximum", _maximum);
        }

        // [Required] 的字符串默认不接受空串；注解自己给了更大的最小长度就以注解为准
        var minLength = Required && isString && !_allowEmptyStrings ? Math.Max(_minLength ?? 0, 1) : _minLength;
        if (isString || isCollection)
        {
            if (minLength is { } min)
            {
                keywords.Number(isString ? "minLength" : "minItems", min);
            }

            if (_maxLength is { } max)
            {
                keywords.Number(isString ? "maxLength" : "maxItems", max);
            }
        }

        if (isString && _pattern is not null)
        {
            keywords.String("pattern", _pattern);
        }

        var inner = keywords.EndObject().ToString();
        inner = inner.Substring(1, inner.Length - 2);
        if (inner.Length == 0)
        {
            return fragment;
        }

        // 片段是 "{...}"：去掉右花括号后拼接；空片段（任意 JSON）不需要逗号
        var body = fragment.Substring(0, fragment.Length - 1);
        return body.Length > 1 ? body + "," + inner + "}" : body + inner + "}";
    }

    /// <summary>
    /// 按注解类型记录约束；不认识的注解忽略。
    /// </summary>
    private void Apply(AttributeData attribute)
    {
        var name = attribute.AttributeClass?.ToDisplayString();
        if (name is null || !name.StartsWith(Namespace, StringComparison.Ordinal))
        {
            return;
        }

        var arguments = attribute.ConstructorArguments;
        switch (name.Substring(Namespace.Length))
        {
            case "RequiredAttribute":
                Required = true;
                _allowEmptyStrings = Named(attribute, "AllowEmptyStrings") is true;
                break;
            case "RangeAttribute" when arguments.Length == 2 && Number(arguments[0]) is { } low && Number(arguments[1]) is { } high:
                _minimum = low;
                _maximum = high;
                _minimumExclusive = Named(attribute, "MinimumIsExclusive") is true;
                _maximumExclusive = Named(attribute, "MaximumIsExclusive") is true;
                break;
            case "StringLengthAttribute" when arguments.Length == 1 && arguments[0].Value is int maximumLength:
                _maxLength = maximumLength;
                if (Named(attribute, "MinimumLength") is int minimumLength && minimumLength > 0)
                {
                    _minLength = minimumLength;
                }

                break;
            case "MinLengthAttribute" when arguments.Length == 1 && arguments[0].Value is int minLength:
                _minLength = minLength;
                break;
            case "MaxLengthAttribute" when arguments.Length == 1 && arguments[0].Value is int maxLength:
                _maxLength = maxLength;
                break;
            case "LengthAttribute" when arguments.Length == 2 && arguments[0].Value is int lengthMin && arguments[1].Value is int lengthMax:
                _minLength = lengthMin;
                _maxLength = lengthMax;
                break;
            case "RegularExpressionAttribute" when arguments.Length == 1 && arguments[0].Value is string pattern && pattern.Length > 0:
                _pattern = pattern;
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 读命名参数的值。
    /// </summary>
    private static object? Named(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(a => string.Equals(a.Key, name, StringComparison.Ordinal)).Value.Value;

    /// <summary>
    /// 把 <c>[Range]</c> 的整数或浮点参数写成 JSON 数值文本；其他重载（类型 + 字符串）不折。
    /// </summary>
    private static string? Number(TypedConstant constant) => constant.Value switch
    {
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        double d when !double.IsNaN(d) && !double.IsInfinity(d) => d.ToString("R", CultureInfo.InvariantCulture),
        _ => null,
    };

    /// <summary>
    /// 是否集合（实现 <c>IEnumerable&lt;T&gt;</c>）。
    /// </summary>
    private static bool IsEnumerable(INamedTypeSymbol type)
    {
        foreach (var candidate in Enumerable.Repeat(type, 1).Concat(type.AllInterfaces))
        {
            if (candidate.IsGenericType && candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return true;
            }
        }

        return false;
    }
}
