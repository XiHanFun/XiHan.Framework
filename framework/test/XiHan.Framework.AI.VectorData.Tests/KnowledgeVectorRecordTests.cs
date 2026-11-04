// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions.Knowledge;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// 向量记录的键（RFC-0022、ADR-0111）
/// </summary>
/// <remarks>
/// 键由记录 ID 确定性推出，算法是存储格式的一部分，用固定向量锁住；写入后对外的记录 ID 不变
/// </remarks>
public sealed class KnowledgeVectorRecordTests
{
    [Theory]
    [InlineData("cats#0", "bf5265ac-88dd-8c19-aed4-85f49dee74bb")]
    [InlineData("doc-1#12", "71a1f72b-0909-805f-9cff-ea750f11aafe")]
    [InlineData("文档#0", "c9b14898-9191-8289-9b09-6726367d2d1a")]
    public void KeyOf_FollowsTheSpecifiedAlgorithm(string recordId, string expected)
    {
        var key = KnowledgeVectorRecord.KeyOf(recordId);

        Assert.Equal(Guid.Parse(expected), key);
        // 第 13 个十六进制位是版本 8，第 17 个是 RFC 变体（8、9、a、b）
        var text = key.ToString("D");
        Assert.Equal('8', text[14]);
        Assert.Contains(text[19], "89ab");
    }

    [Fact]
    public void KeyOf_IsStable_AndDiffersBetweenRecords()
    {
        Assert.Equal(KnowledgeVectorRecord.KeyOf("doc#0"), KnowledgeVectorRecord.KeyOf("doc#0"));
        Assert.NotEqual(KnowledgeVectorRecord.KeyOf("doc#0"), KnowledgeVectorRecord.KeyOf("doc#1"));
        Assert.Throws<ArgumentNullException>(() => KnowledgeVectorRecord.KeyOf(null!));
    }

    [Fact]
    public async Task Upsert_StoresTheDerivedKey_AndTheRecordId_AndSearchRestoresTheRecordId()
    {
        var store = new TestVectorStore();
        await using var host = await VectorDataTestHost.StartAsync(store);
        var knowledge = host.Services.GetRequiredService<IKnowledgeStore>();
        var vector = new float[VectorDataTestHost.Dimensions];
        vector[0] = 1;
        var record = new KnowledgeRecord { Id = "doc#3", DocumentId = "doc", ChunkIndex = 3, Text = "text", Metadata = new Dictionary<string, string>(), Vector = vector };

        (await knowledge.UpsertAsync("keys", [record], CancellationToken.None)).ThrowIfFailure();

        var stored = await store.GetCollection<Guid, KnowledgeVectorRecord>("keys").GetAsync(KnowledgeVectorRecord.KeyOf("doc#3"));
        Assert.NotNull(stored);
        Assert.Equal("doc#3", stored.RecordId);
        var hit = Assert.Single((await knowledge.SearchAsync("keys", vector, 1, null, CancellationToken.None)).ValueOrThrow());
        Assert.Equal("doc#3", hit.Id);
    }
}
