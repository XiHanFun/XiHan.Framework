// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using CommunityToolkit.VectorData.Qdrant;
using Microsoft.Extensions.VectorData;
using Qdrant.Client;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// 接真实 Qdrant 的宿主，释放时删掉这个用例建的集合
/// </summary>
/// <remarks>
/// 真实 Qdrant 来自环境变量 <c>XIHAN_TEST_QDRANT</c>（gRPC 地址，如 <c>http://localhost:6334</c>），未设置时跳过需要它的用例
/// </remarks>
internal sealed class QdrantTestHost : IAsyncDisposable
{
    /// <summary>
    /// 指向测试 Qdrant 的环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_QDRANT";

    /// <summary>
    /// 向量存储
    /// </summary>
    private readonly VectorStore _store;

    /// <summary>
    /// 用例开始前已有的集合
    /// </summary>
    private readonly HashSet<string> _existing;

    /// <summary>
    /// 创建
    /// </summary>
    private QdrantTestHost(VectorStore store, HashSet<string> existing, TestHost host)
    {
        _store = store;
        _existing = existing;
        Host = host;
    }

    /// <summary>
    /// 宿主
    /// </summary>
    public TestHost Host { get; }

    /// <summary>
    /// 连接真实 Qdrant 并启动宿主
    /// </summary>
    /// <remarks>
    /// 未设置环境变量时跳过当前用例
    /// </remarks>
    public static async Task<QdrantTestHost> StartAsync(params (string Key, string Value)[] extra)
    {
        var address = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(address), $"Set the environment variable '{EnvironmentVariable}' to a Qdrant gRPC address to run this test.");
        var store = new QdrantVectorStore(new QdrantClient(new Uri(address)), ownsClient: true);
        var existing = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var name in store.ListCollectionNamesAsync())
        {
            existing.Add(name);
        }

        return new QdrantTestHost(store, existing, await VectorDataTestHost.StartAsync(store, extra));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        // 只删这个用例建的集合：同一个类的用例按顺序执行，别的测试类不碰 Qdrant，所以差集就是这个用例建的
        var created = new List<string>();
        await foreach (var name in _store.ListCollectionNamesAsync())
        {
            if (!_existing.Contains(name))
            {
                created.Add(name);
            }
        }

        foreach (var name in created)
        {
            await _store.EnsureCollectionDeletedAsync(name);
        }

        _store.Dispose();
    }
}
