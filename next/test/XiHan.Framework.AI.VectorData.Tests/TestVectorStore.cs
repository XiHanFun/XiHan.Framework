// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.VectorData;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// 进程内的向量存储
/// </summary>
/// <remarks>
/// 只支持 <c>&lt;Guid, KnowledgeVectorRecord&gt;</c> 集合，供 Provider 测试用。存储级的 <see cref="CollectionExistsAsync"/>（健康探针只用它）可以让它抛出或挂起，模拟向量库不可达或连接卡住
/// </remarks>
internal sealed class TestVectorStore : VectorStore
{
    /// <summary>
    /// 名字 → 集合
    /// </summary>
    private readonly Dictionary<string, TestVectorCollection> _collections = new(StringComparer.Ordinal);

    /// <summary>
    /// 已创建的集合名
    /// </summary>
    public IReadOnlyCollection<string> Collections => _collections.Where(static pair => pair.Value.Exists).Select(static pair => pair.Key).ToList();

    /// <summary>
    /// 存储级调用抛出的异常（模拟向量库不可达）
    /// </summary>
    /// <remarks>
    /// 空表示照常应答
    /// </remarks>
    public Exception? Fault { get; set; }

    /// <summary>
    /// 挂起存储级 <see cref="CollectionExistsAsync"/> 的任务源（模拟卡住、不理会取消令牌的连接器）
    /// </summary>
    /// <remarks>
    /// 空表示照常应答
    /// </remarks>
    public TaskCompletionSource<bool>? Hold { get; set; }

    /// <summary>
    /// 存储级 <see cref="CollectionExistsAsync"/> 问过的集合名（按先后）
    /// </summary>
    public ConcurrentQueue<string> ExistenceChecks { get; } = new();

    /// <summary>
    /// <see cref="ListCollectionNamesAsync"/> 被调用的次数
    /// </summary>
    public int Listings => _listings;

    /// <summary>
    /// 列集合的次数
    /// </summary>
    private int _listings;

    /// <inheritdoc />
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition = null)
    {
        if (typeof(TKey) != typeof(Guid) || typeof(TRecord) != typeof(KnowledgeVectorRecord))
        {
            throw new NotSupportedException("The test store only supports Guid keys and knowledge records.");
        }

        if (!_collections.TryGetValue(name, out var collection))
        {
            collection = new TestVectorCollection(name);
            _collections[name] = collection;
        }

        return (VectorStoreCollection<TKey, TRecord>)(object)collection;
    }

    /// <inheritdoc />
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(string name, VectorStoreCollectionDefinition definition) => throw new NotSupportedException();

    /// <inheritdoc />
    public override IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _listings);
        return Collections.ToAsyncEnumerable();
    }

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        ExistenceChecks.Enqueue(name);
        if (Fault is { } fault)
        {
            return Task.FromException<bool>(fault);
        }

        // 挂起的任务不看取消令牌：探针要自己按时限结束等待
        return Hold is { } hold ? hold.Task : Task.FromResult(_collections.TryGetValue(name, out var collection) && collection.Exists);
    }

    /// <inheritdoc />
    public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        _collections.Remove(name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
}
