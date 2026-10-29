// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// VectorData 知识库 Provider。
/// </summary>
/// <remarks>
/// 没有向量存储即启动失败；完整的摄入 → 检索 → 增强流程。
/// </remarks>
public sealed class VectorDataKnowledgeFeatureTests
{
    [Fact]
    public async Task Start_FailsWithoutAVectorStore()
    {
        var feature = new VectorDataKnowledgeFeature();
        Assert.Equal([AICapabilities.Knowledge], feature.Manifest.Provides.Select(static capability => capability.Id));
        Assert.Contains(feature.Manifest.Requires, static requirement => requirement.Id == AICapabilities.Embedding);
        Assert.Equal(ManifestRole.Provider, feature.Manifest.Role);

        var failure = await Assert.ThrowsAsync<FrameworkException>(() => VectorDataTestHost.StartAsync(null));
        var codes = new List<string>();
        for (Exception? current = failure; current is not null; current = current.InnerException)
        {
            if (current is FrameworkException framework)
            {
                codes.Add(framework.Code);
            }
        }

        Assert.Contains(AIErrors.KnowledgeUnavailable, codes);
    }

    [Fact]
    public async Task Ingest_Retrieve_Augment_EndToEnd()
    {
        var store = new TestVectorStore();
        await using var host = await VectorDataTestHost.StartAsync(store, ("XiHan:AI:Knowledge:Collection", "faq"));
        var ingestor = host.Services.GetRequiredService<IKnowledgeIngestor>();
        var retriever = host.Services.GetRequiredService<IKnowledgeRetriever>();

        var ingested = await ingestor.IngestAsync(
        [
            new KnowledgeIngestRequest { DocumentId = "password", Text = "To reset your password open Settings, choose Security and press Reset password. A reset link is sent by email.", Metadata = new Dictionary<string, string> { ["area"] = "account" } },
            new KnowledgeIngestRequest { DocumentId = "billing", Text = "Invoices are issued monthly. Billing questions go to the finance team by email.", Metadata = new Dictionary<string, string> { ["area"] = "billing" } },
        ], CancellationToken.None);
        Assert.All(ingested, result => Assert.True(result.IsSuccess));
        Assert.Equal(["faq"], store.Collections);

        var chunks = (await retriever.RetrieveAsync(new KnowledgeQuery { Text = "how do I reset my password", Top = 2 }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(2, chunks.Count);
        Assert.Equal("password", chunks[0].DocumentId);
        Assert.Equal("account", chunks[0].Metadata["area"]);

        var billingOnly = (await retriever.RetrieveAsync(new KnowledgeQuery { Text = "reset password", Top = 5, Filter = new RetrievalFilter { Metadata = new Dictionary<string, string> { ["area"] = "billing" } } }, CancellationToken.None)).ValueOrThrow();
        Assert.All(billingOnly, chunk => Assert.Equal("billing", chunk.DocumentId));

        var prompt = host.Services.GetRequiredService<IRagPromptAugmenter>().Augment("how do I reset my password", chunks);
        Assert.Contains("[1] (document: password", prompt, StringComparison.Ordinal);
        Assert.EndsWith("how do I reset my password", prompt, StringComparison.Ordinal);

        Assert.True((await ingestor.RemoveAsync("faq", ["password"], CancellationToken.None)).IsSuccess);
        Assert.All((await retriever.RetrieveAsync(new KnowledgeQuery { Text = "reset password", Top = 5 }, CancellationToken.None)).ValueOrThrow(), chunk => Assert.Equal("billing", chunk.DocumentId));
    }
}
