// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// 操作 ID 到路由的默认推导（<c>spec/conventions/operation-ids.md</c>）：
/// <c>create</c> → <c>POST /{resource}</c>，<c>list</c> → <c>GET /{resource}</c>；请求声明了 <c>id</c> 属性时
/// <c>get</c> → <c>GET /{resource}/{id}</c>，<c>update</c> → <c>PUT /{resource}/{id}</c>，<c>delete</c> → <c>DELETE /{resource}/{id}</c>，其他动词 → <c>POST /{resource}/{id}:{verb}</c>；
/// 没有 <c>id</c> 时去掉 <c>/{id}</c> 段，落在集合（或单例资源）上：<c>GET /{resource}</c>、<c>PUT /{resource}</c>、<c>DELETE /{resource}</c>、<c>POST /{resource}:{verb}</c>（ADR-0015）。
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
        return Derive(descriptor.Contract, HasIdentifier(descriptor));
    }

    /// <summary>
    /// 按约定推导路由。
    /// </summary>
    /// <param name="contract">契约。</param>
    /// <param name="hasIdentifier">请求是否声明 <c>id</c> 属性；有则路由带 <c>/{id}</c> 段，没有则落在集合（或单例资源）上。</param>
    public static HttpRoute Derive(OperationContract contract, bool hasIdentifier)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var resource = contract.Resource;
        // 请求带标识是对某个实例的动作，路径带 /{id}；不带标识是对集合（或单例资源，如 profile）的动作，去掉这一段（Google AIP 的两种形态）
        var target = hasIdentifier ? $"/{resource}/{{id}}" : $"/{resource}";
        return contract.Verb switch
        {
            "create" => new HttpRoute("POST", $"/{resource}"),
            "list" => new HttpRoute("GET", $"/{resource}"),
            "get" => new HttpRoute("GET", target),
            "update" => new HttpRoute("PUT", target),
            "delete" => new HttpRoute("DELETE", target),
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
