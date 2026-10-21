// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Caching.Abstractions.Tests;

/// <summary>
/// 缓存契约层的单元测试：键与选项的校验、JSON 序列化器、序列化缓存、基于存储的锁、序列化器注册与幂等选项。
/// </summary>
public sealed class CacheContractTests
{
    /// <summary>
    /// 往返测试用的示例值类型。
    /// </summary>
    private sealed record Profile(string Name, int Age);

    [Theory]
    [InlineData("user:42", true)]
    [InlineData("a", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("tab\tkey", false)]
    [InlineData("new\nline", false)]
    public void CacheKey_ValidatesShape(string value, bool valid)
    {
        Assert.Equal(valid, CacheKey.IsValid(value));
        if (valid)
        {
            CacheKey key = value;
            Assert.Equal(value, key.Value);
            Assert.Equal(value, key.ToString());
        }
        else
        {
            Assert.Throws<ArgumentException>(() => new CacheKey(value));
        }
    }

    [Fact]
    public void CacheKey_RejectsOverlongAndNull()
    {
        Assert.False(CacheKey.IsValid(new string('k', CacheKey.MaxLength + 1)));
        Assert.True(CacheKey.IsValid(new string('k', CacheKey.MaxLength)));
        Assert.Throws<ArgumentNullException>(static () => new CacheKey(null!));
    }

    [Fact]
    public void CacheEntryOptions_RequirePositiveDurations_AndRejectBoth()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), CacheEntryOptions.Expires(TimeSpan.FromSeconds(5)).TimeToLive);
        Assert.Equal(TimeSpan.FromSeconds(5), CacheEntryOptions.Sliding(TimeSpan.FromSeconds(5)).SlidingExpiration);
        Assert.Null(CacheEntryOptions.Default.TimeToLive);
        Assert.Same(CacheEntryOptions.Default, CacheEntryOptions.Default.Validated());

        Assert.Throws<ArgumentOutOfRangeException>(static () => CacheEntryOptions.Expires(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(static () => CacheEntryOptions.Sliding(TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentException>(static () => new CacheEntryOptions { TimeToLive = TimeSpan.FromSeconds(1), SlidingExpiration = TimeSpan.FromSeconds(1) }.Validated());
    }

    [Fact]
    public void JsonCacheSerializer_RoundTrips_AndRejectsNull()
    {
        var serializer = JsonCacheSerializer.CreateReflectionBased();

        var bytes = serializer.Serialize(new Profile("ada", 36));
        Assert.Equal("{\"name\":\"ada\",\"age\":36}", Encoding.UTF8.GetString(bytes.Span));
        Assert.Equal(new Profile("ada", 36), serializer.Deserialize<Profile>(bytes));
        Assert.Equal(7, serializer.Deserialize<int>(Encoding.UTF8.GetBytes("7")));

        Assert.Throws<InvalidOperationException>(() => serializer.Deserialize<Profile>(Encoding.UTF8.GetBytes("null")));
        // 没有类型解析器的选项在 AOT 下无法取类型信息，必须在构造时拒绝
        Assert.Throws<ArgumentException>(static () => new JsonCacheSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Throws<ArgumentNullException>(static () => new JsonCacheSerializer(null!));
    }

    [Fact]
    public async Task SerializingCache_GetSetRemove_AndGetOrCreate()
    {
        var store = new FakeCacheStore();
        var cache = new SerializingCache(store, JsonCacheSerializer.CreateReflectionBased());
        var key = new CacheKey("profile:1");
        var factoryCalls = 0;

        Assert.False((await cache.GetAsync<Profile>(key, CancellationToken.None)).HasValue);

        var created = await cache.GetOrCreateAsync(key, _ =>
        {
            factoryCalls++;
            return ValueTask.FromResult(new Profile("ada", 36));
        }, CacheEntryOptions.Default, CancellationToken.None);
        var again = await cache.GetOrCreateAsync<Profile>(key, static _ => throw new InvalidOperationException("The factory should not be called again."), CacheEntryOptions.Default, CancellationToken.None);

        Assert.Equal(created, again);
        Assert.Equal(1, factoryCalls);
        Assert.Same(store, cache.Store);
        Assert.Equal(new Profile("ada", 36), (await cache.GetAsync<Profile>(key, CancellationToken.None)).ValueOrThrow());

        await cache.SetAsync(key, new Profile("bob", 40), CacheEntryOptions.Expires(TimeSpan.FromMinutes(1)), CancellationToken.None);
        Assert.Equal("bob", (await cache.GetAsync<Profile>(key, CancellationToken.None)).ValueOrThrow().Name);
        Assert.True(await cache.RemoveAsync(key, CancellationToken.None));
        Assert.False(await cache.RemoveAsync(key, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await cache.SetAsync<Profile>(key, null!, CacheEntryOptions.Default, CancellationToken.None));
    }

    [Fact]
    public async Task StoreDistributedLock_UsesTokenForRelease()
    {
        var store = new FakeCacheStore();
        var locks = new StoreDistributedLock(store);
        var resource = new CacheKey("order:1");

        var handle = (await locks.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), CancellationToken.None)).ValueOrThrow();
        Assert.Equal(resource, handle.Resource);
        Assert.Equal(32, handle.Token.Length);
        Assert.NotNull(store["lock:order:1"]);
        Assert.False((await locks.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), CancellationToken.None)).HasValue);

        await handle.DisposeAsync();

        Assert.Null(store["lock:order:1"]);
        Assert.False(await handle.ReleaseAsync(CancellationToken.None));
        Assert.True((await locks.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), CancellationToken.None)).HasValue);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await locks.TryAcquireAsync(resource, TimeSpan.Zero, CancellationToken.None));
    }

    [Fact]
    public void CacheSerializerRegistration_PrefersExplicitSerializer_ThenJsonOptions()
    {
        var explicitServices = new ServiceCollection().AddSingleton<ICacheSerializer>(JsonCacheSerializer.CreateReflectionBased());
        Assert.True(CacheSerializerRegistration.TryAddJsonSerializer(explicitServices));
        Assert.Single(explicitServices, static d => d.ServiceType == typeof(ICacheSerializer));

        var withOptions = new ServiceCollection().AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() });
        Assert.True(CacheSerializerRegistration.TryAddJsonSerializer(withOptions));
        Assert.IsType<JsonCacheSerializer>(withOptions.BuildServiceProvider().GetRequiredService<ICacheSerializer>());

        Assert.False(CacheSerializerRegistration.TryAddJsonSerializer(new ServiceCollection()));
    }

    [Fact]
    public void IdempotencyOptions_FromConfiguration_ParsesAndRejectsInvalid()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
        [
            new("XiHan:Caching:Idempotency:TimeToLiveSeconds", "3600"),
            new("XiHan:Caching:Idempotency:InProgressTimeToLiveSeconds", "1.5"),
        ]).Build();

        var options = IdempotencyOptions.FromConfiguration(configuration);

        Assert.Equal(TimeSpan.FromHours(1), options.TimeToLive);
        Assert.Equal(TimeSpan.FromSeconds(1.5), options.InProgressTimeToLive);
        Assert.Equal(TimeSpan.FromHours(24), IdempotencyOptions.FromConfiguration(new ConfigurationBuilder().Build()).TimeToLive);
        Assert.Throws<ArgumentException>(static () => IdempotencyOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection([new("XiHan:Caching:Idempotency:TimeToLiveSeconds", "0")]).Build()));
    }
}
