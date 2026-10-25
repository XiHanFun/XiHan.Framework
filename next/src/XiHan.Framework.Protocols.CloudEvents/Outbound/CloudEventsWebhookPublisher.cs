// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.CloudEvents.Inbound;

namespace XiHan.Framework.Protocols.CloudEvents.Outbound;

/// <summary>
/// 出站 Webhook 发布者：把信封以 CloudEvents（binary 或 structured）POST 到主题配置的 URL（<c>XiHan:Protocols:CloudEvents:Webhooks:&lt;主题&gt;</c>）；
/// 批量发布多条时用 batch 内容模式一次 POST，响应对整批生效。
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
    /// 发送请求：2xx 成功，其余状态码为 Webhook 失败，连不上为不可达。
    /// </summary>
    private async Task<Result> SendAsync(HttpRequestMessage request, string topic, CancellationToken cancellationToken)
    {
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

    /// <summary>
    /// 构造主题没有配置 Webhook 的错误。
    /// </summary>
    private static Error WebhookMissing(string topic) => Error.NotFound(CloudEventsErrors.WebhookMissing, CloudEventsMessages.WebhookMissing, MessageArguments.Of(("topic", topic)));

    /// <summary>
    /// 同一结果重复给每一项（一次 POST 的响应对整批生效）。
    /// </summary>
    private static Result[] Repeat(Result result, int count)
    {
        var results = new Result[count];
        Array.Fill(results, result);
        return results;
    }
}
