// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Caching.Remote.Tests;

/// <summary>
/// 按 caching-distributed.json 核对适配器的操作集与消息形态
/// </summary>
/// <remarks>
/// 向量是 spec/conformance/vectors/provider/caching-distributed.json。样例的语义（读写顺序与结果）由插件实现回放；适配器只发请求、读响应，这里核对它的消息类型写出的请求与样例一致、读得出样例的响应，非法键与非法选项在本地就构造不出来，发不到插件
/// </remarks>
public sealed class RemoteCacheVectorTests
{
    /// <summary>
    /// 向量文件
    /// </summary>
    private static readonly JsonDocument _vectors = JsonDocument.Parse(File.ReadAllText(SpecVectors.PathOf("provider", "caching-distributed.json")));

    /// <summary>
    /// 每个操作的请求与响应消息类型，与 <see cref="RemoteCacheStore"/> 发出的一致
    /// </summary>
    private static readonly Dictionary<string, (JsonTypeInfo Request, JsonTypeInfo Response)> _messages = new(StringComparer.Ordinal)
    {
        [RemoteCacheOperations.Get] = (RemoteCacheJsonContext.Default.KeyRequest, RemoteCacheJsonContext.Default.GetResponse),
        [RemoteCacheOperations.Set] = (RemoteCacheJsonContext.Default.SetRequest, RemoteCacheJsonContext.Default.EmptyResponse),
        [RemoteCacheOperations.TryAdd] = (RemoteCacheJsonContext.Default.SetRequest, RemoteCacheJsonContext.Default.AddedResponse),
        [RemoteCacheOperations.Remove] = (RemoteCacheJsonContext.Default.KeyRequest, RemoteCacheJsonContext.Default.RemovedResponse),
        [RemoteCacheOperations.TryRemove] = (RemoteCacheJsonContext.Default.TryRemoveRequest, RemoteCacheJsonContext.Default.RemovedResponse),
        [RemoteCacheOperations.TryRefresh] = (RemoteCacheJsonContext.Default.TryRefreshRequest, RemoteCacheJsonContext.Default.RefreshedResponse),
        [RemoteCacheOperations.GetMany] = (RemoteCacheJsonContext.Default.KeysRequest, RemoteCacheJsonContext.Default.GetManyResponse),
        [RemoteCacheOperations.SetMany] = (RemoteCacheJsonContext.Default.SetManyRequest, RemoteCacheJsonContext.Default.EmptyResponse),
        [RemoteCacheOperations.RemoveMany] = (RemoteCacheJsonContext.Default.KeysRequest, RemoteCacheJsonContext.Default.RemovedManyResponse),
    };

    [Fact]
    public void Operations_AreTheOperationSetInOrder()
    {
        var operations = Root.GetProperty("operations").EnumerateArray().Select(static o => o.GetString()!).ToArray();

        Assert.Equal("caching.distributed@1", Root.GetProperty("capability").GetString());
        Assert.Equal(operations, RemoteCacheOperations.All);
        Assert.Equal(operations.Order(StringComparer.Ordinal), _messages.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SampleRequests_AreWrittenAsInTheVector()
    {
        var samples = 0;
        foreach (var sample in Root.GetProperty("samples").EnumerateArray())
        {
            samples++;
            var (request, _) = _messages[sample.GetProperty("operation").GetString()!];
            var expected = JsonNode.Parse(sample.GetProperty("request").GetRawText());

            // 读进适配器的请求类型再写出，必须与样例逐成员相等：适配器发给插件的就是这样的文本
            var message = JsonSerializer.Deserialize(sample.GetProperty("request").GetRawText(), request);
            var written = JsonSerializer.SerializeToNode(message, request);

            Assert.True(JsonNode.DeepEquals(expected, written), $"{sample.GetProperty("operation").GetString()}: expected {expected?.ToJsonString()}, written {written?.ToJsonString()}");
        }

        Assert.True(samples >= 10, $"Only {samples} samples were read.");
    }

    [Fact]
    public void SampleResponses_AreReadAsInTheVector()
    {
        foreach (var sample in Root.GetProperty("samples").EnumerateArray())
        {
            var (_, response) = _messages[sample.GetProperty("operation").GetString()!];
            var message = JsonSerializer.Deserialize(sample.GetProperty("response").GetRawText(), response);
            Assert.NotNull(message);

            // 适配器的消息不写出值为 null 的成员，读取时缺省与 null 相同，比较前两边都去掉对象里的 null 成员
            var expected = WithoutNullMembers(JsonNode.Parse(sample.GetProperty("response").GetRawText()));
            var read = WithoutNullMembers(JsonSerializer.SerializeToNode(message, response));
            Assert.True(JsonNode.DeepEquals(expected, read), $"{sample.GetProperty("operation").GetString()}: expected {expected?.ToJsonString()}, read {read?.ToJsonString()}");
        }
    }

    [Fact]
    public void InvalidKeys_CannotBeBuilt()
    {
        foreach (var key in Root.GetProperty("invalidKeys").EnumerateArray().Select(static k => k.GetString()!))
        {
            Assert.ThrowsAny<ArgumentException>(() => new CacheKey(key));
        }
    }

    [Fact]
    public void InvalidOptions_AreRejectedBeforeTheyAreSent()
    {
        foreach (var options in Root.GetProperty("invalidOptions").EnumerateArray())
        {
            Assert.ThrowsAny<ArgumentException>(() =>
            {
                var built = new CacheEntryOptions
                {
                    TimeToLive = options.TryGetProperty("ttlMs", out var ttl) ? TimeSpan.FromMilliseconds(ttl.GetInt64()) : null,
                    SlidingExpiration = options.TryGetProperty("slidingMs", out var sliding) ? TimeSpan.FromMilliseconds(sliding.GetInt64()) : null,
                };
                return built.Validated();
            });
        }
    }

    [Fact]
    public void ErrorCodes_AreTheCachingCodes()
    {
        var errors = Root.GetProperty("errors");

        Assert.Equal(CachingErrors.InvalidKey, errors.GetProperty("invalidKey").GetString());
        Assert.Equal(CachingErrors.InvalidOptions, errors.GetProperty("invalidOptions").GetString());
        Assert.Equal(CachingErrors.StoreUnavailable, errors.GetProperty("unavailable").GetString());
    }

    /// <summary>
    /// 向量的根对象
    /// </summary>
    private static JsonElement Root => _vectors.RootElement;

    /// <summary>
    /// 递归去掉对象里值为 null 的成员
    /// </summary>
    /// <remarks>
    /// 数组里的 null 保留，批量响应按位置对应
    /// </remarks>
    private static JsonNode? WithoutNullMembers(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject members:
                foreach (var name in members.Where(static m => m.Value is null).Select(static m => m.Key).ToList())
                {
                    members.Remove(name);
                }

                foreach (var member in members.ToList())
                {
                    WithoutNullMembers(member.Value);
                }

                break;
            case JsonArray items:
                foreach (var item in items)
                {
                    WithoutNullMembers(item);
                }

                break;
        }

        return node;
    }
}
