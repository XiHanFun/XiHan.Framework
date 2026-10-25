// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Idempotency;
using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.Redis.Tests;

/// <summary>
/// Redis Provider 的单元与宿主级测试：值编码、选项解析，以及与内存包并存时的服务分工。
/// </summary>
public sealed class RedisCachingTests
{
    [Fact]
    public void ValueCodec_RoundTripsAbsoluteAndSliding()
    {
        var payload = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("payload"));

        var absolute = RedisValueCodec.Encode(payload, sliding: null);
        var sliding = RedisValueCodec.Encode(payload, TimeSpan.FromSeconds(90));

        Assert.Equal(0, absolute[0]);
        Assert.Equal(1, sliding[0]);
        Assert.Equal(payload.Length + 1, absolute.Length);
        Assert.Equal(payload.Length + 9, sliding.Length);

        var (absolutePayload, noWindow) = RedisValueCodec.Decode(absolute);
        var (slidingPayload, window) = RedisValueCodec.Decode(sliding);
        Assert.True(payload.Span.SequenceEqual(absolutePayload.Span));
        Assert.True(payload.Span.SequenceEqual(slidingPayload.Span));
        Assert.Null(noWindow);
        Assert.Equal(TimeSpan.FromSeconds(90), window);
        Assert.Throws<InvalidOperationException>(static () => RedisValueCodec.Decode([]));
        Assert.Throws<InvalidOperationException>(static () => RedisValueCodec.Decode([9, 1, 2]));
    }

    [Fact]
    public void Options_FromConfigurationAndBinding_PreferConfiguration()
    {
        // 用内存键值对构建配置
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();
        var binding = Maybe.Some(new Kernel.Hosting.BindingValue("cache", "cache:distributed", "bound:6379", new Dictionary<string, string>()));

        Assert.Equal("bound:6379", RedisCachingOptions.FromConfiguration(Build(), binding).Configuration);
        var configured = RedisCachingOptions.FromConfiguration(Build(("XiHan:Caching:Redis:Configuration", "cfg:6379"), ("XiHan:Caching:Redis:KeyPrefix", "app:"), ("XiHan:Caching:Redis:DefaultTimeToLiveSeconds", "60")), binding);
        Assert.Equal("cfg:6379", configured.Configuration);
        Assert.Equal("app:", configured.KeyPrefix);
        Assert.Equal(TimeSpan.FromMinutes(1), configured.DefaultTimeToLive);

        Assert.Throws<ArgumentException>(() => RedisCachingOptions.FromConfiguration(Build(), default));
        Assert.Throws<ArgumentException>(() => RedisCachingOptions.FromConfiguration(Build(("XiHan:Caching:Redis:DefaultTimeToLiveSeconds", "-1")), binding));
        Assert.Throws<ArgumentException>(static () => new RedisCachingOptions { Configuration = " " });
    }

    [Fact]
    public async Task Feature_TakesOverSharedCache_WhileInMemoryKeepsLocal()
    {
        var configuration = RedisTestServer.RequireConfiguration();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryCachingFeature>().UseFeature<RedisCachingFeature>(),
            [
                new("XiHan:Caching:Redis:Configuration", configuration),
                new("XiHan:Caching:Redis:KeyPrefix", $"xihan-test:{Guid.NewGuid():N}:"),
            ]);

        Assert.IsType<RedisCacheStore>(host.Services.GetRequiredService<ISharedCache>().Store);
        Assert.IsType<InMemoryCacheStore>(host.Services.GetRequiredService<ICache>().Store);
        Assert.IsType<StoreDistributedLock>(host.Services.GetRequiredService<IDistributedLock>());
        Assert.Equal(1, host.App.DescribePipeline().Split(nameof(IdempotencyMiddleware)).Length - 1);

        var executions = 0;
        var baggage = new Dictionary<string, string> { [BaggageKeys.IdempotencyKey] = $"redis-{Guid.NewGuid():N}" };
        var first = await host.InvokeAsync(ctx => { executions++; ctx.SetOutcome(Error.Literal("ORDERS_VALIDATION_001", "Rejected.")); return ValueTask.CompletedTask; }, baggage: baggage);
        var second = await host.InvokeAsync(ctx => { executions++; return ValueTask.CompletedTask; }, baggage: baggage);

        Assert.Equal(1, executions);
        Assert.Equal(first.Error?.Code, second.Error?.Code);
    }
}
