// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.CloudEvents.Inbound;

namespace XiHan.Framework.Protocols.CloudEvents.Outbound;

/// <summary>
/// 出站 Webhook 发布者
/// </summary>
/// <remarks>
/// 把信封以 CloudEvents（binary 或 structured）POST 到主题配置的 URL（<c>XiHan:Protocols:CloudEvents:Webhooks:&lt;主题&gt;</c>）；批量发布多条时用 batch 内容模式一次 POST，响应对整批生效。作为具体类型注册，不替换消息代理的 <see cref="IMessagePublisher"/>；非 2xx 与连接失败都是 UNAVAILABLE，由调用方决定重试（例如经发件箱）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class CloudEventsWebhookPublisher : IMessagePublisher
{
    /// <summary>
    /// 使用的命名 HttpClient
    /// </summary>
    /// <remarks>
    /// 应用可按此名配置超时、重试与基地址。主处理器缺省是过出站地址策略（<see cref="OutboundAddressPolicy"/>）、不写追踪上下文头（<see cref="CloudEventsProtocolOptions.PropagateTraceContext"/>）的 <see cref="SocketsHttpHandler"/>；调连接参数用 <c>UseSocketsHttpHandler</c> 配置同一个处理器，整个替换主处理器则由替换后的处理器决定能连哪里、是否传播
    /// </remarks>
    public const string HttpClientName = "xihan.protocols.cloudevents";

    /// <summary>
    /// HttpClient 工厂
    /// </summary>
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// 选项
    /// </summary>
    private readonly CloudEventsProtocolOptions _options;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建
    /// </summary>
    public CloudEventsWebhookPublisher(IHttpClientFactory httpClientFactory, CloudEventsProtocolOptions options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<CloudEventsWebhookPublisher>();
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        TopicName.Validate(topic);
        ArgumentNullException.ThrowIfNull(envelope);
        if (!_options.Webhooks.TryGetValue(topic, out var url))
        {
            return WebhookMissing(topic);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        CloudEventsHttpBinding.Write(envelope, request, _options.Outbound);
        return await SendAsync(request, topic, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        TopicName.Validate(topic);
        ArgumentNullException.ThrowIfNull(envelopes);
        if (envelopes.Count == 0)
        {
            return [];
        }

        // 单条沿用配置的 binary / structured 模式；多条用 batch 内容模式一次 POST（接收方须支持 application/cloudevents-batch+json），一个响应对整批生效
        if (envelopes.Count == 1)
        {
            return [await PublishAsync(topic, envelopes[0], cancellationToken).ConfigureAwait(false)];
        }

        if (!_options.Webhooks.TryGetValue(topic, out var url))
        {
            return Repeat(WebhookMissing(topic), envelopes.Count);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        CloudEventsHttpBinding.WriteBatch(envelopes, request);
        return Repeat(await SendAsync(request, topic, cancellationToken).ConfigureAwait(false), envelopes.Count);
    }

    /// <summary>
    /// 发送请求：2xx 成功，其余状态码为 Webhook 失败，连不上为不可达
    /// </summary>
    /// <remarks>
    /// 结果只看状态码：读到响应头即判定，响应体一个字节也不读，接收方返回的正文再大也不进内存（ADR-0188）
    /// </remarks>
    private async Task<Result> SendAsync(HttpRequestMessage request, string topic, CancellationToken cancellationToken)
    {
        try
        {
            // 不缓冲响应体：释放响应时连接随之中止或回池
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? Result.Success()
                : Error.Unavailable(CloudEventsErrors.WebhookFailed, CloudEventsMessages.WebhookFailed, MessageArguments.Of(("topic", topic), ("status", (int)response.StatusCode)));
        }
        catch (HttpRequestException exception)
        {
            // 连接异常的消息常带订阅方的主机与端口：reason 只写类型，完整异常只进日志；地址被策略拒绝时写拒绝的类型，运维一眼能看出不是对方的问题
            LogWebhookUnreachable(_logger, topic, exception);
            var type = exception.InnerException is OutboundAddressDeniedException denied ? denied.GetType() : exception.GetType();
            return Error.Unavailable(CloudEventsErrors.WebhookUnreachable, CloudEventsMessages.WebhookUnreachable, MessageArguments.Of(("topic", topic), ("reason", type.FullName ?? type.Name)));
        }
    }

    /// <summary>
    /// 构造主题没有配置 Webhook 的错误
    /// </summary>
    private static Error WebhookMissing(string topic) => Error.NotFound(CloudEventsErrors.WebhookMissing, CloudEventsMessages.WebhookMissing, MessageArguments.Of(("topic", topic)));

    /// <summary>
    /// 同一结果重复给每一项（一次 POST 的响应对整批生效）
    /// </summary>
    private static Result[] Repeat(Result result, int count)
    {
        var results = new Result[count];
        Array.Fill(results, result);
        return results;
    }

    /// <summary>
    /// Webhook 连不上
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The CloudEvents webhook of topic '{Topic}' could not be reached.")]
    private static partial void LogWebhookUnreachable(ILogger logger, string topic, Exception exception);
}
