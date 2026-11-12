// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// HTTP 请求
/// </summary>
/// <remarks>
/// <c>Url</c>（模板）、<c>Method</c>（默认 GET）、<c>Headers</c>（对象，值为模板）、<c>Body</c>（字符串模板或对象 → JSON）、<c>ContentType</c>、<c>TimeoutSeconds</c>、<c>ResultVariable</c>（默认 <c>http</c>）、<c>FailOnErrorStatus</c>（默认 <see langword="true"/>）。输出 <c>{ status, headers, body }</c>，JSON 响应解析为对象
/// 超时按引擎的时钟计时、按令牌归因（RFC-0021「HTTP 活动的超时与取消」，ADR-0097）：活动的超时令牌已触发即节点故障（<see cref="WorkflowErrors.ActivityFailed"/>，消息键 <see cref="WorkflowMessages.HttpTimedOut"/>），不论调用方是否也已取消、客户端是抛出还是交回了响应；只有调用方取消时抛出 <see cref="OperationCanceledException"/>
/// 地址由流程定义给出、不可信：缺省的客户端过出站地址策略（ADR-0117），目标被拒绝是节点故障（<see cref="WorkflowErrors.ActivityFailed"/>，消息键 <see cref="WorkflowMessages.HttpAddressDenied"/>）
/// 对端也不可信（ADR-0181）：正文在超时之内边读边计数，保留前 4 MiB、读到第 4 MiB 多一个字节即停并标 <c>truncated</c>；<c>TimeoutSeconds</c> 不超过引擎选项 <c>MaxHttpTimeoutSeconds</c>，不是正的秒数或超过上限都是属性错误（<see cref="WorkflowErrors.InputInvalid"/>）
/// </remarks>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed partial class HttpActivity : IActivity
{
    /// <summary>
    /// 属性：URL（模板）
    /// </summary>
    public const string UrlProperty = "Url";

    /// <summary>
    /// 属性：方法
    /// </summary>
    public const string MethodProperty = "Method";

    /// <summary>
    /// 属性：请求头（对象）
    /// </summary>
    public const string HeadersProperty = "Headers";

    /// <summary>
    /// 属性：请求体
    /// </summary>
    public const string BodyProperty = "Body";

    /// <summary>
    /// 属性
    /// </summary>
    /// <remarks>
    /// 内容类型（默认按请求体推断：对象为 <c>application/json</c>，字符串为 <c>text/plain</c>）
    /// </remarks>
    public const string ContentTypeProperty = "ContentType";

    /// <summary>
    /// 属性：超时秒数
    /// </summary>
    /// <remarks>
    /// 缺省取引擎选项 <c>HttpTimeoutSeconds</c>；写了不超过 <c>MaxHttpTimeoutSeconds</c>
    /// </remarks>
    public const string TimeoutSecondsProperty = "TimeoutSeconds";

    /// <summary>
    /// 属性：结果写入的变量名
    /// </summary>
    public const string ResultVariableProperty = "ResultVariable";

    /// <summary>
    /// 属性：非 2xx 状态是否视为节点故障
    /// </summary>
    public const string FailOnErrorStatusProperty = "FailOnErrorStatus";

    /// <summary>
    /// 默认结果变量
    /// </summary>
    public const string DefaultResultVariable = "http";

    /// <summary>
    /// 响应体最多保留的字节数：防止把超大响应装进实例变量
    /// </summary>
    private const int MaxBodyBytes = 4 * 1024 * 1024;

    /// <summary>
    /// 读正文的初始缓冲上限
    /// </summary>
    private const int InitialBodyBuffer = 64 * 1024;

    /// <summary>
    /// 允许的方法
    /// </summary>
    private static readonly string[] _methods = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    /// <summary>
    /// 方法名是否合法
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
            return ActivityFailures.NotUrl(UrlProperty, address);
        }

        var method = context.Property<string>(MethodProperty) ?? "GET";
        if (!IsValidMethod(method))
        {
            return ActivityFailures.NotHttpMethod(MethodProperty, method);
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
                    return ActivityFailures.HeaderInvalid(HeadersProperty, name);
                }
            }
        }

        if (BuildBody(context, out var content) is { } bodyError)
        {
            return bodyError;
        }

        request.Content = content;
        // 定义可能早于上限发布、或校验之后宿主调低了上限：发请求之前再核对一次
        var options = context.Services.GetRequiredService<WorkflowOptions>();
        if (CheckTimeout(context.Property(TimeoutSecondsProperty), options.MaxHttpTimeout, out var requested) is { } timeoutError)
        {
            return timeoutError;
        }

        var timeout = requested ?? options.HttpTimeout;
        var client = context.Services.GetRequiredService<WorkflowHttpClient>();
        // 活动自己持有超时令牌：按引擎的时钟计时，与调用方令牌链接后交给客户端，归因只看这两个令牌
        using var timer = new CancellationTokenSource(timeout, context.Clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, linked.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsTimeout(exception, timer.Token, cancellationToken))
        {
            return TimedOut();
        }
        catch (HttpRequestException exception) when (exception.InnerException is OutboundAddressDeniedException)
        {
            // 策略已经记了日志（带解析出的地址）；故障会经接口返回，只写请求里的主机名，这是调用方自己给的
            return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.HttpAddressDenied, details: MessageArguments.Of(("host", uri.Host)));
        }
        catch (HttpRequestException exception)
        {
            // 故障存进实例、经接口返回，连接异常的消息常带目标主机与端口：reason 只写类型，完整异常只进日志
            LogRequestFailed(context.Services.GetRequiredService<ILoggerFactory>().CreateLogger<HttpActivity>(), exception);
            var type = exception.GetType();
            return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.HttpFailed, details: MessageArguments.Of(("status", 0), ("reason", type.FullName ?? type.Name)));
        }

        using (response)
        {
            // 写成结果：应用替换的处理器可能接住了取消、交回一个响应，只看响应分不出来，按令牌归因——
            // 超时令牌已触发即超时；只有调用方取消时不把响应交出去、抛出取消，与抛出路径同一个回答
            if (timer.IsCancellationRequested)
            {
                return TimedOut();
            }

            cancellationToken.ThrowIfCancellationRequested();
            // 客户端读到响应头即交回：正文在同一个时限里读，归因与发送一致
            JsonObject result;
            try
            {
                result = await ReadResultAsync(response, linked.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsTimeout(exception, timer.Token, cancellationToken))
            {
                return TimedOut();
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                // 正文读到一半连接断开：与发送失败同一种故障，reason 只写类型
                LogRequestFailed(context.Services.GetRequiredService<ILoggerFactory>().CreateLogger<HttpActivity>(), exception);
                var type = exception.GetType();
                return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.HttpFailed, details: MessageArguments.Of(("status", 0), ("reason", type.FullName ?? type.Name)));
            }

            // 读正文的流可能接住了取消、照常结束，同发送一样按令牌归因
            if (timer.IsCancellationRequested)
            {
                return TimedOut();
            }

            cancellationToken.ThrowIfCancellationRequested();
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
    /// 客户端抛出的异常是否按超时报告
    /// </summary>
    /// <remarks>
    /// 返回 false 的取消是调用方的取消，原样抛出；其余异常由调用处按请求失败报告或原样抛出
    /// </remarks>
    private static bool IsTimeout(Exception exception, CancellationToken timer, CancellationToken caller)
    {
        if (exception is TimeoutException)
        {
            return true;
        }

        if (exception is not OperationCanceledException)
        {
            return false;
        }

        // 超时令牌已触发即超时，不论客户端报的是哪个令牌、调用方是否也已取消：两个都触发时 HttpClient 报的是传给它的链接令牌，
        // 按「调用方令牌是否已触发」推断就会把同一个超时随调度报成取消
        if (timer.IsCancellationRequested)
        {
            return true;
        }

        // 只有调用方取消：是取消，原样交给引擎（整个操作随调用回滚）。两个都没触发：客户端自己的超时（如应用替换的 HttpClient 设了 Timeout）
        return !caller.IsCancellationRequested;
    }

    /// <summary>
    /// 超时的节点故障
    /// </summary>
    private static Error TimedOut() => Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.HttpTimedOut);

    /// <summary>
    /// 组装请求体
    /// </summary>
    /// <remarks>
    /// 字符串按模板渲染，对象序列化为 JSON；没有请求体时 <paramref name="content"/> 为空。返回属性错误，成功为空
    /// </remarks>
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
            return ActivityFailures.NotMediaType(ContentTypeProperty, contentType);
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
    /// 读响应：状态、头与正文（JSON 解析为节点，其余为字符串，超过上限截断）
    /// </summary>
    private static async Task<JsonObject> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var headers = new JsonObject();
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        var (bytes, length) = await ReadBodyAsync(response.Content, cancellationToken).ConfigureAwait(false);
        var truncated = length > MaxBodyBytes;
        var text = Encoding.UTF8.GetString(bytes, 0, Math.Min(length, MaxBodyBytes));
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

    /// <summary>
    /// 读正文，最多读到上限多一个字节
    /// </summary>
    /// <remarks>
    /// 返回缓冲与读到的字节数；字节数大于 <see cref="MaxBodyBytes"/> 表示还有没读的部分
    /// </remarks>
    private static async Task<(byte[] Bytes, int Length)> ReadBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
        // 多读的那一个字节只用来判断是否截断；其余不读，释放响应时连接随之中止，对端再大、再慢也只占这么多
        const int limit = MaxBodyBytes + 1;
        // Content-Length 由对端给出、不可信：初始缓冲另设上限，按实际读到的再扩
        var buffer = new byte[(int)Math.Clamp(content.Headers.ContentLength ?? InitialBodyBuffer, 1, Math.Min(limit, InitialBodyBuffer))];
        var length = 0;
        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            while (length < limit)
            {
                if (length == buffer.Length)
                {
                    Array.Resize(ref buffer, Math.Min(buffer.Length * 2, limit));
                }

                var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                length += read;
            }
        }

        return (buffer, length);
    }

    /// <summary>
    /// 核对 <c>TimeoutSeconds</c>
    /// </summary>
    /// <remarks>
    /// 没写时 <paramref name="timeout"/> 为空（取引擎缺省）；写了必须是正的有限秒数且不超过 <paramref name="max"/>。返回属性错误，合法为空。定义校验与活动执行共用
    /// </remarks>
    internal static Error? CheckTimeout(JsonNode? value, TimeSpan max, out TimeSpan? timeout)
    {
        timeout = null;
        if (value is null)
        {
            return null;
        }

        if (!UserTaskActivity.TryParseSeconds(value, out var seconds))
        {
            return ActivityFailures.NotSeconds(TimeoutSecondsProperty);
        }

        // 先按秒数比较再换算：极大的秒数换成 TimeSpan 会溢出
        if (seconds > max.TotalSeconds)
        {
            return ActivityFailures.SecondsOverLimit(TimeoutSecondsProperty, max);
        }

        timeout = TimeSpan.FromSeconds(seconds);
        return null;
    }

    /// <summary>
    /// HTTP 请求没有发出去或没有响应
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The HTTP activity could not complete its request.")]
    private static partial void LogRequestFailed(ILogger logger, Exception exception);
}
