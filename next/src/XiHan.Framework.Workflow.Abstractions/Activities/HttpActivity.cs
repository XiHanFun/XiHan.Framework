// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// HTTP 请求：<c>Url</c>（模板）、<c>Method</c>（默认 GET）、<c>Headers</c>（对象，值为模板）、<c>Body</c>（字符串模板或对象 → JSON）、<c>ContentType</c>、<c>TimeoutSeconds</c>、<c>ResultVariable</c>（默认 <c>http</c>）、<c>FailOnErrorStatus</c>（默认 <see langword="true"/>）。输出 <c>{ status, headers, body }</c>，JSON 响应解析为对象。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class HttpActivity : IActivity
{
    /// <summary>属性：URL（模板）。</summary>
    public const string UrlProperty = "Url";

    /// <summary>属性：方法。</summary>
    public const string MethodProperty = "Method";

    /// <summary>属性：请求头（对象）。</summary>
    public const string HeadersProperty = "Headers";

    /// <summary>属性：请求体。</summary>
    public const string BodyProperty = "Body";

    /// <summary>属性：内容类型（默认按请求体推断：对象为 <c>application/json</c>，字符串为 <c>text/plain</c>）。</summary>
    public const string ContentTypeProperty = "ContentType";

    /// <summary>属性：超时秒数（默认取引擎选项 <c>HttpTimeoutSeconds</c>）。</summary>
    public const string TimeoutSecondsProperty = "TimeoutSeconds";

    /// <summary>属性：结果写入的变量名。</summary>
    public const string ResultVariableProperty = "ResultVariable";

    /// <summary>属性：非 2xx 状态是否视为节点故障。</summary>
    public const string FailOnErrorStatusProperty = "FailOnErrorStatus";

    /// <summary>默认结果变量。</summary>
    public const string DefaultResultVariable = "http";

    /// <summary>
    /// 响应体最大读取字节数：防止把超大响应装进实例变量。
    /// </summary>
    private const int MaxBodyBytes = 4 * 1024 * 1024;

    /// <summary>
    /// 允许的方法。
    /// </summary>
    private static readonly string[] _methods = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    /// <summary>
    /// 方法名是否合法。
    /// </summary>
    public static bool IsValidMethod(string? method) => method is null || Array.Exists(_methods, candidate => string.Equals(candidate, method, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public async ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var url = context.RenderProperty(UrlProperty);
        if (!url.TryGetValue(out var address))
        {
            return url.Error!;
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return ActivityContext.PropertyInvalid(UrlProperty, $"'{address}' is not an absolute HTTP URL");
        }

        var method = context.Property<string>(MethodProperty) ?? "GET";
        if (!IsValidMethod(method))
        {
            return ActivityContext.PropertyInvalid(MethodProperty, $"'{method}' is not a supported HTTP method");
        }

        using var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), uri);
        if (context.Property(HeadersProperty) is JsonObject headers)
        {
            foreach (var (name, value) in headers)
            {
                var rendered = value is JsonValue scalar ? context.Evaluator.Render(WorkflowValues.AsString(scalar), context.Scope()) : Result.Success(value?.ToJsonString() ?? string.Empty);
                if (!rendered.TryGetValue(out var text))
                {
                    return rendered.Error!;
                }

                // 内容类头放到内容上，其余放到请求上；无法解析的头名是属性错误
                if (!name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) && !request.Headers.TryAddWithoutValidation(name, text))
                {
                    return ActivityContext.PropertyInvalid(HeadersProperty, $"header '{name}' is invalid");
                }
            }
        }

        if (BuildBody(context, out var content) is { } bodyError)
        {
            return bodyError;
        }

        request.Content = content;
        var timeout = context.Property(TimeoutSecondsProperty) is { } timeoutNode && WorkflowValues.TryConvert<double>(timeoutNode, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : context.Services.GetRequiredService<WorkflowOptions>().HttpTimeout;
        var client = context.Services.GetRequiredService<WorkflowHttpClient>();
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.HttpFailed, details: MessageArguments.Of(("status", 0), ("reason", "timeout")));
        }
        catch (HttpRequestException exception)
        {
            return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.HttpFailed, details: MessageArguments.Of(("status", 0), ("reason", exception.Message)));
        }

        using (response)
        {
            var result = await ReadResultAsync(response, cancellationToken).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode && context.Property(FailOnErrorStatusProperty, true))
            {
                return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.HttpFailed, details: MessageArguments.Of(("status", status), ("reason", response.ReasonPhrase ?? string.Empty)));
            }

            var variable = context.Property<string>(ResultVariableProperty);
            var outputs = new JsonObject { [string.IsNullOrWhiteSpace(variable) ? DefaultResultVariable : variable] = result };
            return ActivityOutcome.Complete(outputs, response.IsSuccessStatusCode ? "success" : "error");
        }
    }

    /// <summary>
    /// 组装请求体：字符串按模板渲染，对象序列化为 JSON；没有请求体时 <paramref name="content"/> 为空。返回属性错误，成功为空。
    /// </summary>
    private static Error? BuildBody(ActivityContext context, out HttpContent? content)
    {
        content = null;
        var body = context.Property(BodyProperty);
        if (body is null)
        {
            return null;
        }

        var contentType = context.Property<string>(ContentTypeProperty);
        string text;
        if (body is JsonValue scalar && scalar.TryGetValue<string>(out var template))
        {
            var rendered = context.Evaluator.Render(template, context.Scope());
            if (!rendered.TryGetValue(out var renderedText))
            {
                return rendered.Error!;
            }

            text = renderedText;
            contentType ??= "text/plain";
        }
        else
        {
            text = body.ToJsonString();
            contentType ??= "application/json";
        }

        if (!MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
        {
            return ActivityContext.PropertyInvalid(ContentTypeProperty, $"'{contentType}' is not a media type");
        }

        var built = new StringContent(text, Encoding.UTF8);
        built.Headers.ContentType = mediaType;
        if (mediaType.CharSet is null)
        {
            built.Headers.ContentType.CharSet = "utf-8";
        }

        content = built;
        return null;
    }

    /// <summary>
    /// 读响应：状态、头与正文（JSON 解析为节点，其余为字符串，超过上限截断）。
    /// </summary>
    private static async Task<JsonObject> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var headers = new JsonObject();
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var truncated = bytes.Length > MaxBodyBytes;
        var text = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, MaxBodyBytes));
        JsonNode? body = text;
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!truncated && mediaType is not null && mediaType.Contains("json", StringComparison.OrdinalIgnoreCase) && text.Length > 0)
        {
            try
            {
                body = JsonNode.Parse(text);
            }
            catch (JsonException)
            {
                // 声明是 JSON 但解析不了：按文本给出
            }
        }

        return new JsonObject
        {
            ["status"] = (int)response.StatusCode,
            ["headers"] = headers,
            ["body"] = body,
            ["truncated"] = truncated,
        };
    }
}
