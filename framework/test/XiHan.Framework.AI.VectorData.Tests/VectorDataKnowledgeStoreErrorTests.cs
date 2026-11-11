// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// 存储操作的错误（RFC-0022、ADR-0112）
/// </summary>
/// <remarks>
/// 连接器抛出时，错误里的 <c>reason</c> 只写异常类型，完整异常进日志；取消照常抛出
/// </remarks>
public sealed class VectorDataKnowledgeStoreErrorTests
{
    /// <summary>
    /// 连接器的异常消息里带着不该外露的地址
    /// </summary>
    private const string Secret = "qdrant-prod.internal:6334";

    [Fact]
    public async Task EveryOperation_ReportsOnlyTheExceptionType_AndLogsTheFullException()
    {
        var logs = new LogCapture();
        var fault = new HttpRequestException($"Connection refused by {Secret}.");
        var store = new TestVectorStore();
        await using var host = await VectorDataTestHost.StartAsync(store, null, logs);
        var knowledge = host.Services.GetRequiredService<IKnowledgeStore>();
        var vector = new float[VectorDataTestHost.Dimensions];
        var record = new KnowledgeRecord { Id = "doc#0", DocumentId = "doc", ChunkIndex = 0, Text = "text", Vector = vector };
        store.OperationFault = fault;

        Error?[] errors =
        [
            (await knowledge.UpsertAsync("faq", [record], CancellationToken.None)).Error,
            (await knowledge.DeleteAsync("faq", ["doc"], CancellationToken.None)).Error,
            (await knowledge.TrimAsync("faq", [new KnowledgeTrim("doc", 0)], CancellationToken.None)).Error,
            (await knowledge.SearchAsync("faq", new KnowledgeSearch(vector, 1), CancellationToken.None)).Error,
        ];

        Assert.All(errors, static error =>
        {
            Assert.NotNull(error);
            Assert.Equal(AIErrors.KnowledgeUnavailable, error.Code);
            Assert.Equal(AIMessages.KnowledgeUnavailable, error.MessageKey);
            Assert.Equal(typeof(HttpRequestException).FullName, error.Details?["reason"]);
            Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
        });

        // 每次失败一条 Warning，带操作名、集合名与完整异常：排查靠它
        var logged = logs.Entries.Where(static entry => entry.Category == typeof(VectorDataKnowledgeStore).FullName).ToList();
        Assert.Equal(4, logged.Count);
        Assert.All(logged, entry =>
        {
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Same(fault, entry.Exception);
            Assert.Contains("'faq'", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, entry.Message, StringComparison.Ordinal);
        });
        foreach (var (operation, entry) in new[] { nameof(IKnowledgeStore.UpsertAsync), nameof(IKnowledgeStore.DeleteAsync), nameof(IKnowledgeStore.TrimAsync), nameof(IKnowledgeStore.SearchAsync) }.Zip(logged))
        {
            Assert.Contains(operation, entry.Message, StringComparison.Ordinal);
        }

        // 探针只走存储级调用，存储操作失败不影响健康项，也不记探测日志
        Assert.True(host.App.Health().Ready);
    }

    [Fact]
    public async Task Cancellation_IsThrown_AndNotLoggedAsAFailure()
    {
        var logs = new LogCapture();
        var store = new TestVectorStore();
        await using var host = await VectorDataTestHost.StartAsync(store, null, logs);
        var knowledge = host.Services.GetRequiredService<IKnowledgeStore>();
        store.OperationFault = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await knowledge.SearchAsync("faq", new KnowledgeSearch(new float[VectorDataTestHost.Dimensions], 1), CancellationToken.None));

        Assert.DoesNotContain(logs.Entries, static entry => entry.Category == typeof(VectorDataKnowledgeStore).FullName);
    }
}
