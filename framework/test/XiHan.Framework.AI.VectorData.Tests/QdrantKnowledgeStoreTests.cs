// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using CommunityToolkit.VectorData.Qdrant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Qdrant.Client;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Conformance.Knowledge;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// 真实 Qdrant 上的知识库（RFC-0022、ADR-0111）
/// </summary>
/// <remarks>
/// 存储套件全部用例，加上摄入、检索、重新摄入与删除走完一遍，以及连不上时的健康项；进程内替身接受任何键，只有真实连接器才会暴露键类型与过滤翻译的问题。未设置 <c>XIHAN_TEST_QDRANT</c> 时跳过
/// </remarks>
public sealed class QdrantKnowledgeStoreTests : KnowledgeStoreConformance
{
    /// <inheritdoc />
    protected override async Task<KnowledgeStoreHost> StartAsync()
    {
        var host = await QdrantTestHost.StartAsync();
        return new KnowledgeStoreHost(host.Host.Services.GetRequiredService<IKnowledgeStore>(), VectorDataTestHost.Dimensions, host);
    }

    [Fact]
    public async Task Ingest_Retrieve_Reingest_AndRemove()
    {
        await using var qdrant = await QdrantTestHost.StartAsync(("XiHan:AI:Knowledge:Collection", "faq-" + Guid.NewGuid().ToString("N")[..8]));
        var ingestor = qdrant.Host.Services.CreateScope().ServiceProvider.GetRequiredService<IKnowledgeIngestor>();
        var retriever = qdrant.Host.Services.CreateScope().ServiceProvider.GetRequiredService<IKnowledgeRetriever>();
        Assert.Equal(LifecycleState.Ready, Assert.Single(qdrant.Host.App.Health().Contributions, static c => c.Name == VectorDataKnowledgeFeature.HealthName).State);

        var ingested = await ingestor.IngestAsync(
        [
            new KnowledgeIngestRequest { DocumentId = "password", Text = "To reset your password open Settings, choose Security and press Reset password. A reset link is sent by email.", Metadata = new Dictionary<string, string> { ["topic"] = "account" } },
            new KnowledgeIngestRequest { DocumentId = "billing", Text = "Invoices are issued monthly. Billing questions go to the finance team by email.", Metadata = new Dictionary<string, string> { ["topic"] = "finance" } },
        ], CancellationToken.None);
        var passwordChunks = ingested[0].ValueOrThrow();
        Assert.True(passwordChunks > 1);
        ingested[1].ValueOrThrow();

        // 命中还原字符串记录 ID 与元数据：对外的形状不随向量键变化
        var chunks = (await retriever.RetrieveAsync(new KnowledgeQuery { Text = "how do I reset my password", Top = 2 }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(2, chunks.Count);
        Assert.StartsWith("password#", chunks[0].Id, StringComparison.Ordinal);
        Assert.Equal("password", chunks[0].DocumentId);
        Assert.Equal("account", chunks[0].Metadata["topic"]);

        var billingOnly = (await retriever.RetrieveAsync(new KnowledgeQuery { Text = "reset password", Top = 5, Filter = new RetrievalFilter { DocumentIds = ["billing"] } }, CancellationToken.None)).ValueOrThrow();
        Assert.NotEmpty(billingOnly);
        Assert.All(billingOnly, static chunk => Assert.Equal("billing", chunk.DocumentId));

        // 重新摄入变短：确定性的键覆盖留下的块，多出来的旧块被裁掉
        var reingested = await ingestor.IngestAsync([new KnowledgeIngestRequest { DocumentId = "password", Text = "Press Reset password." }], CancellationToken.None);
        Assert.Equal(1, reingested[0].ValueOrThrow());
        var remaining = (await retriever.RetrieveAsync(new KnowledgeQuery { Text = "reset password", Top = 5, Filter = new RetrievalFilter { DocumentIds = ["password"] } }, CancellationToken.None)).ValueOrThrow();
        var only = Assert.Single(remaining);
        Assert.Equal(("password#0", "Press Reset password."), (only.Id, only.Text));

        Assert.True((await ingestor.RemoveAsync(null, ["password"], CancellationToken.None)).IsSuccess);
        Assert.Empty((await retriever.RetrieveAsync(new KnowledgeQuery { Text = "reset password", Top = 5, Filter = new RetrievalFilter { DocumentIds = ["password"] } }, CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task UnreachableServer_ReportsTheExceptionType_AndLogsTheException()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(QdrantTestHost.EnvironmentVariable)), $"Set the environment variable '{QdrantTestHost.EnvironmentVariable}' to run the tests against the real connector.");
        var logs = new LogCapture();
        // 端口 1 上没有服务：真实连接器抛出的异常，应用照常启动
        using var store = new QdrantVectorStore(new QdrantClient(new Uri("http://127.0.0.1:1")), ownsClient: true);
        await using var host = await VectorDataTestHost.StartAsync(store, null, logs);

        var status = Assert.Single(host.App.Health().Contributions, static c => c.Name == VectorDataKnowledgeFeature.HealthName);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(AIErrors.KnowledgeUnavailable, status.Error?.Code);
        Assert.Equal(typeof(VectorStoreException).FullName, status.Error?.Details?["reason"]);
        var logged = Assert.Single(logs.Entries, static entry => entry.Category == typeof(VectorDataKnowledgeStore).FullName);
        Assert.Equal(LogLevel.Warning, logged.Level);
        Assert.IsType<VectorStoreException>(logged.Exception);

        // 存储操作同样：错误只带类型，完整异常进日志
        var search = await host.Services.GetRequiredService<IKnowledgeStore>().SearchAsync("knowledge", new KnowledgeSearch(new float[VectorDataTestHost.Dimensions], 1), CancellationToken.None);
        Assert.Equal(AIErrors.KnowledgeUnavailable, search.Error?.Code);
        Assert.Equal(typeof(VectorStoreException).FullName, search.Error?.Details?["reason"]);
        var failed = logs.Entries.Where(static entry => entry.Category == typeof(VectorDataKnowledgeStore).FullName).ToList();
        Assert.Equal(2, failed.Count);
        Assert.IsType<VectorStoreException>(failed[1].Exception);
    }
}
