// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 普通端点进 OpenAPI 文档时的声明。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class OpenApiEndpointConventionBuilderExtensions
{
    /// <summary>
    /// 声明端点的 JSON 请求体 / 响应正文同某个已登记操作的请求 / 响应 Schema（至少给一个）；不声明时按类型与已登记操作自动匹配，匹配不到在生成文档时以 <see cref="HttpErrors.OpenApiSchemaUnresolved"/> 失败。
    /// </summary>
    /// <param name="builder">端点约定构建器。</param>
    /// <param name="request">请求体同哪个操作的请求 Schema。</param>
    /// <param name="response">响应正文同哪个操作的响应 Schema。</param>
    public static TBuilder WithXiHanOperationSchema<TBuilder>(this TBuilder builder, string? request = null, string? response = null)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new OperationSchemaReference(request, response));
    }
}
