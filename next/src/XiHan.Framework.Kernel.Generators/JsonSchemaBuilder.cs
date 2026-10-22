// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Kernel.Generators;

/// <summary>
/// 从类型符号生成 JSON Schema（draft 2020-12）：基元类型映射到 <c>type</c>/<c>format</c>，集合为 <c>array</c>，字典为 <c>additionalProperties</c>，
/// 对象的公开可读属性按序列化上下文的命名策略成为 <c>properties</c>，非可空且无默认值的成为 <c>required</c>，嵌套对象进 <c>$defs</c> 并以 <c>$ref</c> 引用。
/// </summary>
internal sealed class JsonSchemaBuilder
{
    /// <summary>
    /// 命名策略。
    /// </summary>
    private readonly JsonNamingPolicyKind _namingPolicy;

    /// <summary>
    /// 枚举是否按名字。
    /// </summary>
    private readonly bool _stringEnums;

    /// <summary>
    /// 已进入 <c>$defs</c> 的对象类型 → 定义名。
    /// </summary>
    private readonly Dictionary<ITypeSymbol, string> _definitions = new(SymbolEqualityComparer.Default);

    /// <summary>
    /// <c>$defs</c> 的 JSON 片段，按定义名。
    /// </summary>
    private readonly List<KeyValuePair<string, string>> _definitionBodies = [];

    /// <summary>
    /// 创建构建器。
    /// </summary>
    public JsonSchemaBuilder(JsonNamingPolicyKind namingPolicy, bool stringEnums)
    {
        _namingPolicy = namingPolicy;
        _stringEnums = stringEnums;
    }

    /// <summary>
    /// 生成根类型的完整 Schema 文档。
    /// </summary>
    public string Build(ITypeSymbol root)
    {
        var body = SchemaOf(root, nullable: false, inline: true);
        var json = new JsonText().StartObject()
            .String("$schema", "https://json-schema.org/draft/2020-12/schema")
            .String("title", root.Name);
        // 根对象直接展开（去掉外层花括号再拼进来，逗号由 JsonText 补），其他对象放在 $defs
        var inner = body.Substring(1, body.Length - 2);
        if (inner.Length > 0)
        {
            json.Raw(null, inner);
        }

        if (_definitionBodies.Count > 0)
        {
            json.StartObject("$defs");
            foreach (var definition in _definitionBodies)
            {
                json.Raw(definition.Key, definition.Value);
            }

            json.EndObject();
        }

        return json.EndObject().ToString();
    }

    /// <summary>
    /// 某类型的 Schema 片段；<paramref name="inline"/> 为 <see langword="true"/> 时对象直接展开而不是进 <c>$defs</c>。
    /// </summary>
    private string SchemaOf(ITypeSymbol type, bool nullable, bool inline = false)
    {
        // 可空注解或 Nullable<T> 都允许 null；类型本身的 Schema 在内层决定
        if (type is INamedTypeSymbol { IsGenericType: true } nullableValue && nullableValue.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return SchemaOf(nullableValue.TypeArguments[0], nullable: true, inline);
        }

        nullable |= type.NullableAnnotation == NullableAnnotation.Annotated && !type.IsValueType;
        var json = new JsonText().StartObject();
        switch (type)
        {
            case IArrayTypeSymbol array when array.ElementType.SpecialType == SpecialType.System_Byte:
                Type(json, "string", nullable);
                json.String("contentEncoding", "base64");
                break;
            case IArrayTypeSymbol array:
                Type(json, "array", nullable);
                json.Raw("items", SchemaOf(array.ElementType, nullable: false));
                break;
            case INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType:
                if (_stringEnums)
                {
                    Type(json, "string", nullable);
                    json.StartArray("enum");
                    foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>().Where(static f => f.HasConstantValue))
                    {
                        json.String(null, member.Name);
                    }

                    json.EndArray();
                }
                else
                {
                    Type(json, "integer", nullable);
                }

                break;
            case INamedTypeSymbol named when Primitive(named) is { } primitive:
                Type(json, primitive.Type, nullable);
                if (primitive.Format is not null)
                {
                    json.String("format", primitive.Format);
                }

                break;
            case INamedTypeSymbol named when DictionaryValue(named) is { } valueType:
                Type(json, "object", nullable);
                json.Raw("additionalProperties", SchemaOf(valueType, nullable: false));
                break;
            case INamedTypeSymbol named when ElementType(named) is { } elementType:
                Type(json, "array", nullable);
                json.Raw("items", SchemaOf(elementType, nullable: false));
                break;
            case INamedTypeSymbol named when named.SpecialType == SpecialType.System_Object || named.ToDisplayString() is "System.Text.Json.JsonElement" or "System.Text.Json.Nodes.JsonNode":
                // 任意 JSON：不加约束
                break;
            case INamedTypeSymbol named when inline:
                Type(json, "object", nullable);
                WriteProperties(json, named);
                break;
            case INamedTypeSymbol named:
                json.EndObject();
                return Reference(named, nullable);
            default:
                break;
        }

        return json.EndObject().ToString();
    }

    /// <summary>
    /// 对象类型进 <c>$defs</c>（只登记一次，先占位再生成以支持递归），返回 <c>$ref</c> 片段。
    /// </summary>
    private string Reference(INamedTypeSymbol type, bool nullable)
    {
        if (!_definitions.TryGetValue(type, out var name))
        {
            name = UniqueDefinitionName(type.Name);
            _definitions[type] = name;
            var body = new JsonText().StartObject();
            Type(body, "object", nullable: false);
            WriteProperties(body, type);
            _definitionBodies.Add(new KeyValuePair<string, string>(name, body.EndObject().ToString()));
        }

        var reference = new JsonText().StartObject().String("$ref", "#/$defs/" + name);
        if (nullable)
        {
            // 可空引用：$ref 与 null 的 anyOf
            return new JsonText().StartObject().StartArray("anyOf").Raw(null, reference.EndObject().ToString()).StartObject().String("type", "null").EndObject().EndArray().EndObject().ToString();
        }

        return reference.EndObject().ToString();
    }

    /// <summary>
    /// 同名类型来自不同命名空间时加序号。
    /// </summary>
    private string UniqueDefinitionName(string name)
    {
        var candidate = name;
        var index = 2;
        while (_definitionBodies.Any(d => d.Key == candidate))
        {
            candidate = name + index++;
        }

        return candidate;
    }

    /// <summary>
    /// 写 <c>properties</c> 与 <c>required</c>：公开可读的实例属性，跳过 <c>[JsonIgnore]</c>；名字按 <c>[JsonPropertyName]</c> 或命名策略。
    /// </summary>
    private void WriteProperties(JsonText json, INamedTypeSymbol type)
    {
        var required = new List<string>();
        json.StartObject("properties");
        foreach (var property in Properties(type))
        {
            var name = PropertyName(property);
            json.Raw(name, SchemaOf(property.Type, nullable: false));
            if (IsRequired(type, property))
            {
                required.Add(name);
            }
        }

        json.EndObject();
        if (required.Count > 0)
        {
            json.StartArray("required");
            foreach (var name in required)
            {
                json.String(null, name);
            }

            json.EndArray();
        }
    }

    /// <summary>
    /// 类型及其基类的公开可读实例属性，基类的在前。
    /// </summary>
    private static IEnumerable<IPropertySymbol> Properties(INamedTypeSymbol type)
    {
        var chain = new Stack<INamedTypeSymbol>();
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            chain.Push(current);
        }

        while (chain.Count > 0)
        {
            foreach (var member in chain.Pop().GetMembers().OfType<IPropertySymbol>())
            {
                if (member.IsStatic || member.IsIndexer || member.GetMethod is null || member.DeclaredAccessibility != Accessibility.Public || member.ExplicitInterfaceImplementations.Length > 0)
                {
                    continue;
                }

                // 记录的 EqualityContract 与 [JsonIgnore] 的属性不序列化
                if (member.Name == "EqualityContract" || member.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonIgnoreAttribute"))
                {
                    continue;
                }

                yield return member;
            }
        }
    }

    /// <summary>
    /// 属性在 JSON 里的名字。
    /// </summary>
    private string PropertyName(IPropertySymbol property)
    {
        var explicitName = property.GetAttributes()
            .FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonPropertyNameAttribute")?
            .ConstructorArguments.FirstOrDefault().Value as string;
        return explicitName ?? NamingPolicies.Apply(_namingPolicy, property.Name);
    }

    /// <summary>
    /// 是否必填：带 <c>required</c> 修饰符，或非可空且对应的主构造参数没有默认值；可空、有初始值或有默认值的不必填。
    /// </summary>
    private static bool IsRequired(INamedTypeSymbol type, IPropertySymbol property)
    {
        if (property.IsRequired)
        {
            return true;
        }

        var nullable = property.Type.NullableAnnotation == NullableAnnotation.Annotated
            || (property.Type is INamedTypeSymbol { IsGenericType: true } generic && generic.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
        if (nullable)
        {
            return false;
        }

        // 位置记录：属性对应的主构造参数没有默认值才必填
        var parameter = type.Constructors
            .Where(static c => c.DeclaredAccessibility == Accessibility.Public)
            .SelectMany(static c => c.Parameters)
            .FirstOrDefault(p => string.Equals(p.Name, property.Name, StringComparison.OrdinalIgnoreCase));
        if (parameter is not null)
        {
            return !parameter.HasExplicitDefaultValue;
        }

        // 普通类型的非可空引用属性通常有初始值（string.Empty 等），不强求；值类型有默认值
        return false;
    }

    /// <summary>
    /// 写 <c>type</c>，可空时写成 <c>[type, "null"]</c>。
    /// </summary>
    private static void Type(JsonText json, string type, bool nullable)
    {
        if (nullable)
        {
            json.StartArray("type").String(null, type).String(null, "null").EndArray();
        }
        else
        {
            json.String("type", type);
        }
    }

    /// <summary>
    /// 基元类型映射。
    /// </summary>
    private static (string Type, string? Format)? Primitive(INamedTypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
                return ("string", null);
            case SpecialType.System_Char:
                return ("string", null);
            case SpecialType.System_Boolean:
                return ("boolean", null);
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
                return ("integer", "int32");
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                return ("integer", "int64");
            case SpecialType.System_Single:
                return ("number", "float");
            case SpecialType.System_Double:
                return ("number", "double");
            case SpecialType.System_Decimal:
                return ("number", "decimal");
            case SpecialType.System_DateTime:
                return ("string", "date-time");
            default:
                return type.ToDisplayString() switch
                {
                    "System.Guid" => ("string", "uuid"),
                    "System.DateTimeOffset" => ("string", "date-time"),
                    "System.DateOnly" => ("string", "date"),
                    "System.TimeOnly" => ("string", "time"),
                    "System.TimeSpan" => ("string", "duration"),
                    "System.Uri" => ("string", "uri"),
                    "System.Int128" or "System.UInt128" or "System.Numerics.BigInteger" => ("integer", null),
                    "System.Half" => ("number", null),
                    _ => null,
                };
        }
    }

    /// <summary>
    /// 字典类型的值类型（键必须是字符串）。
    /// </summary>
    private static ITypeSymbol? DictionaryValue(INamedTypeSymbol type)
    {
        foreach (var candidate in Enumerable.Repeat(type, 1).Concat(type.AllInterfaces))
        {
            if (candidate.IsGenericType && candidate.TypeArguments.Length == 2 && candidate.TypeArguments[0].SpecialType == SpecialType.System_String)
            {
                var definition = candidate.OriginalDefinition.ToDisplayString();
                if (definition is "System.Collections.Generic.IDictionary<TKey, TValue>" or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
                {
                    return candidate.TypeArguments[1];
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 集合类型的元素类型（字符串本身不算集合）。
    /// </summary>
    private static ITypeSymbol? ElementType(INamedTypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return null;
        }

        foreach (var candidate in Enumerable.Repeat(type, 1).Concat(type.AllInterfaces))
        {
            if (candidate.IsGenericType && candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return candidate.TypeArguments[0];
            }
        }

        return null;
    }
}
