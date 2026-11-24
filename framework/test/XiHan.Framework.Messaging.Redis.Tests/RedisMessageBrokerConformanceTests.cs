// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.Redis.Tests;

/// <summary>
/// 让 <see cref="RedisMessagingFeature"/> 对真实 Redis 跑完整套队列用例
/// </summary>
/// <remarks>
/// 未设置 <c>XIHAN_TEST_REDIS</c> 时跳过。每个用例一个独立的键前缀，结束时删掉前缀下的流。回收阈值调到 5 秒：被停止打断的投递留在待确认列表里，要空闲过回收阈值才重新投递，缺省的 60 秒会让那条用例等一分多钟
/// </remarks>
public sealed class RedisMessageBrokerConformanceTests : MessageBrokerConformance, IAsyncDisposable
{
    /// <summary>
    /// 本用例的键前缀
    /// </summary>
    private readonly string _prefix = RedisTestServer.NewPrefix();

    /// <inheritdoc />
    /// <remarks>
    /// 回收在条目空闲过阈值（5 秒）之后的下一次维护（每 5 秒一次）进行，最迟十秒出头，留足余量
    /// </remarks>
    protected override TimeSpan RedeliveryTimeout => TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Messaging:Redis:Configuration", RedisTestServer.Configuration),
        new("XiHan:Messaging:Redis:KeyPrefix", _prefix),
        new("XiHan:Messaging:Redis:ClaimIdleTimeSeconds", "5"),
    ];

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        RedisTestServer.RequireConfiguration();
        return new RedisMessagingFeature();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 死信流是 <c>&lt;KeyPrefix&gt;&lt;topic&gt;&lt;DeadLetterSuffix&gt;</c>，条目的 <c>envelope</c> 字段是原消息。用测试的共享连接 <c>XRANGE</c> 整条流：不取走条目，键不存在时为空
    /// </remarks>
    protected override async ValueTask<IReadOnlyList<MessageEnvelope>> ReadDeadLettersAsync(TestHost host, string topic, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        var options = host.Services.GetRequiredService<RedisMessagingOptions>();
        var key = options.KeyPrefix + topic + host.Services.GetRequiredService<MessagingOptions>().DeadLetterSuffix;
        var entries = await RedisTestServer.Database().StreamRangeAsync(key).WaitAsync(cancellationToken);
        var envelopes = new List<MessageEnvelope>(entries.Length);
        foreach (var entry in entries)
        {
            var body = entry[RedisMessageBroker.EnvelopeField];
            // 无法解码的死信（原样转入的坏条目）不在套件的断言范围里，跳过
            if (!body.IsNull && CloudEventsFormat.Decode((byte[]?)body).TryGetValue(out var envelope))
            {
                envelopes.Add(envelope);
            }
        }

        return envelopes;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await RedisTestServer.DeleteKeysAsync(_prefix);
}
