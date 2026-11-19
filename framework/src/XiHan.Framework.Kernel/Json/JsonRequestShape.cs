// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// 按序列化元数据核对载荷的形状
/// </summary>
/// <remarks>
/// 照 RFC-0011 规则 2 的顺序找出解码会报的第一处问题（ADR-0236）：按文本顺序逐个成员核对类型，缺少必填成员在所在对象结束时报该对象的路径。
/// System.Text.Json 自己报哪一处取决于类型的声明方式：源生成对带 <c>required</c>、<c>init</c> 成员或主构造函数的类型先读完构造参数、再填其余成员，缺少成员会先于写在前面的类型不符报出。
/// 先在这里按规范的顺序查一遍，反序列化就只在这里查不到的地方失败。只认各语言共有的类型（字符串、整数、浮点、布尔、数组、字典、对象与任意 JSON）以及能用 <see cref="JsonElement"/> 判定的 .NET 类型，枚举交给它自己的转换器读一次；带自定义转换器、多态与其他类型的部分不查，交给反序列化报
/// </remarks>
internal static class JsonRequestShape
{
    /// <summary>
    /// 找出载荷不合请求类型的第一处
    /// </summary>
    /// <returns>该处的路径；没有查出问题时为 <see langword="null"/></returns>
    public static string? FindProblem(JsonElement root, JsonTypeInfo info)
        => Visit(root, info, JsonPath.Root, info.Options.NumberHandling, false);

    /// <summary>
    /// 核对一个值
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="info">值的类型的元数据</param>
    /// <param name="path">值的路径</param>
    /// <param name="numberHandling">生效的数字读取方式</param>
    /// <param name="rejectNull">成员按可空标注不接受 null（<see cref="JsonSerializerOptions.RespectNullableAnnotations"/>）</param>
    private static string? Visit(JsonElement value, JsonTypeInfo info, string path, JsonNumberHandling numberHandling, bool rejectNull)
    {
        var type = info.Type;
        var underlying = Nullable.GetUnderlyingType(type);
        if (value.ValueKind == JsonValueKind.Null)
        {
            // 值类型（Nullable<T> 除外）不接受 null；引用类型接受，按可空标注拒绝时除外。任意 JSON 的 JsonElement 把 null 读成 Null 种类
            return (type.IsValueType && underlying is null && type != typeof(JsonElement)) || rejectNull ? path : null;
        }

        if (underlying is not null)
        {
            info = info.Options.GetTypeInfo(underlying);
            type = underlying;
        }

        // 多态的类型按鉴别器读，形状不在这里判定
        if (info.PolymorphismOptions is not null)
        {
            return null;
        }

        if (type.IsEnum)
        {
            return VisitEnum(value, info, path);
        }

        return info.Kind switch
        {
            JsonTypeInfoKind.Object => VisitObject(value, info, path, numberHandling),
            JsonTypeInfoKind.Enumerable => VisitArray(value, info, path, numberHandling),
            JsonTypeInfoKind.Dictionary => VisitDictionary(value, info, path, numberHandling),
            _ => VisitScalar(value, type, path, numberHandling),
        };
    }

    /// <summary>
    /// 核对一个对象：成员按文本顺序，必填成员在对象结束时
    /// </summary>
    private static string? VisitObject(JsonElement value, JsonTypeInfo info, string path, JsonNumberHandling numberHandling)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return path;
        }

        var options = info.Options;
        var comparison = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var disallowUnmapped = (info.UnmappedMemberHandling ?? options.UnmappedMemberHandling) == JsonUnmappedMemberHandling.Disallow
            && !info.Properties.Any(static p => p.IsExtensionData);
        var typeHandling = info.NumberHandling ?? numberHandling;
        HashSet<JsonPropertyInfo>? seen = null;
        foreach (var member in value.EnumerateObject())
        {
            var memberPath = JsonPath.Member(path, member.Name);
            var property = info.Properties.FirstOrDefault(p => !p.IsExtensionData && comparison.Equals(p.Name, member.Name));
            if (property is null)
            {
                if (disallowUnmapped)
                {
                    return memberPath;
                }

                continue;
            }

            (seen ??= []).Add(property);

            // 自定义转换器怎么读这里不知道，交给反序列化
            if (property.CustomConverter is not null)
            {
                continue;
            }

            var rejectNull = options.RespectNullableAnnotations && !property.IsSetNullable;
            if (Visit(member.Value, options.GetTypeInfo(property.PropertyType), memberPath, property.NumberHandling ?? typeHandling, rejectNull) is { } problem)
            {
                return problem;
            }
        }

        // 缺少必填成员与 System.Text.Json 一样报所在对象的路径
        foreach (var property in info.Properties)
        {
            if (property.IsRequired && (seen is null || !seen.Contains(property)))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// 核对一个数组：项按下标顺序
    /// </summary>
    private static string? VisitArray(JsonElement value, JsonTypeInfo info, string path, JsonNumberHandling numberHandling)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return path;
        }

        if (info.ElementType is not { } elementType)
        {
            return null;
        }

        var element = info.Options.GetTypeInfo(elementType);
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            // 数组项的可空标注 System.Text.Json 不核对
            if (Visit(item, element, JsonPath.Index(path, index++), numberHandling, false) is { } problem)
            {
                return problem;
            }
        }

        return null;
    }

    /// <summary>
    /// 核对一个字典：值按成员的文本顺序
    /// </summary>
    /// <remarks>
    /// 只核对字符串键的字典，其他键类型的文本怎么解析交给反序列化
    /// </remarks>
    private static string? VisitDictionary(JsonElement value, JsonTypeInfo info, string path, JsonNumberHandling numberHandling)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return path;
        }

        if (info.KeyType != typeof(string) || info.ElementType is not { } elementType)
        {
            return null;
        }

        var element = info.Options.GetTypeInfo(elementType);
        foreach (var member in value.EnumerateObject())
        {
            if (Visit(member.Value, element, JsonPath.Member(path, member.Name), numberHandling, false) is { } problem)
            {
                return problem;
            }
        }

        return null;
    }

    /// <summary>
    /// 核对一个标量
    /// </summary>
    /// <remarks>
    /// 按 System.Text.Json 内置转换器的读法：数字不从字符串读（<see cref="JsonNumberHandling.AllowReadingFromString"/> 时除外），整数不接受小数点与指数并按类型的范围核对，浮点上溢为无穷；认不出的类型不查
    /// </remarks>
    private static string? VisitScalar(JsonElement value, Type type, string path, JsonNumberHandling numberHandling)
    {
        if (type == typeof(JsonElement) || type == typeof(JsonDocument) || type == typeof(object) || typeof(JsonNode).IsAssignableFrom(type))
        {
            return null;
        }

        if (type == typeof(string))
        {
            return value.ValueKind == JsonValueKind.String ? null : path;
        }

        if (type == typeof(bool))
        {
            return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : path;
        }

        if (type == typeof(char))
        {
            return value.ValueKind == JsonValueKind.String && value.GetString()!.Length == 1 ? null : path;
        }

        if (type == typeof(Guid))
        {
            return value.ValueKind == JsonValueKind.String && value.TryGetGuid(out _) ? null : path;
        }

        if (type == typeof(DateTime))
        {
            return value.ValueKind == JsonValueKind.String && value.TryGetDateTime(out _) ? null : path;
        }

        if (type == typeof(DateTimeOffset))
        {
            return value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out _) ? null : path;
        }

        if (type == typeof(byte[]))
        {
            return value.ValueKind == JsonValueKind.String && value.TryGetBytesFromBase64(out _) ? null : path;
        }

        if (IsNumber(type))
        {
            if (value.ValueKind == JsonValueKind.Number)
            {
                return FitsNumber(value, type) ? null : path;
            }

            // 字符串里的数字只在声明了从字符串读时接受，内容按同样的规则
            if (value.ValueKind == JsonValueKind.String && (numberHandling & JsonNumberHandling.AllowReadingFromString) != 0)
            {
                return null;
            }

            return path;
        }

        return null;
    }

    /// <summary>
    /// 核对一个枚举值
    /// </summary>
    /// <remarks>
    /// 缺省的枚举转换器只读整数，字符串枚举转换器（<see cref="JsonStringEnumConverter"/>、源生成的 <c>UseStringEnumConverter</c>）读成员名、通常也读整数；
    /// 两者是同一个内部类型，只差构造时的选项，从元数据看不出读不读字符串，所以由枚举自己的转换器读这一个值。只读这一处，报错的位置与先后仍按文本顺序（ADR-0247）
    /// </remarks>
    private static string? VisitEnum(JsonElement value, JsonTypeInfo info, string path)
    {
        try
        {
            _ = value.Deserialize(info);
            return null;
        }
        catch (JsonException)
        {
            return path;
        }
    }

    /// <summary>
    /// 是否是内置转换器读的数字类型
    /// </summary>
    private static bool IsNumber(Type type)
        => type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(sbyte)
            || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(byte)
            || type == typeof(double) || type == typeof(float) || type == typeof(decimal);

    /// <summary>
    /// 数字是否能读成该类型
    /// </summary>
    /// <remarks>
    /// <see cref="JsonElement"/> 的 <c>TryGet</c> 与反序列化用的是同一套解析：整数只接受整数写法，双精度与单精度上溢为无穷，十进制小数上溢失败
    /// </remarks>
    private static bool FitsNumber(JsonElement value, Type type)
        => type == typeof(int) ? value.TryGetInt32(out _)
            : type == typeof(long) ? value.TryGetInt64(out _)
            : type == typeof(short) ? value.TryGetInt16(out _)
            : type == typeof(sbyte) ? value.TryGetSByte(out _)
            : type == typeof(uint) ? value.TryGetUInt32(out _)
            : type == typeof(ulong) ? value.TryGetUInt64(out _)
            : type == typeof(ushort) ? value.TryGetUInt16(out _)
            : type == typeof(byte) ? value.TryGetByte(out _)
            : type == typeof(double) ? value.TryGetDouble(out _)
            : type == typeof(float) ? value.TryGetSingle(out _)
            : type == typeof(decimal) && value.TryGetDecimal(out _);
}
