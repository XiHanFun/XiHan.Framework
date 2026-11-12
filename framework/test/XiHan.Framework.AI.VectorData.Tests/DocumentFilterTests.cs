// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions.Knowledge;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// 按文档过滤的检索（ADR-0190）
/// </summary>
/// <remarks>
/// 文档 ID 拼成平衡的或树交给连接器：上限个数的 ID 照常过滤，只返回点名的文档
/// </remarks>
public sealed class DocumentFilterTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(RetrievalFilter.MaxDocumentIds)]
    public async Task FilterOfManyDocuments_ReturnsOnlyTheNamedOnes(int count)
    {
        await using var host = await VectorDataTestHost.StartAsync(new TestVectorStore());
        var knowledge = host.Services.GetRequiredService<IKnowledgeStore>();
        var vector = new float[VectorDataTestHost.Dimensions];
        vector[0] = 1f;
        var records = Enumerable.Range(0, 5).Select(i => new KnowledgeRecord { Id = $"d{i}#0", DocumentId = $"d{i}", ChunkIndex = 0, Text = $"t{i}", Vector = vector }).ToList();
        (await knowledge.UpsertAsync("faq", records, CancellationToken.None)).ThrowIfFailure();

        // 点名 d1 与 d3，其余是不存在的文档，凑够 count 个
        var named = new List<string> { "d1", "d3" }.Take(count).Concat(Enumerable.Range(0, Math.Max(0, count - 2)).Select(static i => $"missing-{i}")).ToList();
        var search = new KnowledgeSearch(vector, 10) { Filter = new RetrievalFilter { DocumentIds = named } };

        var hits = (await knowledge.SearchAsync("faq", search, CancellationToken.None)).ValueOrThrow();

        Assert.Equal(named.Where(static id => id.StartsWith('d')).Order(StringComparer.Ordinal), hits.Select(static h => h.DocumentId).Order(StringComparer.Ordinal));
    }
}
