// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// RFC 9457 Problem Details 的响应体。<c>code</c> 与 <c>traceId</c> 是扩展成员。
/// </summary>
/// <param name="Type">问题类型 URI：<c>urn:xihan:error:&lt;错误码&gt;</c>。</param>
/// <param name="Title">错误码的 KIND 段。</param>
/// <param name="Status">HTTP 状态码。</param>
/// <param name="Detail">错误消息。</param>
/// <param name="Instance">请求路径。</param>
/// <param name="Code">错误码。</param>
/// <param name="TraceId">调用的 trace-id。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record ProblemResponse(string Type, string Title, int Status, string Detail, string Instance, string Code, string TraceId)
{
    /// <summary>
    /// 从错误构造响应体。
    /// </summary>
    public static ProblemResponse FromError(Error error, string instance, string traceId)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(traceId);
        return new ProblemResponse($"urn:xihan:error:{error.Code}", error.Kind, StatusCodeFor(error), error.Message, instance, error.Code, traceId);
    }

    /// <summary>
    /// 错误码 KIND 段到 HTTP 状态码的映射。
    /// </summary>
    public static int StatusCodeFor(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Kind switch
        {
            "VALIDATION" => StatusCodes.Status400BadRequest,
            "NOTFOUND" => StatusCodes.Status404NotFound,
            "CONFLICT" => StatusCodes.Status409Conflict,
            "DENIED" => string.Equals(error.Code, "SECURITY_DENIED_001", StringComparison.Ordinal) ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden,
            "UNAVAILABLE" => StatusCodes.Status503ServiceUnavailable,
            "TIMEOUT" => StatusCodes.Status504GatewayTimeout,
            "CANCELLED" => StatusCodes.Status408RequestTimeout,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}

/// <summary>
/// 探针端点的响应体。
/// </summary>
/// <param name="Status"><c>pass</c> 或 <c>fail</c>。</param>
/// <param name="State">应用聚合状态。</param>
/// <param name="Features">各特性状态。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record HealthResponse(string Status, string State, IReadOnlyList<FeatureHealth> Features);

/// <summary>
/// 探针端点中的单个特性状态。
/// </summary>
/// <param name="Id">清单 ID。</param>
/// <param name="State">生命周期状态。</param>
/// <param name="Error">失败原因的错误码。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record FeatureHealth(string Id, string State, string? Error);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProblemResponse))]
[JsonSerializable(typeof(HealthResponse))]
internal sealed partial class HostingJsonContext : JsonSerializerContext;
