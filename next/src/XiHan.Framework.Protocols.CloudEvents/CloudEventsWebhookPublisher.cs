// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents;

/// <summary>
/// 出站 Webhook 发布者：把信封以 CloudEvents（binary 或 structured）POST 到主题配置的 URL（<c>XiHan:Protocols:CloudEvents:Webhooks:&lt;主题&gt;</c>）。
/// 作为具体类型注册，不替换消息代理的 <see cref="IMessagePublisher"/>；非 2xx 与连接失败都是 UNAVAILABLE，由调用方决定重试（例如经发件箱）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CloudEventsWebhookPublisher : IMessagePublisher
{
    /// <summary>
    /// 使用的命名 HttpClient；应用可按此名配置超时、重试与基地址。
    /// </summary>
    public const string HttpClientName = "xihan.protocols.cloudevents";

    /// <summary>
    /// HttpClient 工厂。
    /// </summary>
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly CloudEventsProtocolOptions _options;

    /// <summary>
    /// 创建。
    /// </summary>
    public CloudEventsWebhookPublisher(IHttpClientFactory httpClientFactory, CloudEventsProtocolOptions options)
    {
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
            return Error.NotFound(CloudEventsErrors.WebhookMissing, CloudEventsMessages.WebhookMissing, MessageArguments.Of(("topic", topic)));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        CloudEventsHttpBinding.Write(envelope, request, _options.Outbound);
        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? Result.Success()
                : Error.Unavailable(CloudEventsErrors.WebhookFailed, CloudEventsMessages.WebhookFailed, MessageArguments.Of(("topic", topic), ("status", (int)response.StatusCode)));
        }
        catch (HttpRequestException exception)
        {
            return Error.Unavailable(CloudEventsErrors.WebhookUnreachable, CloudEventsMessages.WebhookUnreachable, MessageArguments.Of(("topic", topic), ("reason", exception.Message)));
        }
    }
}
