// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.Routing;

/// <summary>
/// 把 HTTP 请求绑定为 XGI 载荷。
/// </summary>
/// <remarks>
/// 一个 JSON 对象，由路由值、JSON 请求体与查询串合并而成。路由值最优先（URL 里的标识就是权威），其次请求体，最后查询串。操作描述带请求的序列化元数据时，路由值与查询串按目标属性的类型写成 JSON 布尔、数字或数组，属性名取序列化名（不分大小写匹配）；没有元数据（插件代理）或找不到属性时按字符串写入，多值写成字符串数组。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class OperationRequestBinder
{
    /// <summary>
    /// 带请求体的方法。
    /// </summary>
    /// <remarks>
    /// GET、DELETE 的请求体被忽略。
    /// </remarks>
    private static readonly string[] _bodyMethods = ["POST", "PUT", "PATCH"];

    /// <summary>
    /// 绑定。
    /// </summary>
    /// <remarks>
    /// 请求体不是 JSON 对象时返回 <see cref="KernelErrors.InvalidOperationPayload"/>。
    /// </remarks>
    public static async ValueTask<Result<ReadOnlyMemory<byte>>> BindAsync(HttpContext httpContext, OperationDescriptor descriptor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(descriptor);
        var request = httpContext.Request;
        var properties = PropertiesOf(descriptor.RequestInfo);
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new ArrayBufferWriter<byte>();
        using var document = await ReadBodyAsync(request, cancellationToken).ConfigureAwait(false);
        if (document is not null && document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return Error.Validation(KernelErrors.InvalidOperationPayload, KernelMessages.InvalidOperationPayload, MessageArguments.Of(("id", descriptor.Id), ("reason", "the request body must be a JSON object")));
        }

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            // 路由值最优先：URL 里的标识就是权威，不让请求体覆盖它
            foreach (var (name, value) in request.RouteValues)
            {
                if (value is null)
                {
                    continue;
                }

                var property = properties.GetValueOrDefault(name);
                if (written.Add(property?.Name ?? name))
                {
                    WriteValues(writer, property?.Name ?? name, [Convert.ToString(value, CultureInfo.InvariantCulture)], property, descriptor.RequestInfo);
                }
            }

            if (document is not null)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (written.Add(property.Name))
                    {
                        property.WriteTo(writer);
                    }
                }
            }

            // 查询串最后：按目标属性类型写，单值写到集合属性时也写成数组
            foreach (var (name, values) in request.Query)
            {
                var property = properties.GetValueOrDefault(name);
                if (written.Add(property?.Name ?? name))
                {
                    WriteValues(writer, property?.Name ?? name, [.. values], property, descriptor.RequestInfo);
                }
            }

            writer.WriteEndObject();
        }

        return Result.Success(buffer.WrittenMemory);
    }

    /// <summary>
    /// 请求类型的属性按序列化名索引（不分大小写）。
    /// </summary>
    /// <remarks>
    /// 没有元数据或不是对象类型时为空表。
    /// </remarks>
    private static Dictionary<string, JsonPropertyInfo> PropertiesOf(JsonTypeInfo? requestInfo)
    {
        var properties = new Dictionary<string, JsonPropertyInfo>(StringComparer.OrdinalIgnoreCase);
        if (requestInfo is not { Kind: JsonTypeInfoKind.Object })
        {
            return properties;
        }

        foreach (var property in requestInfo.Properties)
        {
            properties.TryAdd(property.Name, property);
        }

        return properties;
    }

    /// <summary>
    /// 写一个来自路由或查询串的字段。
    /// </summary>
    /// <remarks>
    /// 集合属性写数组，标量属性写单值（多值且目标不是集合时保留数组，让反序列化报出类型不符）。
    /// </remarks>
    private static void WriteValues(Utf8JsonWriter writer, string name, string?[] values, JsonPropertyInfo? property, JsonTypeInfo? requestInfo)
    {
        writer.WritePropertyName(name);
        var elementType = property is null ? null : ElementTypeOf(property.PropertyType, requestInfo);
        if (elementType is not null || values.Length > 1)
        {
            writer.WriteStartArray();
            foreach (var value in values)
            {
                WriteScalar(writer, value, elementType);
            }

            writer.WriteEndArray();
            return;
        }

        WriteScalar(writer, values.Length == 0 ? null : values[0], property?.PropertyType);
    }

    /// <summary>
    /// 集合属性的元素类型（字符串不算集合）。
    /// </summary>
    /// <remarks>
    /// 元数据解析不了时返回 null，按标量处理。
    /// </remarks>
    private static Type? ElementTypeOf(Type propertyType, JsonTypeInfo? requestInfo)
    {
        if (propertyType == typeof(string) || requestInfo is null)
        {
            return null;
        }

        try
        {
            // 元素类型取自同一序列化上下文的元数据：源生成上下文登记了属性类型，就能拿到集合的元素类型，不需要反射
            var info = requestInfo.Options.GetTypeInfo(propertyType);
            return info.Kind == JsonTypeInfoKind.Enumerable ? info.ElementType : null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// 按目标类型写一个文本值。
    /// </summary>
    /// <remarks>
    /// 布尔、整数、浮点与数值枚举写 JSON 字面量，解析不了或其他类型写字符串（交给反序列化报错或按字符串处理）。
    /// </remarks>
    private static void WriteScalar(Utf8JsonWriter writer, string? value, Type? targetType)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var type = targetType is null ? null : Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (type == typeof(bool) && bool.TryParse(value, out var flag))
        {
            writer.WriteBooleanValue(flag);
        }
        else if (IsInteger(type) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            writer.WriteNumberValue(integer);
        }
        else if (IsInteger(type) && ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unsigned))
        {
            writer.WriteNumberValue(unsigned);
        }
        else if ((type == typeof(decimal) || type == typeof(double) || type == typeof(float)) && decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            writer.WriteNumberValue(number);
        }
        else if (type is { IsEnum: true } && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ordinal))
        {
            // 枚举的数值写成数字，名称写成字符串（只有带字符串枚举转换器的上下文能读）
            writer.WriteNumberValue(ordinal);
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }

    /// <summary>
    /// 是否整数类型。
    /// </summary>
    private static bool IsInteger(Type? type)
        => type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
            || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);

    /// <summary>
    /// 读取并解析 JSON 请求体。
    /// </summary>
    /// <remarks>
    /// 无请求体的方法或空请求体返回 <see langword="null"/>，非 JSON 抛出 <see cref="JsonException"/> 之外的情况由调用方翻译。
    /// </remarks>
    private static async ValueTask<JsonDocument?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!_bodyMethods.Contains(request.Method, StringComparer.OrdinalIgnoreCase) || request.ContentLength == 0)
        {
            return null;
        }

        using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        if (memory.Length == 0)
        {
            return null;
        }

        memory.Position = 0;
        try
        {
            return await JsonDocument.ParseAsync(memory, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // 非 JSON 的请求体按"不是对象"处理：交给调用方统一产出载荷不合法错误
            return JsonDocument.Parse("\"invalid\""u8.ToArray());
        }
    }
}
