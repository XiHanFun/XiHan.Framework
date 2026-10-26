// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.RegularExpressions;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Http.Routing;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 从操作登记表生成 OpenAPI 3.1 文档：路径与方法按 <see cref="HttpProtocolOptions.RouteFor"/>，参数与请求体来自 <see cref="OperationSchema"/>，
/// 嵌套定义提升到 <c>components/schemas</c>，失败响应统一为 RFC 9457 Problem Details。没有 Schema 的操作只发布路径与操作 ID。
/// 契约的授权要求以扩展字段发布：操作上的 <c>x-xihan-permission</c>（权限码）或 <c>x-xihan-policy</c>（策略名），
/// 以及 <c>components</c> 下按权限码索引的 <c>x-xihan-permissions</c>（权限定义）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static partial class OpenApiDocumentBuilder
{
    /// <summary>
    /// JSON Schema 文档里不进 OpenAPI 的顶层键。
    /// </summary>
    private static readonly string[] _droppedKeys = ["$schema", "$defs"];

    /// <summary>
    /// 带请求体的方法。
    /// </summary>
    private static readonly string[] _bodyMethods = ["POST", "PUT", "PATCH"];

    /// <summary>
    /// 生成文档的 UTF-8 JSON。
    /// </summary>
    /// <param name="registry">操作登记表。</param>
    /// <param name="options">HTTP 选项（路由覆盖与前缀）。</param>
    /// <param name="permissions">权限码注册表；为 <see langword="null"/> 时 <c>x-xihan-permissions</c> 只收录被已发布操作引用的权限码，且只有 <c>code</c>。</param>
    /// <param name="title">文档标题，通常是应用名。</param>
    /// <param name="version">文档版本。</param>
    public static byte[] Build(OperationRegistry registry, HttpProtocolOptions options, PermissionRegistry? permissions, string title, string version)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var components = new SortedDictionary<string, JsonDocument>(StringComparer.Ordinal);
        var documents = new List<JsonDocument>();
        var referencedPermissions = new SortedSet<string>(StringComparer.Ordinal);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("openapi", "3.1.0");
            writer.WriteStartObject("info");
            writer.WriteString("title", title);
            writer.WriteString("version", version);
            writer.WriteEndObject();

            writer.WriteStartObject("paths");
            // 同一路径模板下可能有多个方法（GET /orders/{id} 与 PUT /orders/{id}），按模板分组后再按方法写
            var byTemplate = registry.ExposedTo(Expose.Http)
                .Where(static d => d.Contract.Stability != Stability.Internal)
                .Select(d => (Descriptor: d, Route: options.RouteFor(d)))
                .GroupBy(static e => e.Route.Template, StringComparer.Ordinal)
                .OrderBy(static g => g.Key, StringComparer.Ordinal);
            foreach (var group in byTemplate)
            {
                writer.WriteStartObject(group.Key);
                foreach (var (descriptor, route) in group.OrderBy(static e => e.Route.Method, StringComparer.Ordinal))
                {
                    writer.WriteStartObject(route.Method.ToLowerInvariant());
                    WriteOperation(writer, descriptor, route, components, documents, referencedPermissions);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();

            writer.WriteStartObject("components");
            writer.WriteStartObject("schemas");
            WriteProblemDetails(writer);
            foreach (var (name, schema) in components)
            {
                writer.WritePropertyName(name);
                schema.RootElement.WriteTo(writer);
            }

            writer.WriteEndObject();
            WritePermissions(writer, referencedPermissions, permissions);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        foreach (var document in documents)
        {
            document.Dispose();
        }

        foreach (var document in components.Values)
        {
            document.Dispose();
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// 写一个操作对象：操作 ID、标签、稳定性与授权要求的扩展字段、路径与查询参数、请求体、响应。被引用的权限码记入 <paramref name="referencedPermissions"/>。
    /// </summary>
    private static void WriteOperation(Utf8JsonWriter writer, OperationDescriptor descriptor, HttpRoute route, SortedDictionary<string, JsonDocument> components, List<JsonDocument> documents, SortedSet<string> referencedPermissions)
    {
        writer.WriteString("operationId", descriptor.Id);
        writer.WriteStartArray("tags");
        writer.WriteStringValue(descriptor.Contract.Resource);
        writer.WriteEndArray();
        if (descriptor.Contract.Stability != Stability.Stable)
        {
            writer.WriteString("x-xihan-stability", descriptor.Contract.Stability.ToString());
        }

        // 契约里权限与策略二选一（OperationContract 构造时已校验），都没有则是无授权要求的操作，不写扩展字段。
        // 权限码不是 OAuth scope、策略名更不是，所以不落到标准的 security / securitySchemes，留给认证方案用
        if (descriptor.Contract.Permission is { } permission)
        {
            writer.WriteString("x-xihan-permission", permission);
            referencedPermissions.Add(permission);
        }
        else if (descriptor.Contract.Policy is { } policy)
        {
            writer.WriteString("x-xihan-policy", policy);
        }

        var request = Load(descriptor.Schema?.Request, components, documents);
        var response = Load(descriptor.Schema?.Response, components, documents);
        var pathParameters = PathParameter().Matches(route.Template).Select(static m => m.Groups["name"].Value).ToList();
        var hasBody = _bodyMethods.Contains(route.Method, StringComparer.Ordinal);
        var required = request is { } r && r.RootElement.TryGetProperty("required", out var requiredArray)
            ? requiredArray.EnumerateArray().Select(static e => e.GetString()!).ToHashSet(StringComparer.Ordinal)
            : [];

        writer.WriteStartArray("parameters");
        foreach (var name in pathParameters)
        {
            WriteParameter(writer, name, "path", required: true, PropertySchema(request, name));
        }

        // 没有请求体的方法：请求对象里除路由值以外的属性都从查询串绑定
        if (!hasBody && request is { } queryable && queryable.RootElement.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (!pathParameters.Contains(property.Name, StringComparer.Ordinal))
                {
                    WriteParameter(writer, property.Name, "query", required.Contains(property.Name), property.Value);
                }
            }
        }

        writer.WriteEndArray();

        if (hasBody && request is { } body)
        {
            writer.WriteStartObject("requestBody");
            writer.WriteBoolean("required", true);
            writer.WriteStartObject("content");
            writer.WriteStartObject("application/json");
            writer.WritePropertyName("schema");
            WriteSchema(writer, body.RootElement);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteStartObject("responses");
        writer.WriteStartObject(HttpRouteConvention.SuccessStatusCode(descriptor.Contract).ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteString("description", "Success");
        if (response is { } ok)
        {
            writer.WriteStartObject("content");
            writer.WriteStartObject("application/json");
            writer.WritePropertyName("schema");
            WriteSchema(writer, ok.RootElement);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteStartObject("default");
        writer.WriteString("description", "Error");
        writer.WriteStartObject("content");
        writer.WriteStartObject("application/problem+json");
        writer.WriteStartObject("schema");
        writer.WriteString("$ref", "#/components/schemas/ProblemDetails");
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>
    /// 解析一段 JSON Schema：把 <c>#/$defs/</c> 引用改写为 <c>#/components/schemas/</c>，并把 <c>$defs</c> 提升到组件。
    /// </summary>
    private static JsonDocument? Load(string? schema, SortedDictionary<string, JsonDocument> components, List<JsonDocument> documents)
    {
        if (schema is null)
        {
            return null;
        }

        var document = JsonDocument.Parse(schema.Replace("\"#/$defs/", "\"#/components/schemas/", StringComparison.Ordinal));
        documents.Add(document);
        if (document.RootElement.TryGetProperty("$defs", out var definitions))
        {
            foreach (var definition in definitions.EnumerateObject())
            {
                // 同名定义按先到者为准：同一类型在多个操作里出现时内容相同
                if (!components.ContainsKey(definition.Name))
                {
                    components[definition.Name] = JsonDocument.Parse(definition.Value.GetRawText());
                }
            }
        }

        return document;
    }

    /// <summary>
    /// 请求 Schema 里某属性的 Schema；没有时按字符串。
    /// </summary>
    private static JsonElement? PropertySchema(JsonDocument? request, string name)
        => request is { } r && r.RootElement.TryGetProperty("properties", out var properties) && properties.TryGetProperty(name, out var property) ? property : null;

    /// <summary>
    /// 写一个参数对象。
    /// </summary>
    private static void WriteParameter(Utf8JsonWriter writer, string name, string location, bool required, JsonElement? schema)
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("in", location);
        writer.WriteBoolean("required", required);
        writer.WritePropertyName("schema");
        if (schema is { } element)
        {
            element.WriteTo(writer);
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteString("type", "string");
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 写 Schema 根对象，去掉 <c>$schema</c> 与已提升的 <c>$defs</c>。
    /// </summary>
    private static void WriteSchema(Utf8JsonWriter writer, JsonElement root)
    {
        writer.WriteStartObject();
        foreach (var property in root.EnumerateObject())
        {
            if (!_droppedKeys.Contains(property.Name, StringComparer.Ordinal))
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 写 <c>components</c> 下的 <c>x-xihan-permissions</c>：键是权限码（按序数排序），值是 <c>code</c>、<c>displayName</c> 与（非空时的）<c>description</c>。
    /// </summary>
    private static void WritePermissions(Utf8JsonWriter writer, SortedSet<string> referencedPermissions, PermissionRegistry? permissions)
    {
        // 收录范围 = 已发布操作引用到的权限码 ∪ 注册表里登记的全部定义：
        // 授权界面要列出应用的全部权限，其中有些权限不被任何 HTTP 操作引用（只在 gRPC / MCP 暴露的操作、端点级 RequireXiHanPermission），
        // 而被操作引用却没登记的权限码也要出现，前端才能按操作的权限码对上条目——这种只写 code，因为没有定义可取
        var codes = new SortedSet<string>(referencedPermissions, StringComparer.Ordinal);
        if (permissions is not null)
        {
            codes.UnionWith(permissions.All().Select(static p => p.Code));
        }

        writer.WriteStartObject("x-xihan-permissions");
        foreach (var code in codes)
        {
            writer.WriteStartObject(code);
            writer.WriteString("code", code);
            if (permissions is not null && permissions.Find(code).TryGetValue(out var definition))
            {
                writer.WriteString("displayName", definition.DisplayName);
                if (definition.Description is { } description)
                {
                    writer.WriteString("description", description);
                }
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// RFC 9457 Problem Details 的 Schema（含 <c>code</c>、<c>traceId</c>、字段级 <c>errors</c> 与标量 <c>details</c> 扩展）。
    /// </summary>
    private static void WriteProblemDetails(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("ProblemDetails");
        writer.WriteString("type", "object");
        writer.WriteStartObject("properties");
        foreach (var name in new[] { "type", "title", "detail", "instance", "code", "traceId" })
        {
            writer.WriteStartObject(name);
            writer.WriteString("type", "string");
            writer.WriteEndObject();
        }

        writer.WriteStartObject("status");
        writer.WriteString("type", "integer");
        writer.WriteEndObject();

        // errors：字段键 → 该字段的本地化消息数组（只在 VALIDATION 错误上出现）
        writer.WriteStartObject("errors");
        writer.WriteString("type", "object");
        writer.WriteStartObject("additionalProperties");
        writer.WriteString("type", "array");
        writer.WriteStartObject("items");
        writer.WriteString("type", "string");
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();

        // details：错误细节里的标量（名字 → 字符串 / 数值 / 布尔 / null）
        writer.WriteStartObject("details");
        writer.WriteString("type", "object");
        writer.WriteStartObject("additionalProperties");
        writer.WriteStartArray("type");
        foreach (var name in new[] { "string", "number", "integer", "boolean", "null" })
        {
            writer.WriteStringValue(name);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteStartArray("required");
        foreach (var name in new[] { "type", "title", "status", "code" })
        {
            writer.WriteStringValue(name);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    /// 路由模板里的参数名。
    /// </summary>
    [GeneratedRegex(@"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)[^}]*\}")]
    private static partial Regex PathParameter();
}
