// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Security.Abstractions.Authorization;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 生成 OpenAPI 3.1 文档。
/// </summary>
/// <remarks>
/// <para>操作按 <see cref="HttpProtocolOptions.RouteFor"/> 得到路径与方法，参数与请求体来自 <see cref="OperationSchema"/>，嵌套定义提升到 <c>components/schemas</c>，失败响应统一为 RFC 9457 Problem Details；没有 Schema 的操作只发布路径与操作 ID。</para>
/// <para>授权要求以扩展字段发布：操作上的 <c>x-xihan-permission</c>（权限码）或 <c>x-xihan-policy</c>（策略名），以及 <c>components</c> 下按权限码索引的 <c>x-xihan-permissions</c>；同时按区域附在 <c>description</c> 末尾一行。
/// 装配了 Bearer 认证时发布 <c>components.securitySchemes.bearer</c> 与全局 <c>security</c>，没有授权要求的操作标 <c>security: []</c>。</para>
/// <para>文案（ADR-0052）由 <see cref="OpenApiDocumentSource.Localize"/> 按区域给出：操作的 <c>summary</c> / <c>description</c> 取 <c>&lt;操作 ID&gt;.title</c> / <c>.description</c>，
/// 顶层 <c>tags</c> 的 <c>x-displayName</c> / <c>description</c> 取 <c>openapi.tags.&lt;资源段&gt;.name</c> / <c>.description</c>，分组的显示名取 <c>openapi.groups.&lt;组名&gt;.name</c>，
/// Schema 的 <c>description</c>（源生成器折进的 XML 文档注释）可被 <c>openapi.schemas.&lt;Schema 名&gt;.description</c> 与 <c>.properties.&lt;属性名&gt;</c> 按区域替换。</para>
/// <para>分组（<see cref="OpenApiDocumentSource.Groups"/>）：全量文档带 <c>x-tagGroups</c>；<see cref="OpenApiDocumentSource.Group"/> 指定时只出该组的操作与端点、它们引用的组件与权限码。</para>
/// <para>普通端点（见 <see cref="EndpointReader"/>）也进文档：路由参数、端点名 / 摘要 / 说明 / 标签、端点级授权要求、<c>Accepts</c> / <c>Produces</c> 的内容类型；
/// JSON 类型不另推导 Schema，只能引用已登记操作的根 Schema（显式 <see cref="OperationSchemaReference"/> 或按类型匹配），被引用的根 Schema 提升到 <c>components/schemas</c>，引用不到即 <see cref="HttpErrors.OpenApiSchemaUnresolved"/>。</para>
/// </remarks>
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
    /// <c>securitySchemes</c> 里 Bearer 方案的名字。
    /// </summary>
    private const string BearerSchemeName = "bearer";

    /// <summary>
    /// 没有本地化时成功响应的说明。
    /// </summary>
    private const string SuccessText = "Success";

    /// <summary>
    /// 没有本地化时失败响应的说明。
    /// </summary>
    private const string ErrorText = "Error";

    /// <summary>
    /// 生成文档的 UTF-8 JSON。
    /// </summary>
    /// <remarks>
    /// 标题与版本给定，其余（<c>servers</c>、认证方案、普通端点、摘要）都不带。
    /// </remarks>
    /// <param name="registry">操作登记表。</param>
    /// <param name="options">HTTP 选项（路由覆盖与前缀）。</param>
    /// <param name="permissions">权限码注册表；为 <see langword="null"/> 时 <c>x-xihan-permissions</c> 只收录被已发布操作引用的权限码，且只有 <c>code</c>。</param>
    /// <param name="title">文档标题，通常是应用名。</param>
    /// <param name="version">文档版本。</param>
    public static byte[] Build(OperationRegistry registry, HttpProtocolOptions options, PermissionRegistry? permissions, string title, string version)
        => Build(new OpenApiDocumentSource(registry, options, permissions) { Title = title, Version = version });

    /// <summary>
    /// 生成文档的 UTF-8 JSON。
    /// </summary>
    public static byte[] Build(OpenApiDocumentSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var context = new BuildContext(source);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("openapi", "3.1.0");
            writer.WriteStartObject("info");
            if (context.Group is { } group)
            {
                writer.WriteString("title", context.Text(GroupKey(group, "name")) ?? group.Name);
                if (context.Text(GroupKey(group, "description")) is { } groupDescription)
                {
                    writer.WriteString("description", groupDescription);
                }
            }
            else
            {
                writer.WriteString("title", source.Title);
            }

            writer.WriteString("version", source.Version);
            writer.WriteEndObject();
            WriteServers(writer, source.Servers);
            WriteTags(writer, context);

            writer.WriteStartObject("paths");
            foreach (var (template, methods) in context.Paths)
            {
                writer.WriteStartObject(template);
                foreach (var (method, write) in methods)
                {
                    writer.WriteStartObject(method.ToLowerInvariant());
                    write(writer);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();

            writer.WriteStartObject("components");
            writer.WriteStartObject("schemas");
            WriteProblemDetails(writer);
            foreach (var (name, schema) in context.Components)
            {
                writer.WritePropertyName(name);
                WriteLocalizedSchema(writer, schema.RootElement, name, context);
            }

            writer.WriteEndObject();
            if (context.Bearer)
            {
                // 权限码不是 OAuth scope、策略名更不是，所以 security 只挂方案不带 scope；权限与策略仍只走 x-xihan-* 扩展
                writer.WriteStartObject("securitySchemes");
                writer.WriteStartObject(BearerSchemeName);
                writer.WriteString("type", "http");
                writer.WriteString("scheme", "bearer");
                writer.WriteString("bearerFormat", "JWT");
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            WritePermissions(writer, context.ReferencedPermissions, source.Permissions, includeRegistered: context.Group is null);
            writer.WriteEndObject();
            if (context.Bearer)
            {
                WriteSecurity(writer, required: true);
            }

            if (context.Group is null && source.Groups.Count > 0)
            {
                WriteTagGroups(writer, context);
            }

            writer.WriteEndObject();
        }

        context.Dispose();
        return buffer.ToArray();
    }

    /// <summary>
    /// 写 <c>servers</c>（空列表不写）。
    /// </summary>
    private static void WriteServers(Utf8JsonWriter writer, IReadOnlyList<OpenApiServer> servers)
    {
        if (servers.Count == 0)
        {
            return;
        }

        writer.WriteStartArray("servers");
        foreach (var server in servers)
        {
            writer.WriteStartObject();
            writer.WriteString("url", server.Url);
            if (server.Description is { } description)
            {
                writer.WriteString("description", description);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 写顶层 <c>tags</c>。
    /// </summary>
    /// <remarks>
    /// 文档里用到的每个标签，名字序数排序，带区域的显示名与说明（有才写）。
    /// </remarks>
    private static void WriteTags(Utf8JsonWriter writer, BuildContext context)
    {
        if (context.UsedTags.Count == 0)
        {
            return;
        }

        writer.WriteStartArray("tags");
        foreach (var tag in context.UsedTags)
        {
            writer.WriteStartObject();
            writer.WriteString("name", tag);
            if (context.Text(TagKey(tag, "name")) is { } displayName)
            {
                writer.WriteString("x-displayName", displayName);
            }

            if (context.Text(TagKey(tag, "description")) is { } description)
            {
                writer.WriteString("description", description);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 写全量文档的 <c>x-tagGroups</c>。
    /// </summary>
    /// <remarks>
    /// 各组按排序、名字列出组内用到的标签，没进任何组的标签归入最后一组。
    /// </remarks>
    private static void WriteTagGroups(Utf8JsonWriter writer, BuildContext context)
    {
        var grouped = new HashSet<string>(StringComparer.Ordinal);
        writer.WriteStartArray("x-tagGroups");
        foreach (var group in context.Source.Groups.OrderBy(static g => g.Order).ThenBy(static g => g.Name, StringComparer.Ordinal))
        {
            var tags = context.GroupTags[group.Name].Where(context.UsedTags.Contains).ToList();
            grouped.UnionWith(tags);
            if (tags.Count > 0)
            {
                WriteTagGroup(writer, context.Text(GroupKey(group, "name")) ?? group.Name, tags);
            }
        }

        var rest = context.UsedTags.Where(tag => !grouped.Contains(tag)).ToList();
        if (rest.Count > 0)
        {
            WriteTagGroup(writer, context.Text(HttpMessages.OpenApiUngrouped) ?? "Other", rest);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 写 <c>x-tagGroups</c> 的一项。
    /// </summary>
    private static void WriteTagGroup(Utf8JsonWriter writer, string name, IEnumerable<string> tags)
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteStartArray("tags");
        foreach (var tag in tags)
        {
            writer.WriteStringValue(tag);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    /// 写 <c>security</c>：要求认证时挂 Bearer 方案，不要求时为空数组（覆盖全局要求）。
    /// </summary>
    private static void WriteSecurity(Utf8JsonWriter writer, bool required)
    {
        writer.WriteStartArray("security");
        if (required)
        {
            writer.WriteStartObject();
            writer.WriteStartArray(BearerSchemeName);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 写一个操作对象。
    /// </summary>
    /// <remarks>
    /// 操作 ID、标签、摘要、稳定性与授权要求的扩展字段、安全要求、路径与查询参数、请求体、响应。
    /// </remarks>
    private static void WriteOperation(Utf8JsonWriter writer, OperationDescriptor descriptor, HttpRoute route, BuildContext context)
    {
        writer.WriteString("operationId", descriptor.Id);
        writer.WriteStartArray("tags");
        writer.WriteStringValue(descriptor.Contract.Resource);
        writer.WriteEndArray();
        if (context.Text(OperationKey(descriptor.Id, ".title")) is { } summary)
        {
            writer.WriteString("summary", summary);
        }

        if (Join(context.Text(OperationKey(descriptor.Id, ".description")), AuthorizationText(descriptor.Contract.Permission, descriptor.Contract.Policy, context)) is { } description)
        {
            writer.WriteString("description", description);
        }

        if (descriptor.Contract.Stability != Stability.Stable)
        {
            writer.WriteString("x-xihan-stability", descriptor.Contract.Stability.ToString());
        }

        // 契约里权限与策略二选一（OperationContract 构造时已校验），都没有则是无授权要求的操作，不写扩展字段。
        // 权限码不是 OAuth scope、策略名更不是，所以不落到标准的 security / securitySchemes，留给认证方案用
        var authorized = WriteAuthorization(writer, descriptor.Contract.Permission, descriptor.Contract.Policy, context);
        if (context.Bearer && !authorized)
        {
            WriteSecurity(writer, required: false);
        }

        var request = context.Promoted(descriptor, SchemaRole.Request, out var requestName) ? null : context.Load(descriptor.Schema?.Request);
        var response = context.Promoted(descriptor, SchemaRole.Response, out var responseName) ? null : context.Load(descriptor.Schema?.Response);
        // 参数要看请求 Schema 的属性：被提升的根也从组件里读
        var requestForParameters = request ?? (requestName is { } promoted ? context.Component(promoted) : null);
        var pathParameters = PathParameter().Matches(route.Template).Select(static m => m.Groups["name"].Value).ToList();
        var hasBody = _bodyMethods.Contains(route.Method, StringComparer.Ordinal);
        var required = requestForParameters is { } r && r.RootElement.TryGetProperty("required", out var requiredArray)
            ? requiredArray.EnumerateArray().Select(static e => e.GetString()!).ToHashSet(StringComparer.Ordinal)
            : [];

        var requestKey = requestForParameters is { } titled ? SchemaKeyOf(titled.RootElement) : null;
        writer.WriteStartArray("parameters");
        foreach (var name in pathParameters)
        {
            WriteParameter(writer, name, "path", required: true, PropertySchema(requestForParameters, name), PropertyText(requestKey, name, context));
        }

        // 没有请求体的方法：请求对象里除路由值以外的属性都从查询串绑定
        if (!hasBody && requestForParameters is { } queryable && queryable.RootElement.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (!pathParameters.Contains(property.Name, StringComparer.Ordinal))
                {
                    WriteParameter(writer, property.Name, "query", required.Contains(property.Name), property.Value, PropertyText(requestKey, property.Name, context));
                }
            }
        }

        writer.WriteEndArray();

        if (hasBody && (request is not null || requestName is not null))
        {
            writer.WriteStartObject("requestBody");
            if (requestForParameters is { } body && RootText(body.RootElement, context) is { } bodyDescription)
            {
                writer.WriteString("description", bodyDescription);
            }

            writer.WriteBoolean("required", true);
            writer.WriteStartObject("content");
            writer.WriteStartObject("application/json");
            writer.WritePropertyName("schema");
            WriteSchemaOrReference(writer, request, requestName, context);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteStartObject("responses");
        writer.WriteStartObject(HttpRouteConvention.SuccessStatusCode(descriptor.Contract).ToString(CultureInfo.InvariantCulture));
        var responseRoot = response ?? (responseName is { } promotedResponse ? context.Component(promotedResponse) : null);
        writer.WriteString("description", (responseRoot is { } described ? RootText(described.RootElement, context) : null) ?? context.Text(HttpMessages.OpenApiSuccess) ?? SuccessText);
        if (response is not null || responseName is not null)
        {
            writer.WriteStartObject("content");
            writer.WriteStartObject("application/json");
            writer.WritePropertyName("schema");
            WriteSchemaOrReference(writer, response, responseName, context);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        WriteProblemResponse(writer, context);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 写一个普通端点的操作对象。
    /// </summary>
    /// <remarks>
    /// 端点名、标签、摘要与说明、授权要求的扩展字段、安全要求、路径参数、按内容类型写的请求体与响应。
    /// </remarks>
    private static void WriteEndpoint(Utf8JsonWriter writer, EndpointDescription endpoint, BuildContext context)
    {
        if (endpoint.Name is { } name)
        {
            writer.WriteString("operationId", name);
        }

        if (endpoint.Tags.Count > 0)
        {
            writer.WriteStartArray("tags");
            foreach (var tag in endpoint.Tags)
            {
                writer.WriteStringValue(tag);
            }

            writer.WriteEndArray();
        }

        if (endpoint.Summary is { } summary)
        {
            writer.WriteString("summary", summary);
        }

        // 端点级 RequireXiHanPermission 的要求是 permission:<码> 策略，拆回权限码；其余（authenticated、命名策略）按策略名
        var permission = endpoint.Requirement is { } requirement ? PermissionPolicy.CodeOf(requirement.Policy) : null;
        var policy = endpoint.Requirement is { } named && permission is null ? named.Policy : null;
        if (Join(endpoint.Description, AuthorizationText(permission, policy, context)) is { } description)
        {
            writer.WriteString("description", description);
        }

        var authorized = WriteAuthorization(writer, permission, policy, context);
        if (context.Bearer && !authorized)
        {
            WriteSecurity(writer, required: false);
        }

        writer.WriteStartArray("parameters");
        foreach (var parameter in PathParameter().Matches(endpoint.Template).Select(static m => m.Groups["name"].Value))
        {
            WriteParameter(writer, parameter, "path", required: true, null, null);
        }

        writer.WriteEndArray();

        if (endpoint.Accepts is { } accepts && accepts.ContentTypes.Count > 0)
        {
            writer.WriteStartObject("requestBody");
            writer.WriteBoolean("required", !accepts.IsOptional);
            writer.WriteStartObject("content");
            foreach (var contentType in accepts.ContentTypes)
            {
                writer.WriteStartObject(contentType);
                writer.WritePropertyName("schema");
                WriteContentSchema(writer, contentType, accepts.RequestType, endpoint, SchemaRole.Request, context);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteStartObject("responses");
        foreach (var produces in endpoint.Produces)
        {
            writer.WriteStartObject(produces.StatusCode.ToString(CultureInfo.InvariantCulture));
            writer.WriteString("description", produces.StatusCode is >= 200 and < 300 ? context.Text(HttpMessages.OpenApiSuccess) ?? SuccessText : context.Text(HttpMessages.OpenApiError) ?? ErrorText);
            // 有类型没写内容类型的按 JSON；只写了内容类型没有类型的按内容类型写（二进制下载）；两者都没有的没有正文
            var (contentTypes, type) = ResponseContent(produces);
            if (contentTypes.Count > 0)
            {
                writer.WriteStartObject("content");
                foreach (var contentType in contentTypes)
                {
                    writer.WriteStartObject(contentType);
                    writer.WritePropertyName("schema");
                    WriteContentSchema(writer, contentType, type, endpoint, SchemaRole.Response, context);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        WriteProblemResponse(writer, context);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 授权要求的那一行说明。
    /// </summary>
    /// <remarks>
    /// 权限（带显示名）、命名策略或只要求登录；没有要求或没有本地化时为 <see langword="null"/>。
    /// </remarks>
    private static string? AuthorizationText(string? permission, string? policy, BuildContext context)
    {
        if (permission is not null)
        {
            return context.Source.Permissions is { } registry && registry.Find(permission).TryGetValue(out var definition)
                ? context.Text(HttpMessages.OpenApiPermissionRequired, new Dictionary<string, object?>(StringComparer.Ordinal) { ["permission"] = permission, ["name"] = definition.DisplayName })
                : context.Text(HttpMessages.OpenApiPermissionCodeRequired, new Dictionary<string, object?>(StringComparer.Ordinal) { ["permission"] = permission });
        }

        if (policy is null)
        {
            return null;
        }

        return string.Equals(policy, AuthenticatedPolicy.PolicyName, StringComparison.Ordinal)
            ? context.Text(HttpMessages.OpenApiAuthenticationRequired)
            : context.Text(HttpMessages.OpenApiPolicyRequired, new Dictionary<string, object?>(StringComparer.Ordinal) { ["policy"] = policy });
    }

    /// <summary>
    /// 两段说明之间空一行。
    /// </summary>
    /// <remarks>
    /// 都没有为 <see langword="null"/>。
    /// </remarks>
    private static string? Join(string? first, string? second)
        => first is null ? second : second is null ? first : first + "\n\n" + second;

    /// <summary>
    /// 写授权要求的扩展字段。
    /// </summary>
    /// <remarks>
    /// 权限码进 <c>x-xihan-permission</c> 并记入被引用集合，否则策略名进 <c>x-xihan-policy</c>；返回是否有要求。
    /// </remarks>
    private static bool WriteAuthorization(Utf8JsonWriter writer, string? permission, string? policy, BuildContext context)
    {
        if (permission is not null)
        {
            writer.WriteString("x-xihan-permission", permission);
            context.ReferencedPermissions.Add(permission);
            return true;
        }

        if (policy is not null)
        {
            writer.WriteString("x-xihan-policy", policy);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 按内容类型写请求体 / 响应正文的 Schema。
    /// </summary>
    /// <remarks>
    /// JSON 引用已登记操作的根 Schema（提升到组件），<c>multipart/form-data</c> 是含二进制属性的对象，表单是对象，文本是字符串，其余二进制按内容类型写成 <c>format: binary</c> 的字符串。
    /// </remarks>
    private static void WriteContentSchema(Utf8JsonWriter writer, string contentType, Type? type, EndpointDescription endpoint, SchemaRole role, BuildContext context)
    {
        if (IsJson(contentType))
        {
            writer.WriteStartObject();
            writer.WriteString("$ref", "#/components/schemas/" + context.ResolveJson(endpoint, role, type));
            writer.WriteEndObject();
            return;
        }

        if (contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            // 表单文件：一个文件是 file 属性，多个是 files 数组，其余表单类型只知道是对象
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            if (type is not null && (typeof(IFormFile).IsAssignableFrom(type) || typeof(IFormFileCollection).IsAssignableFrom(type) || type == typeof(IFormFile[])))
            {
                var many = type != typeof(IFormFile) && !typeof(IFormFile).IsAssignableFrom(type);
                writer.WriteStartObject("properties");
                writer.WriteStartObject(many ? "files" : "file");
                if (many)
                {
                    writer.WriteString("type", "array");
                    writer.WriteStartObject("items");
                    WriteBinary(writer);
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteString("type", "string");
                    writer.WriteString("format", "binary");
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            return;
        }

        if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WriteEndObject();
            return;
        }

        if (type == typeof(string) || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "string");
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        WriteBinary(writer);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 响应元数据的内容类型与正文类型。
    /// </summary>
    /// <remarks>
    /// 有类型（非 void）没写内容类型的按 JSON，没有类型的按写出的内容类型，两者都没有的没有正文。
    /// </remarks>
    private static (List<string> ContentTypes, Type? Type) ResponseContent(IProducesResponseTypeMetadata produces)
    {
        var type = produces.Type is { } t && t != typeof(void) ? t : null;
        var contentTypes = produces.ContentTypes.ToList();
        if (contentTypes.Count == 0 && type is not null)
        {
            contentTypes.Add("application/json");
        }

        return (contentTypes, type);
    }

    /// <summary>
    /// 写二进制字符串 Schema 的成员。
    /// </summary>
    private static void WriteBinary(Utf8JsonWriter writer)
    {
        writer.WriteString("type", "string");
        writer.WriteString("format", "binary");
    }

    /// <summary>
    /// 内容类型是否 JSON（<c>application/json</c> 或 <c>+json</c> 后缀）。
    /// </summary>
    private static bool IsJson(string contentType)
    {
        var media = contentType.Split(';', 2)[0].Trim();
        return media.Equals("application/json", StringComparison.OrdinalIgnoreCase) || media.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 写 <c>default</c> 的 Problem Details 响应。
    /// </summary>
    private static void WriteProblemResponse(Utf8JsonWriter writer, BuildContext context)
    {
        writer.WriteStartObject("default");
        writer.WriteString("description", context.Text(HttpMessages.OpenApiError) ?? ErrorText);
        writer.WriteStartObject("content");
        writer.WriteStartObject("application/problem+json");
        writer.WriteStartObject("schema");
        writer.WriteString("$ref", "#/components/schemas/ProblemDetails");
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>
    /// 请求 Schema 里某属性的 Schema。
    /// </summary>
    /// <remarks>
    /// 没有时按字符串。
    /// </remarks>
    private static JsonElement? PropertySchema(JsonDocument? request, string name)
        => request is { } r && r.RootElement.TryGetProperty("properties", out var properties) && properties.TryGetProperty(name, out var property) ? property : null;

    /// <summary>
    /// 写一个参数对象。
    /// </summary>
    /// <remarks>
    /// 说明取区域覆盖或属性 Schema 的 <c>description</c>。
    /// </remarks>
    private static void WriteParameter(Utf8JsonWriter writer, string name, string location, bool required, JsonElement? schema, string? localized)
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("in", location);
        if ((localized ?? (schema is { ValueKind: JsonValueKind.Object } described && described.TryGetProperty("description", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null)) is { } description)
        {
            writer.WriteString("description", description);
        }

        writer.WriteBoolean("required", required);
        writer.WritePropertyName("schema");
        if (schema is { } element)
        {
            WritePropertySchema(writer, element, localized);
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
    /// 写内联的 Schema 根对象（去掉 <c>$schema</c> 与已提升的 <c>$defs</c>，说明按区域）。
    /// </summary>
    /// <remarks>
    /// 或已提升到组件时的 <c>$ref</c>。
    /// </remarks>
    private static void WriteSchemaOrReference(Utf8JsonWriter writer, JsonDocument? inline, string? componentName, BuildContext context)
    {
        if (componentName is { } name)
        {
            writer.WriteStartObject();
            writer.WriteString("$ref", "#/components/schemas/" + name);
            writer.WriteEndObject();
            return;
        }

        WriteLocalizedSchema(writer, inline!.RootElement, TitleOf(inline.RootElement), context);
    }

    /// <summary>
    /// 写 Schema 根对象。
    /// </summary>
    /// <remarks>
    /// 去掉 <c>$schema</c> 与已提升的 <c>$defs</c>，根与直接属性的 <c>description</c> 有区域覆盖时替换（没有说明的补上）。
    /// </remarks>
    private static void WriteLocalizedSchema(Utf8JsonWriter writer, JsonElement root, string? name, BuildContext context)
    {
        var key = name is null ? null : SchemaKey(name);
        var rootText = key is null ? null : context.Text(key + ".description");
        writer.WriteStartObject();
        if (rootText is not null)
        {
            writer.WriteString("description", rootText);
        }

        foreach (var property in root.EnumerateObject())
        {
            if (_droppedKeys.Contains(property.Name, StringComparer.Ordinal) || (rootText is not null && property.NameEquals("description")))
            {
                continue;
            }

            if (key is not null && property.NameEquals("properties") && property.Value.ValueKind == JsonValueKind.Object)
            {
                writer.WriteStartObject("properties");
                foreach (var member in property.Value.EnumerateObject())
                {
                    writer.WritePropertyName(member.Name);
                    WritePropertySchema(writer, member.Value, context.Text(PropertyKey(key, member.Name)));
                }

                writer.WriteEndObject();
                continue;
            }

            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 写一个属性的 Schema。
    /// </summary>
    /// <remarks>
    /// 有区域覆盖时把 <c>description</c> 换成它（放在最前）。
    /// </remarks>
    private static void WritePropertySchema(Utf8JsonWriter writer, JsonElement schema, string? localized)
    {
        if (localized is null || schema.ValueKind != JsonValueKind.Object)
        {
            schema.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("description", localized);
        foreach (var property in schema.EnumerateObject())
        {
            if (!property.NameEquals("description"))
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 根 Schema 的说明：区域覆盖或 <c>description</c>。
    /// </summary>
    private static string? RootText(JsonElement root, BuildContext context)
        => (SchemaKeyOf(root) is { } key ? context.Text(key + ".description") : null)
            ?? (root.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String ? description.GetString() : null);

    /// <summary>
    /// 属性的区域说明（只有覆盖，没有覆盖为 <see langword="null"/>）。
    /// </summary>
    private static string? PropertyText(string? schemaKey, string property, BuildContext context)
        => schemaKey is null ? null : context.Text(PropertyKey(schemaKey, property));

    /// <summary>
    /// 根 Schema 的 <c>title</c>。
    /// </summary>
    private static string? TitleOf(JsonElement root)
        => root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String ? title.GetString() : null;

    /// <summary>
    /// 按根 <c>title</c> 的 Schema 键前缀。
    /// </summary>
    /// <remarks>
    /// 没有 <c>title</c> 为 <see langword="null"/>。
    /// </remarks>
    private static string? SchemaKeyOf(JsonElement root) => TitleOf(root) is { } title ? SchemaKey(title) : null;

    /// <summary>
    /// 操作文案的键：操作 ID 的 <c>-</c> 换 <c>_</c> 再加后缀。
    /// </summary>
    private static string OperationKey(string operationId, string suffix) => operationId.Replace('-', '_') + suffix;

    /// <summary>
    /// 标签文案的键：<c>openapi.tags.&lt;标签&gt;.&lt;项&gt;</c>。
    /// </summary>
    private static string TagKey(string tag, string part) => "openapi.tags." + tag.Replace('-', '_') + "." + part;

    /// <summary>
    /// 分组文案的键：<c>openapi.groups.&lt;组名&gt;.&lt;项&gt;</c>。
    /// </summary>
    private static string GroupKey(OpenApiGroup group, string part) => "openapi.groups." + group.Name.Replace('-', '_') + "." + part;

    /// <summary>
    /// Schema 文案的键前缀：<c>openapi.schemas.&lt;snake_case 的 Schema 名&gt;</c>。
    /// </summary>
    private static string SchemaKey(string name) => "openapi.schemas." + SnakeCase(name);

    /// <summary>
    /// 属性文案的键：<c>&lt;Schema 键前缀&gt;.properties.&lt;snake_case 的属性名&gt;</c>。
    /// </summary>
    private static string PropertyKey(string schemaKey, string property) => schemaKey + ".properties." + SnakeCase(property);

    /// <summary>
    /// snake_case：大写字母前加下划线（连续大写只在后接小写时断开），连字符换下划线。
    /// </summary>
    internal static string SnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsUpper(ch))
            {
                var breaks = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]) || (char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1])));
                if (breaks)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                builder.Append(ch == '-' ? '_' : ch);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// 写 Schema 根对象，去掉 <c>$schema</c> 与已提升的 <c>$defs</c>（提升组件时用，不按区域改写）。
    /// </summary>
    private static void WriteStripped(Utf8JsonWriter writer, JsonElement root)
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
    /// 写 <c>components</c> 下的 <c>x-xihan-permissions</c>。
    /// </summary>
    /// <remarks>
    /// 键是权限码（按序数排序），值是 <c>code</c>、<c>displayName</c> 与（非空时的）<c>description</c>。
    /// </remarks>
    private static void WritePermissions(Utf8JsonWriter writer, SortedSet<string> referencedPermissions, PermissionRegistry? permissions, bool includeRegistered)
    {
        // 收录范围 = 已发布操作与普通端点引用到的权限码 ∪ 注册表里登记的全部定义：
        // 授权界面要列出应用的全部权限，其中有些权限不被任何 HTTP 操作引用（只在 gRPC / MCP 暴露的操作），
        // 而被引用却没登记的权限码也要出现，前端才能按权限码对上条目——这种只写 code，因为没有定义可取
        // 分组文档只收本组引用的权限码
        var codes = new SortedSet<string>(referencedPermissions, StringComparer.Ordinal);
        if (permissions is not null && includeRegistered)
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
    /// RFC 9457 Problem Details 的 Schema。
    /// </summary>
    /// <remarks>
    /// 含 <c>code</c>、<c>traceId</c>、字段级 <c>errors</c> 与标量 <c>details</c> 扩展。
    /// </remarks>
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

    /// <summary>
    /// 根 Schema 的角色。
    /// </summary>
    private enum SchemaRole
    {
        /// <summary>
        /// 请求。
        /// </summary>
        Request,

        /// <summary>
        /// 响应。
        /// </summary>
        Response,
    }

    /// <summary>
    /// 一次生成的工作状态。
    /// </summary>
    /// <remarks>
    /// 路径（模板 → 方法 → 写入动作）、组件、被引用的权限码、提升的根 Schema，以及普通端点 JSON 类型到操作根 Schema 的解析。先读普通端点决定哪些根要提升，再写路径，这样操作自己也以 <c>$ref</c> 引用被提升的根，一份 Schema 只出现一次。
    /// </remarks>
    private sealed class BuildContext : IDisposable
    {
        /// <summary>
        /// 解析过的 Schema 文档，结束时释放。
        /// </summary>
        private readonly List<JsonDocument> _documents = [];

        /// <summary>
        /// 提升到组件的根：操作 ID 与角色 → 组件名。
        /// </summary>
        private readonly Dictionary<(string Id, SchemaRole Role), string> _promoted = [];

        /// <summary>
        /// 发布的操作，按 ID。
        /// </summary>
        private readonly Dictionary<string, (OperationDescriptor Descriptor, HttpRoute Route)> _operations;

        /// <summary>
        /// 创建。
        /// </summary>
        /// <remarks>
        /// 挑出发布的操作与普通端点，解析端点的 JSON 引用（决定提升的根），按模板与方法排好写入顺序。
        /// </remarks>
        public BuildContext(OpenApiDocumentSource source)
        {
            Source = source;
            Bearer = source.AuthenticationSchemes.Any(static s => string.Equals(s, "Bearer", StringComparison.OrdinalIgnoreCase));
            var exposed = source.Registry.ExposedTo(Expose.Http).Where(static d => d.Contract.Stability != Stability.Internal).ToList();
            // 组的标签：组内操作的资源段加额外标签；按全部操作算，分组文档与全量文档的 x-tagGroups 一致
            GroupTags = source.Groups.ToDictionary(
                static g => g.Name,
                g => new SortedSet<string>(exposed.Where(d => g.Owners.Contains(d.OwnerId, StringComparer.Ordinal)).Select(static d => d.Contract.Resource).Concat(g.Tags), StringComparer.Ordinal),
                StringComparer.Ordinal);
            IReadOnlyList<EndpointDescription> endpoints = EndpointReader.Read(source.Endpoints);
            if (source.Group is { } groupName)
            {
                Group = source.Groups.FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.Ordinal))
                    ?? throw new ArgumentException($"The OpenAPI group '{groupName}' is not declared.", nameof(source));
                exposed = exposed.Where(d => Group.Owners.Contains(d.OwnerId, StringComparer.Ordinal)).ToList();
                var tags = GroupTags[groupName];
                endpoints = endpoints.Where(e => e.Tags.Any(tags.Contains)).ToList();
            }

            _operations = exposed.ToDictionary(static d => d.Id, d => (Descriptor: d, Route: source.Options.RouteFor(d)), StringComparer.Ordinal);
            UsedTags.UnionWith(exposed.Select(static d => d.Contract.Resource));
            UsedTags.UnionWith(endpoints.SelectMany(static e => e.Tags));

            // 先解析端点引用的 JSON 类型：引用不到在这里失败，提升的根在写操作前就定下来
            foreach (var endpoint in endpoints)
            {
                if (endpoint.Accepts is { } accepts && accepts.ContentTypes.Any(IsJson))
                {
                    ResolveJson(endpoint, SchemaRole.Request, accepts.RequestType);
                }

                foreach (var produces in endpoint.Produces)
                {
                    var (contentTypes, type) = ResponseContent(produces);
                    if (contentTypes.Any(IsJson))
                    {
                        ResolveJson(endpoint, SchemaRole.Response, type);
                    }
                }
            }

            var paths = new SortedDictionary<string, SortedDictionary<string, Action<Utf8JsonWriter>>>(StringComparer.Ordinal);
            foreach (var (descriptor, route) in _operations.Values)
            {
                Methods(paths, route.Template)[route.Method] = writer => WriteOperation(writer, descriptor, route, this);
            }

            foreach (var endpoint in endpoints)
            {
                var methods = Methods(paths, endpoint.Template);
                // 普通端点与操作共用一条路由：ASP.NET Core 到匹配时才报歧义，文档里会出现重复的键，这里先失败
                if (!methods.TryAdd(endpoint.Method, writer => WriteEndpoint(writer, endpoint, this)))
                {
                    throw new FrameworkException(HttpErrors.DuplicateRoute, $"Endpoint '{endpoint.DisplayName}' shares the HTTP route '{endpoint.Method} {endpoint.Template}' with another endpoint or operation.");
                }
            }

            Paths = paths;
        }

        /// <summary>
        /// 输入。
        /// </summary>
        public OpenApiDocumentSource Source { get; }

        /// <summary>
        /// 是否发布 Bearer 方案。
        /// </summary>
        public bool Bearer { get; }

        /// <summary>
        /// 只出这一组时的组。
        /// </summary>
        /// <remarks>
        /// 全量文档为 <see langword="null"/>。
        /// </remarks>
        public OpenApiGroup? Group { get; }

        /// <summary>
        /// 各组的标签，按组名。
        /// </summary>
        public Dictionary<string, SortedSet<string>> GroupTags { get; }

        /// <summary>
        /// 文档里用到的标签，序数排序。
        /// </summary>
        public SortedSet<string> UsedTags { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// 按键（与参数）取文档区域的文案。
        /// </summary>
        /// <remarks>
        /// 没有本地化或没有这条为 <see langword="null"/>。
        /// </remarks>
        public string? Text(string key, IReadOnlyDictionary<string, object?>? arguments = null) => Source.Localize?.Invoke(key, arguments);

        /// <summary>
        /// 路径：模板 → 方法 → 写入动作，都按序数排序。
        /// </summary>
        public SortedDictionary<string, SortedDictionary<string, Action<Utf8JsonWriter>>> Paths { get; }

        /// <summary>
        /// <c>components/schemas</c>：提升的 <c>$defs</c> 定义与被引用的根，按名字排序。
        /// </summary>
        public SortedDictionary<string, JsonDocument> Components { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// 被操作与端点引用的权限码。
        /// </summary>
        public SortedSet<string> ReferencedPermissions { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// 解析一段 JSON Schema。
        /// </summary>
        /// <remarks>
        /// 把 <c>#/$defs/</c> 引用改写为 <c>#/components/schemas/</c>，并把 <c>$defs</c> 提升到组件；为空返回 <see langword="null"/>。
        /// </remarks>
        public JsonDocument? Load(string? schema)
        {
            if (schema is null)
            {
                return null;
            }

            var document = JsonDocument.Parse(schema.Replace("\"#/$defs/", "\"#/components/schemas/", StringComparison.Ordinal));
            _documents.Add(document);
            if (document.RootElement.TryGetProperty("$defs", out var definitions))
            {
                foreach (var definition in definitions.EnumerateObject())
                {
                    // 同名定义按先到者为准：同一类型在多个操作里出现时内容相同
                    if (!Components.ContainsKey(definition.Name))
                    {
                        Components[definition.Name] = JsonDocument.Parse(definition.Value.GetRawText());
                    }
                }
            }

            return document;
        }

        /// <summary>
        /// 操作的某个根是否已提升到组件。
        /// </summary>
        /// <remarks>
        /// 是则给出组件名。
        /// </remarks>
        public bool Promoted(OperationDescriptor descriptor, SchemaRole role, out string? name)
            => _promoted.TryGetValue((descriptor.Id, role), out name);

        /// <summary>
        /// 组件里的 Schema 文档。
        /// </summary>
        public JsonDocument Component(string name) => Components[name];

        /// <summary>
        /// 解析普通端点的 JSON 类型到某个已登记操作的根 Schema。
        /// </summary>
        /// <remarks>
        /// 显式声明优先，否则按类型与操作的请求 / 响应类型匹配（取 ID 最小且有 Schema 的操作）；把该根提升到组件并返回组件名；引用不到抛 <see cref="HttpErrors.OpenApiSchemaUnresolved"/>。
        /// </remarks>
        public string ResolveJson(EndpointDescription endpoint, SchemaRole role, Type? type)
        {
            var declared = role == SchemaRole.Request ? endpoint.SchemaReference?.RequestOperationId : endpoint.SchemaReference?.ResponseOperationId;
            OperationDescriptor? descriptor;
            if (declared is not null)
            {
                descriptor = Source.Registry.Find(declared).TryGetValue(out var found) && found.Schema is not null ? found : null;
                if (descriptor is null)
                {
                    throw new FrameworkException(HttpErrors.OpenApiSchemaUnresolved, $"Endpoint '{endpoint.DisplayName}' declares its {Describe(role)} schema as operation '{declared}', which is not registered or has no schema.");
                }
            }
            else
            {
                descriptor = type is null
                    ? null
                    : Source.Registry.All().FirstOrDefault(d => d.Schema is not null && (role == SchemaRole.Request ? d.RequestType : d.ResponseType) == type);
                if (descriptor is null)
                {
                    throw new FrameworkException(HttpErrors.OpenApiSchemaUnresolved, $"Endpoint '{endpoint.DisplayName}' has a JSON {Describe(role)} of type '{type?.FullName ?? "(none)"}' that matches no registered operation's {Describe(role)} type; declare it with WithXiHanOperationSchema or register an operation with that contract.");
                }
            }

            return Promote(descriptor, role);
        }

        /// <summary>
        /// 把操作的根 Schema 提升到组件（已提升则直接返回）。
        /// </summary>
        /// <remarks>
        /// 名字取 Schema 的 <c>title</c>，没有则按操作 ID 拼。
        /// </remarks>
        private string Promote(OperationDescriptor descriptor, SchemaRole role)
        {
            if (_promoted.TryGetValue((descriptor.Id, role), out var existing))
            {
                return existing;
            }

            var document = Load(role == SchemaRole.Request ? descriptor.Schema!.Request : descriptor.Schema!.Response)!;
            var name = document.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String && title.GetString() is { Length: > 0 } text
                ? text
                : descriptor.Id.Replace('.', '_').Replace('-', '_') + (role == SchemaRole.Request ? "_request" : "_response");
            // 根提升时去掉 $schema 与已提升的 $defs；同名组件先到者为准（同一类型的根内容相同）
            if (!Components.ContainsKey(name))
            {
                using var stripped = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stripped))
                {
                    WriteStripped(writer, document.RootElement);
                }

                Components[name] = JsonDocument.Parse(stripped.ToArray());
            }

            _promoted[(descriptor.Id, role)] = name;
            return name;
        }

        /// <summary>
        /// 角色的英文名，用于错误说明。
        /// </summary>
        private static string Describe(SchemaRole role) => role == SchemaRole.Request ? "request" : "response";

        /// <summary>
        /// 某模板下的方法表。
        /// </summary>
        private static SortedDictionary<string, Action<Utf8JsonWriter>> Methods(SortedDictionary<string, SortedDictionary<string, Action<Utf8JsonWriter>>> paths, string template)
        {
            if (!paths.TryGetValue(template, out var methods))
            {
                methods = new SortedDictionary<string, Action<Utf8JsonWriter>>(StringComparer.Ordinal);
                paths[template] = methods;
            }

            return methods;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var document in _documents)
            {
                document.Dispose();
            }

            foreach (var document in Components.Values)
            {
                document.Dispose();
            }
        }
    }
}
