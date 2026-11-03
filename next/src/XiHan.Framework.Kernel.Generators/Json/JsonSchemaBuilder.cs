// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Kernel.Generators.Json;

/// <summary>
/// 从类型符号生成 JSON Schema（draft 2020-12）
/// </summary>
/// <remarks>
/// 基元类型映射到 <c>type</c>/<c>format</c>，集合为 <c>array</c>，字典为 <c>object</c> + <c>additionalProperties</c>（键不是字符串时键的文本形式写在 <c>propertyNames</c>，System.Text.Json 不支持的键类型登记到 <see cref="UnsupportedKeys"/>），任意 JSON（<c>object</c>、<c>JsonElement</c>、<c>JsonDocument</c>、<c>JsonNode</c> 与 <c>JsonValue</c>）为不加约束的 <c>{}</c>，<c>JsonObject</c> 按字典、<c>JsonArray</c> 按集合（值与元素是任意 JSON）；类型按原始定义的元数据名识别（<see cref="KnownTypes"/>），可空标注只加 <c>null</c>、不改变形状，对象的公开可读属性按序列化上下文的命名策略成为 <c>properties</c>（<c>[JsonIgnore]</c> 只有 <c>Always</c> 不进，条件忽略可能省略的不进 <c>required</c>），非可空且无默认值的成为 <c>required</c>，嵌套对象进 <c>$defs</c> 并以 <c>$ref</c> 引用。根 <c>title</c> 与定义名按 <see cref="SchemaNames"/>（封闭泛型带实参名）；按字符串序列化的枚举进 <c>$defs/&lt;枚举类型名&gt;</c>（<c>type: string</c> + <c>enum</c>）并以 <c>$ref</c> 引用，可空为 <c>anyOf</c> 带 <c>null</c>。属性（或位置记录的主构造参数）上的 DataAnnotations 折进 Schema：<c>[Required]</c> 进 <c>required</c> 并去掉可空（字符串还加 <c>minLength: 1</c>），<c>[Range]</c> → <c>minimum</c> / <c>maximum</c>（<c>MinimumIsExclusive</c> 等 → <c>exclusiveMinimum</c> / <c>exclusiveMaximum</c>），<c>[StringLength]</c> / <c>[MinLength]</c> / <c>[MaxLength]</c> / <c>[Length]</c> → 字符串的 <c>minLength</c> / <c>maxLength</c> 或集合的 <c>minItems</c> / <c>maxItems</c>，<c>[RegularExpression]</c> → <c>pattern</c>。XML 文档注释进 <c>description</c>（<see cref="DocumentationText"/>）：根与 <c>$defs</c> 里的对象、枚举取类型的 <c>&lt;summary&gt;</c>，属性取属性的 <c>&lt;summary&gt;</c>，没有时取主构造参数在类型上的 <c>&lt;param&gt;</c>
/// </remarks>
internal sealed class JsonSchemaBuilder
{
    /// <summary>
    /// System.Text.Json 序列化特性所在的命名空间
    /// </summary>
    private const string SerializationNamespace = "System.Text.Json.Serialization";

    /// <summary>
    /// 有符号整数键的文本形式
    /// </summary>
    private const string SignedIntegerKey = "^-?[0-9]+$";

    /// <summary>
    /// 无符号整数键的文本形式
    /// </summary>
    private const string UnsignedIntegerKey = "^[0-9]+$";

    /// <summary>
    /// 浮点与 decimal 键的文本形式（System.Text.Json 写成 <c>1.5</c>、<c>1E+20</c>）
    /// </summary>
    private const string NumberKey = "^-?[0-9]+(\\.[0-9]+)?([eE][+-]?[0-9]+)?$";

    /// <summary>
    /// 命名策略
    /// </summary>
    private readonly JsonNamingPolicyKind _namingPolicy;

    /// <summary>
    /// 枚举是否按名字
    /// </summary>
    private readonly bool _stringEnums;

    /// <summary>
    /// 字典键的命名策略
    /// </summary>
    private readonly JsonNamingPolicyKind _dictionaryKeyPolicy;

    /// <summary>
    /// 没有 <c>[JsonIgnore]</c> 的属性的忽略条件
    /// </summary>
    private readonly JsonIgnoreConditionKind _defaultIgnoreCondition;

    /// <summary>
    /// 上下文是否登记了转换器
    /// </summary>
    private readonly bool _hasConverters;

    /// <summary>
    /// 遇到的不受支持的字典键类型，去重
    /// </summary>
    private readonly List<ITypeSymbol> _unsupportedKeys = [];

    /// <summary>
    /// 已进入 <c>$defs</c> 的对象类型 → 定义名
    /// </summary>
    private readonly Dictionary<ITypeSymbol, string> _definitions = new(SymbolEqualityComparer.Default);

    /// <summary>
    /// <c>$defs</c> 的 JSON 片段，按定义名
    /// </summary>
    private readonly List<KeyValuePair<string, string>> _definitionBodies = [];

    /// <summary>
    /// 按序列化上下文的选项创建构建器
    /// </summary>
    public JsonSchemaBuilder(JsonContextModel context)
    {
        _namingPolicy = context.NamingPolicy;
        _stringEnums = context.UseStringEnumConverter;
        _dictionaryKeyPolicy = context.DictionaryKeyPolicy;
        _defaultIgnoreCondition = context.DefaultIgnoreCondition;
        _hasConverters = context.HasConverters;
    }

    /// <summary>
    /// 生成途中遇到的 System.Text.Json 不支持的字典键类型
    /// </summary>
    /// <remarks>
    /// 运行期序列化与反序列化都会抛出 <c>NotSupportedException</c>；生成器据此报 XHG006，Schema 照写为对象、不写 <c>propertyNames</c>
    /// </remarks>
    public IReadOnlyList<ITypeSymbol> UnsupportedKeys => _unsupportedKeys;

    /// <summary>
    /// 生成根类型的完整 Schema 文档
    /// </summary>
    public string Build(ITypeSymbol root)
    {
        var body = SchemaOf(root, nullable: false, inline: true);
        var json = new JsonText().StartObject()
            .String("$schema", "https://json-schema.org/draft/2020-12/schema")
            .String("title", SchemaNames.Of(root));
        if (root is INamedTypeSymbol && DocumentationText.Summary(root) is { } description)
        {
            json.String("description", description);
        }

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
    /// 某类型的 Schema 片段
    /// </summary>
    /// <remarks>
    /// <paramref name="inline"/> 为 <see langword="true"/> 时对象直接展开而不是进 <c>$defs</c>；<paramref name="forceNonNull"/> 为 <see langword="true"/> 时（<c>[Required]</c>）忽略类型的可空性
    /// </remarks>
    private string SchemaOf(ITypeSymbol type, bool nullable, bool inline = false, bool forceNonNull = false)
    {
        // 可空注解或 Nullable<T> 都允许 null；类型本身的 Schema 在内层决定；[Required] 的属性不允许 null
        if (type is INamedTypeSymbol { IsGenericType: true } nullableValue && nullableValue.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return SchemaOf(nullableValue.TypeArguments[0], nullable: !forceNonNull, inline);
        }

        nullable |= !forceNonNull && type.NullableAnnotation == NullableAnnotation.Annotated && !type.IsValueType;
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
            case INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType when _stringEnums && !inline:
                // 字符串枚举是有名字的值集：进 $defs/<枚举类型名>，属性以 $ref 引用，协议适配器与前端生成器据此得到同名类型而不是按属性各起一个
                json.EndObject();
                return Reference(enumType, nullable, body =>
                {
                    Type(body, "string", nullable: false);
                    WriteEnumMembers(body, enumType);
                });
            case INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType:
                if (_stringEnums)
                {
                    Type(json, "string", nullable);
                    WriteEnumMembers(json, enumType);
                }
                else
                {
                    // 枚举按整数，格式取底层类型：协议适配器据此选 32 / 64 位
                    Type(json, "integer", nullable);
                    json.String("format", enumType.EnumUnderlyingType?.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64 ? "int64" : "int32");
                }

                break;
            case INamedTypeSymbol named when Primitive(named) is { } primitive:
                Type(json, primitive.Type, nullable);
                if (primitive.Format is not null)
                {
                    json.String("format", primitive.Format);
                }

                break;
            case INamedTypeSymbol named when DictionaryOf(named) is { } dictionary:
                // System.Text.Json 把任何键的字典写成 JSON 对象：键成属性名，非字符串键的文本形式写在 propertyNames
                Type(json, "object", nullable);
                if (KeyNames(dictionary.Key) is { } keyNames)
                {
                    json.Raw("propertyNames", keyNames);
                }

                json.Raw("additionalProperties", SchemaOf(dictionary.Value, nullable: false));
                break;
            case INamedTypeSymbol named when ElementType(named) is { } elementType:
                Type(json, "array", nullable);
                json.Raw("items", SchemaOf(elementType, nullable: false));
                break;
            case INamedTypeSymbol named when IsAnyJson(named):
                // 任意 JSON：不加约束，可空也不改变形状（{} 本来就接受 null）。JsonObject / JsonArray 已在上面按字典与集合匹配，元素是任意值
                break;
            case INamedTypeSymbol named when inline:
                Type(json, "object", nullable);
                WriteProperties(json, named);
                break;
            case INamedTypeSymbol named:
                json.EndObject();
                return Reference(named, nullable, body =>
                {
                    Type(body, "object", nullable: false);
                    WriteProperties(body, named);
                });
            default:
                break;
        }

        return json.EndObject().ToString();
    }

    /// <summary>
    /// 写字符串枚举的 <c>enum</c>：成员名按声明顺序
    /// </summary>
    private static void WriteEnumMembers(JsonText json, INamedTypeSymbol enumType)
    {
        json.StartArray("enum");
        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>().Where(static f => f.HasConstantValue))
        {
            json.String(null, member.Name);
        }

        json.EndArray();
    }

    /// <summary>
    /// 类型进 <c>$defs</c>
    /// </summary>
    /// <remarks>
    /// 只登记一次，先占位再生成以支持递归；定义名按 <see cref="SchemaNames"/>。返回 <c>$ref</c> 片段。<paramref name="writeBody"/> 写定义的内容
    /// </remarks>
    private string Reference(INamedTypeSymbol type, bool nullable, Action<JsonText> writeBody)
    {
        if (!_definitions.TryGetValue(type, out var name))
        {
            name = UniqueDefinitionName(SchemaNames.Of(type));
            _definitions[type] = name;
            var body = new JsonText().StartObject();
            if (DocumentationText.Summary(type) is { } description)
            {
                body.String("description", description);
            }

            writeBody(body);
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
    /// 同名类型来自不同命名空间时加序号
    /// </summary>
    /// <remarks>
    /// 按已占位的名字判重，嵌套生成途中遇到的同名类型也不会撞名
    /// </remarks>
    private string UniqueDefinitionName(string name)
    {
        var candidate = name;
        var index = 2;
        while (_definitions.ContainsValue(candidate))
        {
            candidate = name + index++;
        }

        return candidate;
    }

    /// <summary>
    /// 写 <c>properties</c> 与 <c>required</c>
    /// </summary>
    /// <remarks>
    /// 公开可读的实例属性；忽略条件为 <c>Always</c> 的跳过，条件忽略可能省略的不进 <c>required</c>（见 <see cref="MayBeOmitted"/>）；名字按 <c>[JsonPropertyName]</c> 或命名策略
    /// </remarks>
    private void WriteProperties(JsonText json, INamedTypeSymbol type)
    {
        var required = new List<string>();
        json.StartObject("properties");
        foreach (var property in Properties(type))
        {
            // Always（含不写条件的 [JsonIgnore]）既不写出也不读入；其余条件下属性仍会序列化，留在 Schema 里
            var condition = IgnoreCondition(property);
            if (condition == JsonIgnoreConditionKind.Always)
            {
                continue;
            }

            var name = PropertyName(property);
            var constraints = PropertyConstraints.Collect(type, property);
            json.Raw(name, WithDescription(constraints.ApplyTo(SchemaOf(property.Type, nullable: false, forceNonNull: constraints.Required), property.Type), DescriptionOf(type, property)));
            // 可能被省略的属性不能必填：required 修饰符、位置记录与 [Required] 都让位于序列化的实际行为
            if (!MayBeOmitted(condition, property.Type) && (constraints.Required || IsRequired(type, property)))
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
    /// 属性的说明
    /// </summary>
    /// <remarks>
    /// 属性自己的 <c>&lt;summary&gt;</c>，没有时取声明它的类型上同名主构造参数的 <c>&lt;param&gt;</c>
    /// </remarks>
    private static string? DescriptionOf(INamedTypeSymbol type, IPropertySymbol property)
        => DocumentationText.Summary(property) ?? DocumentationText.Parameter(property.ContainingType ?? type, property.Name);

    /// <summary>
    /// 在 Schema 片段（紧凑的 JSON 对象）最前面加上 <c>description</c>
    /// </summary>
    /// <remarks>
    /// 没有说明时原样返回
    /// </remarks>
    private static string WithDescription(string schema, string? description)
    {
        if (description is null)
        {
            return schema;
        }

        var member = new JsonText().StartObject().String("description", description).EndObject().ToString();
        return schema == "{}" ? member : member.Substring(0, member.Length - 1) + "," + schema.Substring(1);
    }

    /// <summary>
    /// 属性的有效忽略条件
    /// </summary>
    /// <remarks>
    /// 有 <c>[JsonIgnore]</c> 时取它的 <c>Condition</c>（不写即 <c>Always</c>，<c>Never</c> 覆盖上下文缺省），没有时取上下文的 <c>DefaultIgnoreCondition</c>
    /// </remarks>
    private JsonIgnoreConditionKind IgnoreCondition(IPropertySymbol property)
    {
        var attribute = property.GetAttributes().FirstOrDefault(static a => KnownTypes.Is(a.AttributeClass, SerializationNamespace, "JsonIgnoreAttribute"));
        if (attribute is null)
        {
            return _defaultIgnoreCondition;
        }

        return attribute.NamedArguments.FirstOrDefault(static a => a.Key == "Condition").Value.Value is int condition ? (JsonIgnoreConditionKind)condition : JsonIgnoreConditionKind.Always;
    }

    /// <summary>
    /// 忽略条件是否会让一个按可空契约有值的属性不出现
    /// </summary>
    /// <remarks>
    /// 可空的属性本来就不必填，这里只看不可空的：<c>WhenWriting</c> / <c>WhenReading</c> 总有一个方向没有它；<c>WhenWritingDefault</c> 下值类型取默认值（0、false、空 Guid）时不写出；不可空的引用按可空契约不会是 null，<c>WhenWritingDefault</c> / <c>WhenWritingNull</c> 都不省略它
    /// </remarks>
    private static bool MayBeOmitted(JsonIgnoreConditionKind condition, ITypeSymbol type) => condition switch
    {
        JsonIgnoreConditionKind.WhenWriting or JsonIgnoreConditionKind.WhenReading => true,
        JsonIgnoreConditionKind.WhenWritingDefault => type.IsValueType,
        _ => false,
    };

    /// <summary>
    /// 类型及其基类的公开可读实例属性，基类的在前
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

                // 记录的 EqualityContract 不序列化；[JsonIgnore] 按条件在 WriteProperties 里处理
                if (member.Name == "EqualityContract")
                {
                    continue;
                }

                yield return member;
            }
        }
    }

    /// <summary>
    /// 属性在 JSON 里的名字
    /// </summary>
    private string PropertyName(IPropertySymbol property)
    {
        var explicitName = property.GetAttributes()
            .FirstOrDefault(static a => KnownTypes.Is(a.AttributeClass, SerializationNamespace, "JsonPropertyNameAttribute"))?
            .ConstructorArguments.FirstOrDefault().Value as string;
        return explicitName ?? NamingPolicies.Apply(_namingPolicy, property.Name);
    }

    /// <summary>
    /// 是否必填
    /// </summary>
    /// <remarks>
    /// 带 <c>required</c> 修饰符，或非可空且对应的主构造参数没有默认值；可空、有初始值或有默认值的不必填
    /// </remarks>
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
    /// 写 <c>type</c>，可空时写成 <c>[type, "null"]</c>
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
    /// 基元类型映射
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
                // 按命名空间与元数据名识别：可空标注的引用类型（Uri?）的显示名带 ?，按显示名比对会把它当成普通对象展开
                return KnownTypes.NameIn(type, "System") switch
                {
                    "Guid" => ("string", "uuid"),
                    "DateTimeOffset" => ("string", "date-time"),
                    "DateOnly" => ("string", "date"),
                    "TimeOnly" => ("string", "time"),
                    "TimeSpan" => ("string", "duration"),
                    "Uri" => ("string", "uri"),
                    "Int128" or "UInt128" => ("integer", null),
                    "Half" => ("number", null),
                    _ => KnownTypes.Is(type, "System.Numerics", "BigInteger") ? ("integer", null) : null,
                };
        }
    }

    /// <summary>
    /// 字典类型的键与值类型
    /// </summary>
    /// <remarks>
    /// 类型本身或其接口是 <c>IDictionary&lt;TKey, TValue&gt;</c> / <c>IReadOnlyDictionary&lt;TKey, TValue&gt;</c>，任何键类型都算（System.Text.Json 都按对象处理，键类型是否受支持由 <see cref="KeyNames"/> 判定）
    /// </remarks>
    private static (ITypeSymbol Key, ITypeSymbol Value)? DictionaryOf(INamedTypeSymbol type)
    {
        foreach (var candidate in Enumerable.Repeat(type, 1).Concat(type.AllInterfaces))
        {
            if (candidate.IsGenericType && candidate.TypeArguments.Length == 2
                && KnownTypes.NameIn(candidate, "System.Collections.Generic") is "IDictionary`2" or "IReadOnlyDictionary`2")
            {
                return (candidate.TypeArguments[0], candidate.TypeArguments[1]);
            }
        }

        return null;
    }

    /// <summary>
    /// 字典键的 <c>propertyNames</c> 片段
    /// </summary>
    /// <remarks>
    /// 按 System.Text.Json 写出的属性名：整数写成十进制文本，浮点与 decimal 写成数字文本，<c>bool</c> 写成 <c>True</c> / <c>False</c>，<c>char</c> 是单个字符，枚举是成员名（按 <c>DictionaryKeyPolicy</c> 改写），<c>Guid</c>、日期与时间、<c>Uri</c> 带与值相同的 <c>format</c>。字符串、<c>object</c>、<c>Version</c> 与自带转换器的键不加约束，返回 <see langword="null"/>；其余键类型 System.Text.Json 不支持，登记到 <see cref="UnsupportedKeys"/> 后也返回 <see langword="null"/>
    /// </remarks>
    private string? KeyNames(ITypeSymbol key)
    {
        if (key is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            // 枚举键总按成员名写出（不论是否按字符串序列化枚举值），字典键的命名策略也作用于它
            var names = new JsonText().StartObject().StartArray("enum");
            foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>().Where(static f => f.HasConstantValue))
            {
                names.String(null, NamingPolicies.Apply(_dictionaryKeyPolicy, member.Name));
            }

            return names.EndArray().EndObject().ToString();
        }

        switch (key.SpecialType)
        {
            case SpecialType.System_String:
            case SpecialType.System_Object:
                return null;
            case SpecialType.System_Boolean:
                return new JsonText().StartObject().StartArray("enum").String(null, "True").String(null, "False").EndArray().EndObject().ToString();
            case SpecialType.System_Char:
                return new JsonText().StartObject().Number("minLength", 1).Number("maxLength", 1).EndObject().ToString();
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
                return Pattern(SignedIntegerKey);
            case SpecialType.System_Byte:
            case SpecialType.System_UInt16:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
                return Pattern(UnsignedIntegerKey);
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return Pattern(NumberKey);
            case SpecialType.System_DateTime:
                return Format("date-time");
        }

        switch (KnownTypes.NameIn(key, "System"))
        {
            case "Int128":
                return Pattern(SignedIntegerKey);
            case "UInt128":
                return Pattern(UnsignedIntegerKey);
            case "Half":
                return Pattern(NumberKey);
            case "Guid":
                return Format("uuid");
            case "DateTimeOffset":
                return Format("date-time");
            case "DateOnly":
                return Format("date");
            case "TimeOnly":
                return Format("time");
            case "TimeSpan":
                return Format("duration");
            case "Uri":
                return Format("uri");
            case "Version":
                return null;
        }

        // 键类型自带 [JsonConverter]，或上下文登记了转换器：转换器可能实现了属性名的读写，按普通键处理、不报
        if (_hasConverters || key.GetAttributes().Any(static a => KnownTypes.Is(a.AttributeClass, SerializationNamespace, "JsonConverterAttribute")))
        {
            return null;
        }

        if (!_unsupportedKeys.Contains(key, SymbolEqualityComparer.Default))
        {
            _unsupportedKeys.Add(key);
        }

        return null;
    }

    /// <summary>
    /// 只有 <c>pattern</c> 的 Schema 片段
    /// </summary>
    private static string Pattern(string pattern) => new JsonText().StartObject().String("pattern", pattern).EndObject().ToString();

    /// <summary>
    /// 只有 <c>format</c> 的 Schema 片段
    /// </summary>
    private static string Format(string format) => new JsonText().StartObject().String("format", format).EndObject().ToString();

    /// <summary>
    /// 集合类型的元素类型（字符串本身不算集合）
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

    /// <summary>
    /// 是否任意 JSON 值
    /// </summary>
    /// <remarks>
    /// <see cref="object"/>、<c>JsonElement</c>、<c>JsonDocument</c>、<c>JsonNode</c> 及其派生类型（<c>JsonValue</c>）；按原始定义的元数据名比对，可空标注、别名不影响。<c>JsonObject</c> / <c>JsonArray</c> 先按字典与集合匹配，不走到这里
    /// </remarks>
    private static bool IsAnyJson(INamedTypeSymbol type)
        => type.SpecialType == SpecialType.System_Object
            || KnownTypes.NameIn(type, "System.Text.Json") is "JsonElement" or "JsonDocument"
            || KnownTypes.IsOrDerivesFrom(type, "System.Text.Json.Nodes", "JsonNode");
}
