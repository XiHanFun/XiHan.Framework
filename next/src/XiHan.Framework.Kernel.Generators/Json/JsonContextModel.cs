// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Kernel.Generators.Json;

/// <summary>
/// 项目里一个 <c>JsonSerializerContext</c> 子类的信息
/// </summary>
/// <remarks>
/// 登记的类型及其 <c>Default.&lt;属性&gt;</c> 名、命名策略、枚举是否按字符串序列化、字典键的命名策略、缺省的忽略条件与是否登记了转换器
/// </remarks>
internal sealed class JsonContextModel
{
    /// <summary>
    /// <c>JsonSerializableAttribute</c> 的元数据名
    /// </summary>
    private const string SerializableAttribute = "System.Text.Json.Serialization.JsonSerializableAttribute";

    /// <summary>
    /// <c>JsonSourceGenerationOptionsAttribute</c> 的元数据名
    /// </summary>
    private const string OptionsAttribute = "System.Text.Json.Serialization.JsonSourceGenerationOptionsAttribute";

    /// <summary>
    /// 登记的类型 → <c>Default</c> 上的属性名
    /// </summary>
    private readonly Dictionary<ITypeSymbol, string> _properties = new(SymbolEqualityComparer.Default);

    /// <summary>
    /// 从上下文类型符号读取
    /// </summary>
    /// <remarks>
    /// 特性按符号比对（<paramref name="serializable"/> / <paramref name="options"/> 由编译解析），不比显示字符串
    /// </remarks>
    /// <param name="type">上下文类型</param>
    /// <param name="serializable"><c>JsonSerializableAttribute</c> 的符号；编译里没有时为 <see langword="null"/></param>
    /// <param name="options"><c>JsonSourceGenerationOptionsAttribute</c> 的符号；编译里没有时为 <see langword="null"/></param>
    private JsonContextModel(INamedTypeSymbol type, INamedTypeSymbol? serializable, INamedTypeSymbol? options)
    {
        Type = type;
        foreach (var attribute in type.GetAttributes())
        {
            if (serializable is not null && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, serializable) && attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is ITypeSymbol registered)
            {
                var explicitName = attribute.NamedArguments.FirstOrDefault(static a => a.Key == "TypeInfoPropertyName").Value.Value as string;
                _properties[registered] = explicitName ?? TypeInfoNames.DefaultPropertyName(registered);
            }
            else if (options is not null && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, options))
            {
                foreach (var argument in attribute.NamedArguments)
                {
                    switch (argument.Key)
                    {
                        case "PropertyNamingPolicy" when argument.Value.Value is int policy:
                            NamingPolicy = (JsonNamingPolicyKind)policy;
                            break;
                        case "UseStringEnumConverter" when argument.Value.Value is bool useStrings:
                            UseStringEnumConverter = useStrings;
                            break;
                        case "DictionaryKeyPolicy" when argument.Value.Value is int keyPolicy:
                            DictionaryKeyPolicy = (JsonNamingPolicyKind)keyPolicy;
                            break;
                        case "DefaultIgnoreCondition" when argument.Value.Value is int condition:
                            DefaultIgnoreCondition = (JsonIgnoreConditionKind)condition;
                            break;
                        case "Converters" when argument.Value.Kind == TypedConstantKind.Array:
                            // 数组形式的命名参数不能取 Value（会抛出），只看有没有元素
                            HasConverters = !argument.Value.Values.IsDefaultOrEmpty;
                            break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 上下文类型
    /// </summary>
    public INamedTypeSymbol Type { get; }

    /// <summary>
    /// 属性命名策略
    /// </summary>
    public JsonNamingPolicyKind NamingPolicy { get; } = JsonNamingPolicyKind.Unspecified;

    /// <summary>
    /// 枚举是否按名字序列化
    /// </summary>
    public bool UseStringEnumConverter { get; }

    /// <summary>
    /// 字典键的命名策略
    /// </summary>
    /// <remarks>
    /// 只在写出时作用于字符串与枚举键（System.Text.Json 的行为）
    /// </remarks>
    public JsonNamingPolicyKind DictionaryKeyPolicy { get; } = JsonNamingPolicyKind.Unspecified;

    /// <summary>
    /// 没有 <c>[JsonIgnore]</c> 的属性的忽略条件
    /// </summary>
    public JsonIgnoreConditionKind DefaultIgnoreCondition { get; } = JsonIgnoreConditionKind.Never;

    /// <summary>
    /// 是否在选项里登记了转换器
    /// </summary>
    /// <remarks>
    /// 登记的转换器可能为某个键类型实现了属性名的读写，生成器看不出来
    /// </remarks>
    public bool HasConverters { get; }

    /// <summary>
    /// 该类型若已登记，返回 <c>Default</c> 上的属性名
    /// </summary>
    public bool TryGetPropertyName(ITypeSymbol type, out string propertyName) => _properties.TryGetValue(type, out propertyName!);

    /// <summary>
    /// 收集编译里全部 <c>JsonSerializerContext</c> 子类
    /// </summary>
    public static List<JsonContextModel> Collect(Compilation compilation)
    {
        var contexts = new List<JsonContextModel>();
        var baseType = compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonSerializerContext");
        if (baseType is null)
        {
            return contexts;
        }

        var serializable = compilation.GetTypeByMetadataName(SerializableAttribute);
        var options = compilation.GetTypeByMetadataName(OptionsAttribute);

        var stack = new Stack<INamespaceSymbol>();
        stack.Push(compilation.Assembly.GlobalNamespace);
        while (stack.Count > 0)
        {
            var ns = stack.Pop();
            foreach (var member in ns.GetMembers())
            {
                switch (member)
                {
                    case INamespaceSymbol child:
                        stack.Push(child);
                        break;
                    case INamedTypeSymbol type when DerivesFrom(type, baseType):
                        contexts.Add(new JsonContextModel(type, serializable, options));
                        break;
                }
            }
        }

        return contexts;
    }

    /// <summary>
    /// 类型是否（间接）派生自基类
    /// </summary>
    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }
}
