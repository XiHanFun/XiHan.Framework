// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.Routing;

/// <summary>
/// 操作 ID 到路由的默认推导（<c>spec/conventions/operation-ids.md</c>）：
/// <c>create</c> → <c>POST /{resource}</c>，<c>list</c> → <c>GET /{resource}</c>；请求声明了 <c>id</c> 属性时
/// <c>get</c> → <c>GET /{resource}/{id}</c>，<c>update</c> → <c>PUT /{resource}/{id}</c>，<c>delete</c> → <c>DELETE /{resource}/{id}</c>，其他动词 → <c>POST /{resource}/{id}:{verb}</c>；
/// 没有 <c>id</c> 时去掉 <c>/{id}</c> 段，落在集合（或单例资源）上：<c>GET /{resource}</c>、<c>PUT /{resource}</c>、<c>DELETE /{resource}</c>、<c>POST /{resource}:{verb}</c>；
/// <c>get</c> / <c>list</c> / <c>delete</c> 的请求带嵌套对象时（GET / DELETE 不读请求体，嵌套对象进不了查询串）改用 POST 自定义动词，如 <c>POST /{resource}:list</c>（ADR-0015）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class HttpRouteConvention
{
    /// <summary>
    /// 标识属性的序列化名；比较不分大小写。
    /// </summary>
    public const string IdentifierProperty = "id";

    /// <summary>
    /// 按约定从操作描述推导路由：自定义动词的实例级 / 集合级由请求的序列化元数据决定。
    /// </summary>
    public static HttpRoute Derive(OperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return Derive(descriptor.Contract, HasIdentifier(descriptor), HasNestedFields(descriptor));
    }

    /// <summary>
    /// 按约定推导路由。
    /// </summary>
    /// <param name="contract">契约。</param>
    /// <param name="hasIdentifier">请求是否声明 <c>id</c> 属性；有则路由带 <c>/{id}</c> 段，没有则落在集合（或单例资源）上。</param>
    /// <param name="hasNestedFields">请求是否有嵌套对象字段；有则 <c>get</c> / <c>list</c> / <c>delete</c> 与只读的自定义动词改用 POST。</param>
    public static HttpRoute Derive(OperationContract contract, bool hasIdentifier, bool hasNestedFields = false)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var resource = contract.Resource;
        // 请求带标识是对某个实例的动作，路径带 /{id}；不带标识是对集合（或单例资源，如 profile）的动作，去掉这一段（Google AIP 的两种形态）
        var target = hasIdentifier ? $"/{resource}/{{id}}" : $"/{resource}";
        return contract.Verb switch
        {
            "create" => new HttpRoute("POST", $"/{resource}"),
            "update" => new HttpRoute("PUT", target),
            // GET / DELETE 不读请求体：请求只有标量（及标量集合）字段时才能全部从路由值与查询串绑定
            "list" when !hasNestedFields => new HttpRoute("GET", $"/{resource}"),
            "get" when !hasNestedFields => new HttpRoute("GET", target),
            "delete" when !hasNestedFields => new HttpRoute("DELETE", target),
            // 带嵌套对象（分页、复合筛选）的 list 仍是集合级动作，改成 POST 自定义动词
            "list" => new HttpRoute("POST", $"/{resource}:list"),
            // 声明只读的自定义动词（tree、list-online 等查询变体）与 get 同规则：只有标量字段时走 GET（Google AIP-136），有嵌套对象时仍是 POST
            var verb when contract.IsReadOnly && !hasNestedFields => new HttpRoute("GET", $"{target}:{verb}"),
            var verb => new HttpRoute("POST", $"{target}:{verb}"),
        };
    }

    /// <summary>
    /// 请求是否声明了 <see cref="IdentifierProperty"/>：先看源生成的序列化元数据（不用反射），
    /// 没有类型化元数据的描述（插件代理）看请求 Schema 的 <c>properties</c>，两者都没有按集合级处理。
    /// </summary>
    public static bool HasIdentifier(OperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.RequestInfo is { } requestInfo)
        {
            return HasIdentifier(requestInfo);
        }

        return descriptor.Schema is { } schema && SchemaDeclaresIdentifier(schema.Request);
    }

    /// <summary>
    /// 请求 Schema 的顶层 <c>properties</c> 是否声明了 <see cref="IdentifierProperty"/>；Schema 不是合法 JSON 对象时视为未声明。
    /// </summary>
    private static bool SchemaDeclaresIdentifier(string requestSchema)
    {
        try
        {
            using var document = JsonDocument.Parse(requestSchema);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("properties", out var properties)
                || properties.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in properties.EnumerateObject())
            {
                if (string.Equals(property.Name, IdentifierProperty, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 请求是否有嵌套对象字段（对象、字典，或元素是对象的集合）：先看序列化元数据，没有类型化元数据的描述看请求 Schema；都没有按扁平处理。
    /// </summary>
    public static bool HasNestedFields(OperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.RequestInfo is { } requestInfo)
        {
            return HasNestedFields(requestInfo);
        }

        return descriptor.Schema is { } schema && SchemaDeclaresNestedFields(schema.Request);
    }

    /// <summary>
    /// 请求类型的序列化元数据里是否有嵌套对象字段；属性类型的元数据取自同一序列化上下文，不用反射。
    /// </summary>
    public static bool HasNestedFields(JsonTypeInfo requestInfo)
    {
        ArgumentNullException.ThrowIfNull(requestInfo);
        if (requestInfo.Kind != JsonTypeInfoKind.Object)
        {
            return false;
        }

        foreach (var property in requestInfo.Properties)
        {
            var info = TryGetTypeInfo(requestInfo.Options, property.PropertyType);
            var nested = info?.Kind switch
            {
                JsonTypeInfoKind.Object or JsonTypeInfoKind.Dictionary => true,
                JsonTypeInfoKind.Enumerable => info.ElementType is { } element && TryGetTypeInfo(requestInfo.Options, element)?.Kind is not (null or JsonTypeInfoKind.None),
                _ => false,
            };
            if (nested)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 从序列化上下文取类型元数据；上下文不支持该类型时返回 null（按标量处理）。
    /// </summary>
    private static JsonTypeInfo? TryGetTypeInfo(JsonSerializerOptions options, Type type)
    {
        try
        {
            return options.GetTypeInfo(type);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// 请求 Schema 的顶层属性里是否有对象（<c>type: object</c>、<c>properties</c>，或 <c>$ref</c> 到对象定义）或元素是对象的数组；Schema 不是合法 JSON 对象时视为没有。
    /// </summary>
    private static bool SchemaDeclaresNestedFields(string requestSchema)
    {
        try
        {
            using var document = JsonDocument.Parse(requestSchema);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("properties", out var properties)
                || properties.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in properties.EnumerateObject())
            {
                var schema = property.Value;
                if (IsObjectSchema(root, schema, depth: 0)
                    || (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("items", out var items) && IsObjectSchema(root, items, depth: 0)))
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 标量定义互相引用时允许的最大跳数。
    /// </summary>
    private const int MaxReferenceDepth = 8;

    /// <summary>
    /// 一个属性 Schema 是否描述对象；<c>$ref</c> 按根 <c>$defs</c> 里的定义判定（字符串枚举这类标量定义不是对象），解析不到的引用按对象处理。
    /// </summary>
    private static bool IsObjectSchema(JsonElement root, JsonElement schema, int depth)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            return depth >= MaxReferenceDepth || !TryResolve(root, reference, out var target) || IsObjectSchema(root, target, depth + 1);
        }

        if (schema.TryGetProperty("properties", out _))
        {
            return true;
        }

        if (!schema.TryGetProperty("type", out var type))
        {
            return false;
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => type.ValueEquals("object"u8),
            JsonValueKind.Array => type.EnumerateArray().Any(static item => item.ValueKind == JsonValueKind.String && item.ValueEquals("object"u8)),
            _ => false,
        };
    }

    /// <summary>
    /// 解析本地 <c>$ref</c>（只支持 <c>#/$defs/名字</c>）。
    /// </summary>
    private static bool TryResolve(JsonElement root, JsonElement reference, out JsonElement target)
    {
        const string prefix = "#/$defs/";
        target = default;
        var text = reference.ValueKind == JsonValueKind.String ? reference.GetString() : null;
        return text is not null
            && text.StartsWith(prefix, StringComparison.Ordinal)
            && root.TryGetProperty("$defs", out var definitions)
            && definitions.ValueKind == JsonValueKind.Object
            && definitions.TryGetProperty(text[prefix.Length..], out target);
    }

    /// <summary>
    /// 请求类型的序列化元数据是否声明了 <see cref="IdentifierProperty"/>。
    /// </summary>
    public static bool HasIdentifier(JsonTypeInfo requestInfo)
    {
        ArgumentNullException.ThrowIfNull(requestInfo);
        foreach (var property in requestInfo.Properties)
        {
            if (string.Equals(property.Name, IdentifierProperty, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 成功时的状态码：<c>create</c> 为 201，其余 200。
    /// </summary>
    public static int SuccessStatusCode(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return contract.Verb == "create" ? 201 : 200;
    }
}
