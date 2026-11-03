// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Conformance.Embeddings;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Knowledge;

/// <summary>
/// 提示增强里片段的来源（RFC-0022、ADR-0116）
/// </summary>
/// <remarks>
/// 模型要看得到片段出自哪里才能说清依据：有标题打标题，再来源，都没有才打文档 ID
/// </remarks>
public sealed class RagPromptSourceTests
{
    [Theory]
    [InlineData("员工手册", "wiki/handbook", "员工手册")]
    [InlineData(null, "wiki/handbook", "wiki/handbook")]
    [InlineData("  ", "wiki/handbook", "wiki/handbook")]
    [InlineData(null, null, "doc-1")]
    [InlineData("", " ", "doc-1")]
    public async Task Source_IsTheTitle_ThenTheSource_ThenTheDocumentId(string? title, string? source, string expected)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (title is not null)
        {
            metadata["title"] = title;
        }

        if (source is not null)
        {
            metadata["source"] = source;
        }

        var prompt = await AugmentAsync(new RetrievedChunk { Id = "doc-1#3", DocumentId = "doc-1", ChunkIndex = 3, Text = "正文", Metadata = metadata, Score = 0.9 });

        Assert.Contains($"# Knowledge snippets\n[1] Source: {expected}\n正文\n", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Source_TakesOneLine_EvenWhenTheTitleHasLineBreaks()
    {
        var prompt = await AugmentAsync(new RetrievedChunk { Id = "doc-1#0", DocumentId = "doc-1", ChunkIndex = 0, Text = "正文", Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["title"] = "第一行\r\n第二行" }, Score = 0.9 });

        Assert.Contains("[1] Source: 第一行 第二行\n正文\n", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataKeys_AreTheOnesApplicationsWrite()
    {
        Assert.Equal("title", KnowledgeMetadataKeys.Title);
        Assert.Equal("source", KnowledgeMetadataKeys.Source);
    }

    /// <summary>
    /// 用内置模板增强一个片段
    /// </summary>
    private static async Task<string> AugmentAsync(RetrievedChunk chunk)
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<HashingEmbeddingFeature>());
        return host.Services.GetRequiredService<IRagPromptAugmenter>().Augment("why?", [chunk]);
    }
}
