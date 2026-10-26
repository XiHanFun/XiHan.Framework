// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// RFC 9457 Problem Details 的响应体。<c>code</c>、<c>traceId</c>、<c>errors</c>、<c>details</c> 是扩展成员：
/// <c>errors</c> 只在 VALIDATION 种类的错误上出现，键是字段键（<see cref="SchemaViolation.Field"/>），值是该字段的消息数组；
/// <c>details</c> 是 <see cref="Error.Details"/> 里的标量（字符串、布尔、数值、<see cref="Guid"/>、时间），按白名单输出，集合与对象不写。
/// </summary>
/// <param name="Type">问题类型 URI：<c>urn:xihan:error:&lt;错误码&gt;</c>。</param>
/// <param name="Title">错误码的 KIND 段。</param>
/// <param name="Status">HTTP 状态码。</param>
/// <param name="Detail">错误说明；装配了本地化时是调用方区域的文案，否则是 <see cref="Error.Message"/>。</param>
/// <param name="Instance">请求路径。</param>
/// <param name="Code">错误码。</param>
/// <param name="TraceId">调用的 trace-id。</param>
/// <param name="Errors">字段级错误；没有时为 <see langword="null"/>。</param>
/// <param name="Details">标量细节；没有时为 <see langword="null"/>。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record ProblemResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Instance,
    string Code,
    string TraceId,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors = null,
    IReadOnlyDictionary<string, object?>? Details = null)
{
    /// <summary>
    /// 细节里保留给字段级错误的键。
    /// </summary>
    public const string ErrorsKey = "errors";

    /// <summary>
    /// 从错误构造响应体；<paramref name="detail"/> 给出时作为本地化后的说明，否则用 <see cref="Error.Message"/>；
    /// <paramref name="translate"/> 把一条违规翻译成字段消息（宿主按调用方区域取文案），不给时用违规的原因文本。
    /// </summary>
    public static ProblemResponse FromError(Error error, string instance, string traceId, string? detail = null, Func<SchemaViolation, string>? translate = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(traceId);
        return new ProblemResponse(
            $"urn:xihan:error:{error.Code}",
            error.Kind,
            StatusCodeFor(error),
            detail ?? error.Message,
            instance,
            error.Code,
            traceId,
            FieldErrorsOf(error, translate ?? (static violation => violation.Reason)),
            ScalarDetailsOf(error));
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
            // SECURITY_DENIED_001 是“未认证”，按语义给 401；其余 DENIED 都是已认证但无权限，给 403
            "DENIED" => string.Equals(error.Code, "SECURITY_DENIED_001", StringComparison.Ordinal) ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden,
            "UNAVAILABLE" => StatusCodes.Status503ServiceUnavailable,
            "TIMEOUT" => StatusCodes.Status504GatewayTimeout,
            "CANCELLED" => StatusCodes.Status408RequestTimeout,
            // 未知 KIND（含 UNEXPECTED 与内核 HOSTING 等）一律视为服务端错误
            _ => StatusCodes.Status500InternalServerError,
        };
    }

    /// <summary>
    /// VALIDATION 错误的字段级错误：细节 <c>errors</c> 里的违规按字段键分组（保持出现顺序），每条经 <paramref name="translate"/> 成文案；其他种类或没有违规为 <see langword="null"/>。
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldErrorsOf(Error error, Func<SchemaViolation, string> translate)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(translate);
        if (!string.Equals(error.Kind, "VALIDATION", StringComparison.Ordinal)
            || error.Details is null
            || !error.Details.TryGetValue(ErrorsKey, out var raw)
            || raw is not IReadOnlyList<SchemaViolation> { Count: > 0 } violations)
        {
            return null;
        }

        var errors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var violation in violations)
        {
            if (!errors.TryGetValue(violation.Field, out var messages))
            {
                messages = new List<string>();
                errors[violation.Field] = messages;
                order.Add(violation.Field);
            }

            ((List<string>)messages).Add(translate(violation));
        }

        // Dictionary 不保证顺序：按首次出现的字段序重建，让响应里的字段顺序与载荷一致
        return order.ToDictionary(static field => field, field => errors[field], StringComparer.Ordinal);
    }

    /// <summary>
    /// 细节里的标量：字符串、布尔、整数与浮点、<see cref="decimal"/>、<see cref="Guid"/>、<see cref="DateTimeOffset"/> / <see cref="DateTime"/> / <see cref="TimeSpan"/>、字符；其余（集合、对象、违规列表）不收，<see cref="ErrorsKey"/> 也不收。没有标量为 <see langword="null"/>。
    /// </summary>
    public static IReadOnlyDictionary<string, object?>? ScalarDetailsOf(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error.Details is null)
        {
            return null;
        }

        Dictionary<string, object?>? scalars = null;
        foreach (var (name, value) in error.Details)
        {
            if (string.Equals(name, ErrorsKey, StringComparison.Ordinal) || !IsScalar(value))
            {
                continue;
            }

            scalars ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            scalars[name] = value;
        }

        return scalars;
    }

    /// <summary>
    /// 按 RFC 9457 写出响应体：七个标准 / 已有成员，再按有无写 <c>errors</c> 与 <c>details</c>。
    /// </summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteString("title", Title);
        writer.WriteNumber("status", Status);
        writer.WriteString("detail", Detail);
        writer.WriteString("instance", Instance);
        writer.WriteString("code", Code);
        writer.WriteString("traceId", TraceId);
        if (Errors is { Count: > 0 })
        {
            writer.WriteStartObject("errors");
            foreach (var (field, messages) in Errors)
            {
                writer.WriteStartArray(field);
                foreach (var message in messages)
                {
                    writer.WriteStringValue(message);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        if (Details is { Count: > 0 })
        {
            writer.WriteStartObject("details");
            foreach (var (name, value) in Details)
            {
                WriteScalar(writer, name, value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 值是否属于白名单标量。
    /// </summary>
    private static bool IsScalar(object? value)
        => value is null or string or bool or int or long or short or byte or sbyte or uint or ulong or ushort or double or float or decimal or Guid or DateTimeOffset or DateTime or TimeSpan or char;

    /// <summary>
    /// 写一个标量成员：数值按数值、布尔按布尔、其余按字符串（时间为 ISO 8601，时长为不变区域文本）。
    /// </summary>
    private static void WriteScalar(Utf8JsonWriter writer, string name, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNull(name);
                break;
            case string text:
                writer.WriteString(name, text);
                break;
            case bool flag:
                writer.WriteBoolean(name, flag);
                break;
            case int number:
                writer.WriteNumber(name, number);
                break;
            case long number:
                writer.WriteNumber(name, number);
                break;
            case short number:
                writer.WriteNumber(name, number);
                break;
            case byte number:
                writer.WriteNumber(name, number);
                break;
            case sbyte number:
                writer.WriteNumber(name, number);
                break;
            case uint number:
                writer.WriteNumber(name, number);
                break;
            case ulong number:
                writer.WriteNumber(name, number);
                break;
            case ushort number:
                writer.WriteNumber(name, number);
                break;
            case double number:
                writer.WriteNumber(name, number);
                break;
            case float number:
                writer.WriteNumber(name, number);
                break;
            case decimal number:
                writer.WriteNumber(name, number);
                break;
            case Guid guid:
                writer.WriteString(name, guid);
                break;
            case DateTimeOffset time:
                writer.WriteString(name, time);
                break;
            case DateTime time:
                writer.WriteString(name, time);
                break;
            case TimeSpan duration:
                writer.WriteString(name, duration.ToString("c", System.Globalization.CultureInfo.InvariantCulture));
                break;
            case char character:
                writer.WriteString(name, character.ToString());
                break;
            default:
                // IsScalar 已过滤，这里只是让 switch 完整
                break;
        }
    }
}
