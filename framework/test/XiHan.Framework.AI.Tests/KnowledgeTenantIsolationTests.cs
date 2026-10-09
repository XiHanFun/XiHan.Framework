// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.Abstractions.Rag;
using XiHan.Framework.AI.Rag;

namespace XiHan.Framework.AI.Tests;

/// <summary>
/// 默认知识摄取器在共享集合中的租户隔离测试。
/// </summary>
public sealed class KnowledgeTenantIsolationTests
{
    private const int Dimensions = 3;

    /// <summary>
    /// 两个租户复用同一文档标识时各自写入，删除一个租户的文档后另一个租户的切片保留。
    /// </summary>
    [Fact]
    public async Task SameDocumentId_AcrossTenants_RecordsIsolation()
    {
        var store = new InMemoryKnowledgeVectorStore();
        var ingestor = CreateIngestor(store);

        Assert.Equal(2, await ingestor.IngestAsync(Request("doc-1", 101, "a1|a2"), TestContext.Current.CancellationToken));
        Assert.Equal(2, await ingestor.IngestAsync(Request("doc-1", 202, "b1|b2"), TestContext.Current.CancellationToken));
        Assert.Equal(4, store.Records.Count);

        await ingestor.RemoveDocumentAsync("doc-1", 101, 2, TestContext.Current.CancellationToken);

        var remaining = store.Records.Values.OrderBy(r => r.ChunkIndex).ToList();
        Assert.Equal(2, remaining.Count);
        Assert.All(remaining, r => Assert.Equal(202, r.TenantId));
        Assert.Equal(["b1", "b2"], remaining.Select(r => r.Text));
    }

    /// <summary>
    /// 文档标识全局唯一时，删除只影响目标文档，其他租户与平台文档保留。
    /// </summary>
    [Fact]
    public async Task GloballyUniqueIds_DeleteOnlyTarget()
    {
        var store = new InMemoryKnowledgeVectorStore();
        var ingestor = CreateIngestor(store);

        await ingestor.IngestAsync(Request("doc-a", 101, "a1|a2"), TestContext.Current.CancellationToken);
        await ingestor.IngestAsync(Request("doc-b", 202, "b1"), TestContext.Current.CancellationToken);
        await ingestor.IngestAsync(Request("doc-c", 0, "c1|c2"), TestContext.Current.CancellationToken);

        await ingestor.RemoveDocumentAsync("doc-a", 101, 2, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(store.Records.Values, r => r.DocumentId == "doc-a");
        Assert.Single(store.Records.Values, r => r.DocumentId == "doc-b" && r.TenantId == 202);
        Assert.Equal(2, store.Records.Values.Count(r => r.DocumentId == "doc-c" && r.TenantId == 0));
    }

    /// <summary>
    /// 删除非平台租户文档时一并清理该租户的旧版主键记录，旧版主键下属于其他租户的记录保留。
    /// </summary>
    [Fact]
    public async Task RemoveDocumentAsync_LegacyKeys_DeleteOnlyMatchingTenant()
    {
        var store = new InMemoryKnowledgeVectorStore();
        var ingestor = CreateIngestor(store);

        // 旧版主键与租户 0 的主键相同。
        var legacyOther = VectorStoreKnowledgeRecord.MakeId(0, "doc-1", 0);
        var legacyOwn = VectorStoreKnowledgeRecord.MakeId(0, "doc-1", 1);
        store.Records[legacyOther] = Record(legacyOther, "doc-1", 202, 0);
        store.Records[legacyOwn] = Record(legacyOwn, "doc-1", 101, 1);
        await ingestor.IngestAsync(Request("doc-1", 101, "a1|a2"), TestContext.Current.CancellationToken);

        await ingestor.RemoveDocumentAsync("doc-1", 101, 2, TestContext.Current.CancellationToken);

        var remaining = Assert.Single(store.Records.Values);
        Assert.Equal(legacyOther, remaining.Id);
        Assert.Equal(202, remaining.TenantId);
    }

    private static DefaultKnowledgeIngestor CreateIngestor(InMemoryKnowledgeVectorStore store)
    {
        return new DefaultKnowledgeIngestor(
            new PipeChunkingStrategy(),
            new FixedEmbeddingGeneratorResolver(),
            store,
            Options.Create(new KnowledgeVectorOptions { Dimensions = Dimensions }));
    }

    private static KnowledgeIngestRequest Request(string documentId, long tenantId, string text)
    {
        return new KnowledgeIngestRequest
        {
            DocumentId = documentId,
            TenantId = tenantId,
            Text = text
        };
    }

    private static VectorStoreKnowledgeRecord Record(Guid id, string documentId, long tenantId, int chunkIndex)
    {
        return new VectorStoreKnowledgeRecord
        {
            Id = id,
            DocumentId = documentId,
            TenantId = tenantId,
            ChunkIndex = chunkIndex,
            Text = $"legacy-{tenantId}-{chunkIndex}",
            Embedding = new float[Dimensions]
        };
    }

    /// <summary>
    /// 按竖线切片。
    /// </summary>
    private sealed class PipeChunkingStrategy : IChunkingStrategy
    {
        public IReadOnlyList<string> Chunk(string text, ChunkingOptions options)
        {
            return text.Split('|', StringSplitOptions.RemoveEmptyEntries);
        }
    }

    /// <summary>
    /// 为每段输入返回固定维度的零向量。
    /// </summary>
    private sealed class FixedEmbeddingGeneratorResolver : IAiEmbeddingGeneratorResolver
    {
        public IEmbeddingGenerator<string, Embedding<float>> Resolve(string? providerName = null)
        {
            return new FixedEmbeddingGenerator();
        }

        public void Invalidate(string? providerName = null)
        {
        }
    }

    private sealed class FixedEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(
                values.Select(_ => new Embedding<float>(new float[Dimensions]))));
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 只支持单个知识切片集合的内存向量库。
    /// </summary>
    private sealed class InMemoryKnowledgeVectorStore : VectorStore
    {
        private readonly InMemoryKnowledgeCollection _collection;

        public InMemoryKnowledgeVectorStore()
        {
            _collection = new InMemoryKnowledgeCollection(Records);
        }

        public Dictionary<Guid, VectorStoreKnowledgeRecord> Records { get; } = [];

        public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
            string name,
            VectorStoreCollectionDefinition? definition = null)
        {
            return (VectorStoreCollection<TKey, TRecord>)(object)_collection;
        }

        public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
            string name,
            VectorStoreCollectionDefinition definition)
        {
            throw new NotSupportedException();
        }

        public override IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
        {
            return _collection.CollectionExistsAsync(cancellationToken);
        }

        public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
        {
            return _collection.EnsureCollectionDeletedAsync(cancellationToken);
        }

        public override object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }
    }

    private sealed class InMemoryKnowledgeCollection(Dictionary<Guid, VectorStoreKnowledgeRecord> store)
        : VectorStoreCollection<Guid, VectorStoreKnowledgeRecord>
    {
        private bool _exists;

        public override string Name => KnowledgeVectorOptions.DefaultCollectionName;

        public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_exists || store.Count > 0);
        }

        public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
        {
            _exists = true;
            return Task.CompletedTask;
        }

        public override Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
        {
            _exists = false;
            store.Clear();
            return Task.CompletedTask;
        }

        public override Task<VectorStoreKnowledgeRecord?> GetAsync(
            Guid key,
            RecordRetrievalOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(store.GetValueOrDefault(key));
        }

        public override async IAsyncEnumerable<VectorStoreKnowledgeRecord> GetAsync(
            IEnumerable<Guid> keys,
            RecordRetrievalOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var key in keys)
            {
                if (store.TryGetValue(key, out var record))
                {
                    yield return record;
                }
            }

            await Task.CompletedTask;
        }

        public override IAsyncEnumerable<VectorStoreKnowledgeRecord> GetAsync(
            Expression<Func<VectorStoreKnowledgeRecord, bool>> filter,
            int top,
            FilteredRecordRetrievalOptions<VectorStoreKnowledgeRecord>? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override Task DeleteAsync(Guid key, CancellationToken cancellationToken = default)
        {
            store.Remove(key);
            return Task.CompletedTask;
        }

        public override Task DeleteAsync(IEnumerable<Guid> keys, CancellationToken cancellationToken = default)
        {
            foreach (var key in keys)
            {
                store.Remove(key);
            }

            return Task.CompletedTask;
        }

        public override Task UpsertAsync(VectorStoreKnowledgeRecord record, CancellationToken cancellationToken = default)
        {
            store[record.Id] = record;
            return Task.CompletedTask;
        }

        public override Task UpsertAsync(
            IEnumerable<VectorStoreKnowledgeRecord> records,
            CancellationToken cancellationToken = default)
        {
            foreach (var record in records)
            {
                store[record.Id] = record;
            }

            return Task.CompletedTask;
        }

        public override IAsyncEnumerable<VectorSearchResult<VectorStoreKnowledgeRecord>> SearchAsync<TInput>(
            TInput searchValue,
            int top,
            VectorSearchOptions<VectorStoreKnowledgeRecord>? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }
    }
}
