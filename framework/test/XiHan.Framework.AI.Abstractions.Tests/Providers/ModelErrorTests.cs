// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.Abstractions.Tests.Fixtures;
using XiHan.Framework.AI.Conformance.Embeddings;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Providers;

/// <summary>
/// 模型 Provider 的错误（RFC-0022、ADR-0113）
/// </summary>
/// <remarks>
/// 聊天与嵌入调用抛出时，错误里的 <c>reason</c> 只写异常类型，完整异常进日志；超时不算失败、不记日志
/// </remarks>
public sealed class ModelErrorTests
{
    /// <summary>
    /// Provider 的异常消息里带着不该外露的地址
    /// </summary>
    private const string Secret = "llm-gateway.internal:8443";

    [Fact]
    public async Task Chat_ReportsOnlyTheExceptionType_AndLogsTheFullException()
    {
        var logs = new LogCapture();
        var factory = new FakeChatClientFactory();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<HashingEmbeddingFeature>(),
            [new("XiHan:AI:Providers:fake:Kind", "fake"), new("XiHan:AI:Providers:fake:ChatModel", "model-1")],
            services => services.AddSingleton<IChatClientFactory>(factory).AddSingleton<ILoggerFactory>(logs));
        await using var scope = host.Services.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAIChat>();
        var request = new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "hi")], Provider = "fake" };
        Assert.True((await chat.CompleteAsync(request, CancellationToken.None)).IsSuccess);
        var client = Assert.Single(factory.Created);

        var fault = new InvalidOperationException($"POST https://{Secret}/chat failed.");
        client.Throw = fault;
        var failed = await chat.CompleteAsync(request, CancellationToken.None);

        AssertOnlyTheType(failed.Error, typeof(InvalidOperationException), "fake");
        var logged = Assert.Single(ModelEntries(logs, typeof(AIChat)));
        Assert.Equal(LogLevel.Warning, logged.Level);
        Assert.Same(fault, logged.Exception);
        Assert.Contains("'fake'", logged.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, logged.Message, StringComparison.Ordinal);

        // 超时有自己的错误码，不是模型调用失败，也不记这条日志
        client.Throw = new TimeoutException();
        Assert.Equal(AIErrors.ModelTimeout, (await chat.CompleteAsync(request, CancellationToken.None)).Error?.Code);
        Assert.Single(ModelEntries(logs, typeof(AIChat)));
    }

    [Fact]
    public async Task Embedding_ReportsOnlyTheExceptionType_AndLogsTheFullException()
    {
        var logs = new LogCapture();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<HashingEmbeddingFeature>(),
            [new("XiHan:AI:Providers:broken:Kind", FailingEmbeddingGeneratorFactory.KindName)],
            services =>
            {
                services.AddAIEmbeddingGeneratorFactory<FailingEmbeddingGeneratorFactory>();
                services.AddSingleton<IKnowledgeStore>(new FakeKnowledgeStore());
                services.AddSingleton<ILoggerFactory>(logs);
            });

        var ingested = await host.Services.GetRequiredService<IKnowledgeIngestor>().IngestAsync(new KnowledgeIngestRequest { DocumentId = "doc", Text = "text", Provider = "broken" }, CancellationToken.None);
        AssertOnlyTheType(ingested.Error, typeof(HttpRequestException), "broken");
        var ingesting = Assert.Single(ModelEntries(logs, typeof(KnowledgeIngestor)));
        Assert.Equal(LogLevel.Warning, ingesting.Level);
        Assert.IsType<HttpRequestException>(ingesting.Exception);

        var retrieved = await host.Services.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(new KnowledgeQuery { Text = "text", Provider = "broken" }, CancellationToken.None);
        AssertOnlyTheType(retrieved.Error, typeof(HttpRequestException), "broken");
        var retrieving = Assert.Single(ModelEntries(logs, typeof(KnowledgeRetriever)));
        Assert.Equal(LogLevel.Warning, retrieving.Level);
        Assert.IsType<HttpRequestException>(retrieving.Exception);
    }

    /// <summary>
    /// 错误码、消息键与 Provider 名照旧，<c>reason</c> 只有类型全名，文案里没有异常消息
    /// </summary>
    private static void AssertOnlyTheType(Error? error, Type exception, string provider)
    {
        Assert.NotNull(error);
        Assert.Equal(AIErrors.ModelUnavailable, error.Code);
        Assert.Equal(AIMessages.ModelUnavailable, error.MessageKey);
        Assert.Equal(provider, error.Details?["provider"]);
        Assert.Equal(exception.FullName, error.Details?["reason"]);
        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("unreachable", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 某个类别下带异常的日志
    /// </summary>
    private static List<(LogLevel Level, string Category, string Message, Exception? Exception)> ModelEntries(LogCapture logs, Type category)
        => logs.Entries.Where(entry => entry.Category == category.FullName && entry.Exception is not null).ToList();
}
