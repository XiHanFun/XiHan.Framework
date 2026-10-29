// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.VectorData;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// 进程内的向量存储。
/// </summary>
/// <remarks>
/// 只支持 <c>&lt;string, KnowledgeVectorRecord&gt;</c> 集合，供 Provider 测试用。
/// </remarks>
internal sealed class TestVectorStore : VectorStore
{
    /// <summary>
    /// 名字 → 集合。
    /// </summary>
    private readonly Dictionary<string, TestVectorCollection> _collections = new(StringComparer.Ordinal);

    /// <summary>
    /// 已创建的集合名。
    /// </summary>
    public IReadOnlyCollection<string> Collections => _collections.Where(static pair => pair.Value.Exists).Select(static pair => pair.Key).ToList();

    /// <inheritdoc />
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition = null)
    {
        if (typeof(TKey) != typeof(string) || typeof(TRecord) != typeof(KnowledgeVectorRecord))
        {
            throw new NotSupportedException("The test store only supports string keys and knowledge records.");
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
    public override IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default) => Collections.ToAsyncEnumerable();

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(_collections.TryGetValue(name, out var collection) && collection.Exists);

    /// <inheritdoc />
    public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        _collections.Remove(name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
}
