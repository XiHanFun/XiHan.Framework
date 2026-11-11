// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Conformance;

namespace XiHan.Framework.Messaging.Redis.Tests;

/// <summary>
/// 让 <see cref="RedisMessagingFeature"/> 对真实 Redis 跑完整套队列用例
/// </summary>
/// <remarks>
/// 未设置 <c>XIHAN_TEST_REDIS</c> 时跳过。每个用例一个独立的键前缀，结束时删掉前缀下的流
/// </remarks>
public sealed class RedisMessageBrokerConformanceTests : MessageBrokerConformance, IAsyncDisposable
{
    /// <summary>
    /// 本用例的键前缀
    /// </summary>
    private readonly string _prefix = RedisTestServer.NewPrefix();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Messaging:Redis:Configuration", RedisTestServer.Configuration),
        new("XiHan:Messaging:Redis:KeyPrefix", _prefix),
    ];

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        RedisTestServer.RequireConfiguration();
        return new RedisMessagingFeature();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await RedisTestServer.DeleteKeysAsync(_prefix);
}
