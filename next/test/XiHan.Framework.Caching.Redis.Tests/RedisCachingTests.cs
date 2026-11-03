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
/// Redis Provider 的单元与宿主级测试
/// </summary>
/// <remarks>
/// 值编码、选项解析，以及与内存包并存时的服务分工
/// </remarks>
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
    public void Options_FromConfigurationAndBinding_PreferBinding()
    {
        // 用内存键值对构建配置
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();
        var binding = Maybe.Some(new Kernel.Hosting.BindingValue("cache", "cache:distributed", "bound:6379", new Dictionary<string, string>()));

        Assert.Equal("bound:6379", RedisCachingOptions.FromConfiguration(Build(), binding).Configuration);
        // 两者都给时取绑定的连接，其余键照读配置节
        var configured = RedisCachingOptions.FromConfiguration(Build(("XiHan:Caching:Redis:Configuration", "cfg:6379"), ("XiHan:Caching:Redis:KeyPrefix", "app:"), ("XiHan:Caching:Redis:DefaultTimeToLiveSeconds", "60")), binding);
        Assert.Equal("bound:6379", configured.Configuration);
        Assert.Equal("app:", configured.KeyPrefix);
        Assert.Equal("cfg:6379", RedisCachingOptions.FromConfiguration(Build(("XiHan:Caching:Redis:Configuration", "cfg:6379")), default).Configuration);
        // 主值为空的绑定不算给出了连接
        var empty = Maybe.Some(new Kernel.Hosting.BindingValue("cache", "cache:distributed", " ", new Dictionary<string, string>()));
        Assert.Equal("cfg:6379", RedisCachingOptions.FromConfiguration(Build(("XiHan:Caching:Redis:Configuration", "cfg:6379")), empty).Configuration);
        Assert.Equal(TimeSpan.FromMinutes(1), configured.DefaultTimeToLive);
        Assert.True(configured.PublishChanges);
        Assert.False(RedisCachingOptions.FromConfiguration(Build(("XiHan:Caching:Redis:PublishChanges", "false")), binding).PublishChanges);
        Assert.Throws<ArgumentException>(() => RedisCachingOptions.FromConfiguration(Build(("XiHan:Caching:Redis:PublishChanges", "maybe")), binding));

        Assert.Throws<ArgumentException>(() => RedisCachingOptions.FromConfiguration(Build(), default));
        Assert.Throws<ArgumentException>(() => RedisCachingOptions.FromConfiguration(Build(("XiHan:Caching:Redis:DefaultTimeToLiveSeconds", "-1")), binding));
        Assert.Throws<ArgumentException>(static () => new RedisCachingOptions { Configuration = " " });
    }

    [Fact]
    public void Options_BindingCredentials_OverrideTheConnectionString_WithoutRewritingIt()
    {
        // 用内存键值对构建配置
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();
        var configuration = Build(("XiHan:Caching:Redis:Configuration", "cfg:6379,user=app,password=from-config"));

        // 绑定只给凭据、不给主值：连接仍来自配置节，凭据逐项取绑定的；含逗号的密码不会把连接字符串拆错
        var credentials = Maybe.Some(new Kernel.Hosting.BindingValue("cache", "cache:distributed", null, new Dictionary<string, string> { ["username"] = "ops", ["password"] = "p,w=1" }));
        var options = RedisCachingOptions.FromConfiguration(configuration, credentials);
        Assert.Equal("cfg:6379,user=app,password=from-config", options.Configuration);
        Assert.Equal("ops", options.User);
        Assert.Equal("p,w=1", options.Password);
        var connection = RedisCachingFeature.ConnectionOptions(options);
        Assert.Equal("ops", connection.User);
        Assert.Equal("p,w=1", connection.Password);
        Assert.StartsWith("cfg:6379,", connection.ToString(includePassword: false), StringComparison.Ordinal);

        // 只给密码：用户名照读连接字符串；空白的属性不算给出
        var passwordOnly = Maybe.Some(new Kernel.Hosting.BindingValue("cache", "cache:distributed", null, new Dictionary<string, string> { ["username"] = " ", ["password"] = "secret" }));
        var partial = RedisCachingFeature.ConnectionOptions(RedisCachingOptions.FromConfiguration(configuration, passwordOnly));
        Assert.Equal("app", partial.User);
        Assert.Equal("secret", partial.Password);

        // 没有绑定凭据：连接字符串里的照用
        var unbound = RedisCachingFeature.ConnectionOptions(RedisCachingOptions.FromConfiguration(configuration, default));
        Assert.Null(RedisCachingOptions.FromConfiguration(configuration, default).Password);
        Assert.Equal("app", unbound.User);
        Assert.Equal("from-config", unbound.Password);
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
