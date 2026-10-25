// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 进程内的知识库存储：按集合保存记录，余弦相似度搜索。
/// </summary>
internal sealed class FakeKnowledgeStore : IKnowledgeStore
{
    /// <summary>
    /// 集合 → 记录 ID → 记录。
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, KnowledgeRecord>> _collections = new(StringComparer.Ordinal);

    /// <summary>
    /// 一个集合里的记录。
    /// </summary>
    public IReadOnlyCollection<KnowledgeRecord> Records(string collection) => _collections.TryGetValue(collection, out var records) ? records.Values : [];

    /// <inheritdoc />
    public ValueTask<Result> UpsertAsync(string collection, IReadOnlyList<KnowledgeRecord> records, CancellationToken cancellationToken)
    {
        if (!_collections.TryGetValue(collection, out var table))
        {
            table = new Dictionary<string, KnowledgeRecord>(StringComparer.Ordinal);
            _collections[collection] = table;
        }

        foreach (var record in records)
        {
            table[record.Id] = record;
        }

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public ValueTask<Result> DeleteAsync(string collection, IReadOnlyList<string> documentIds, CancellationToken cancellationToken)
    {
        if (_collections.TryGetValue(collection, out var table))
        {
            foreach (var id in table.Values.Where(record => documentIds.Contains(record.DocumentId, StringComparer.Ordinal)).Select(static record => record.Id).ToList())
            {
                table.Remove(id);
            }
        }

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<RetrievedChunk>>> SearchAsync(string collection, ReadOnlyMemory<float> vector, int top, RetrievalFilter? filter, CancellationToken cancellationToken)
    {
        IReadOnlyList<RetrievedChunk> hits = _collections.TryGetValue(collection, out var table)
            ? table.Values
                .Where(record => filter is null || filter.Matches(record.DocumentId, record.Metadata))
                .Select(record => RetrievedChunk.From(record, Cosine(vector.Span, record.Vector.Span)))
                .OrderByDescending(static chunk => chunk.Score)
                .Take(top)
                .ToList()
            : [];
        return ValueTask.FromResult(Result.Success(hits));
    }

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
