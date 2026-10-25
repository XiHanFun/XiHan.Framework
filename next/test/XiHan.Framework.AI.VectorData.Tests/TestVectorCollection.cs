// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// 进程内的向量集合：字典存储，过滤表达式直接编译执行，余弦相似度搜索。
/// </summary>
internal sealed class TestVectorCollection : VectorStoreCollection<string, KnowledgeVectorRecord>
{
    /// <summary>
    /// ID → 记录。
    /// </summary>
    private readonly Dictionary<string, KnowledgeVectorRecord> _records = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建。
    /// </summary>
    public TestVectorCollection(string name)
    {
        Name = name;
    }

    /// <inheritdoc />
    public override string Name { get; }

    /// <summary>
    /// 是否已创建。
    /// </summary>
    public bool Exists { get; private set; }

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Exists);

    /// <inheritdoc />
    public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        Exists = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        Exists = false;
        _records.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task<KnowledgeVectorRecord?> GetAsync(string key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default)
        => Task.FromResult(_records.TryGetValue(key, out var record) ? Clone(record, options?.IncludeVectors == true) : null);

    /// <inheritdoc />
    public override async IAsyncEnumerable<KnowledgeVectorRecord> GetAsync(IEnumerable<string> keys, RecordRetrievalOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        foreach (var key in keys)
        {
            if (_records.TryGetValue(key, out var record))
            {
                yield return Clone(record, options?.IncludeVectors == true);
            }
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<KnowledgeVectorRecord> GetAsync(Expression<Func<KnowledgeVectorRecord, bool>> filter, int top, FilteredRecordRetrievalOptions<KnowledgeVectorRecord>? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        var predicate = filter.Compile();
        foreach (var record in _records.Values.Where(predicate).Skip(options?.Skip ?? 0).Take(top).ToList())
        {
            yield return Clone(record, options?.IncludeVectors == true);
        }
    }

    /// <inheritdoc />
    public override Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _records.Remove(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            _records.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpsertAsync(KnowledgeVectorRecord record, CancellationToken cancellationToken = default)
    {
        if (!Exists)
        {
            throw new InvalidOperationException("The collection does not exist.");
        }

        _records[record.Id] = Clone(record, includeVectors: true);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(IEnumerable<KnowledgeVectorRecord> records, CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            await UpsertAsync(record, cancellationToken);
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<VectorSearchResult<KnowledgeVectorRecord>> SearchAsync<TInput>(TInput searchValue, int top, VectorSearchOptions<KnowledgeVectorRecord>? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        if (searchValue is not ReadOnlyMemory<float> vector)
        {
            throw new NotSupportedException("The test collection only searches by float vectors.");
        }

        var predicate = options?.Filter?.Compile();
        var hits = _records.Values
            .Where(record => predicate is null || predicate(record))
            .Select(record => new VectorSearchResult<KnowledgeVectorRecord>(Clone(record, options?.IncludeVectors == true), Cosine(vector.Span, record.Vector.Span)))
            .OrderByDescending(static hit => hit.Score)
            .Skip(options?.Skip ?? 0)
            .Take(top)
            .ToList();
        foreach (var hit in hits)
        {
            yield return hit;
        }
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    /// <summary>
    /// 复制一条记录，按需去掉向量。
    /// </summary>
    private static KnowledgeVectorRecord Clone(KnowledgeVectorRecord record, bool includeVectors) => new()
    {
        Id = record.Id,
        DocumentId = record.DocumentId,
        ChunkIndex = record.ChunkIndex,
        Text = record.Text,
        Metadata = record.Metadata,
        Vector = includeVectors ? record.Vector : ReadOnlyMemory<float>.Empty,
    };

    /// <summary>
    /// 余弦相似度。
    /// </summary>
    private static double Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length && i < b.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt(na * nb);
    }
}
