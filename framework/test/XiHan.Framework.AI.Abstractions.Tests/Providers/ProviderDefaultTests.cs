// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.Abstractions.Tests.Fixtures;
using XiHan.Framework.AI.Conformance.Embeddings;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Providers;

/// <summary>
/// 默认 Provider 名由目录给出（RFC-0022「默认名」，ADR-0110）
/// </summary>
/// <remarks>
/// 框架自带的目录从配置推出默认名；应用替换的目录（这里的 Provider 表与默认名都不在应用配置里，模拟落库的目录）给出自己的默认名，
/// 聊天入口与知识库流水线在没写 Provider 时只问目录、不读配置
/// </remarks>
public sealed class ProviderDefaultTests
{
    /// <summary>
    /// 替换目录里的聊天 Provider
    /// </summary>
    private const string StoredChat = "stored-chat";

    /// <summary>
    /// 替换目录里的嵌入 Provider
    /// </summary>
    private const string StoredEmbedding = "stored-embedding";

    /// <summary>
    /// 嵌入维度
    /// </summary>
    private const int Dimensions = 64;

    [Fact]
    public void FrameworkCatalog_DerivesItsDefaultsFromConfiguration()
    {
        // 只配一个 Provider：它就是聊天与嵌入的默认
        using var single = Catalog(new AIOptions { Providers = Providers(StoredChat) });
        Assert.Equal(StoredChat, single.DefaultProvider);
        Assert.Equal(StoredChat, single.DefaultEmbeddingProvider);

        // 两个 Provider、不写默认键：没有默认，不写名字的解析是 AI_NOTFOUND_001，消息键分聊天与嵌入
        using var none = Catalog(new AIOptions { Providers = Providers(StoredChat, StoredEmbedding) });
        Assert.Null(none.DefaultProvider);
        Assert.Null(none.DefaultEmbeddingProvider);
        var chat = none.ResolveChatClient().Error;
        Assert.Equal(AIErrors.ProviderNotFound, chat?.Code);
        Assert.Equal(AIMessages.DefaultProviderMissing, chat?.MessageKey);
        var embedding = none.ResolveEmbeddingGenerator().Error;
        Assert.Equal(AIErrors.ProviderNotFound, embedding?.Code);
        Assert.Equal(AIMessages.DefaultEmbeddingProviderMissing, embedding?.MessageKey);

        // 只写 DefaultProvider：嵌入的默认同它；两个键各写一个：各是各的
        using var shared = Catalog(new AIOptions { Providers = Providers(StoredChat, StoredEmbedding), DefaultProvider = StoredChat });
        Assert.Equal(StoredChat, shared.DefaultEmbeddingProvider);
        using var separate = Catalog(new AIOptions { Providers = Providers(StoredChat, StoredEmbedding), DefaultProvider = StoredChat, DefaultEmbeddingProvider = StoredEmbedding });
        Assert.Equal(StoredChat, separate.DefaultProvider);
        Assert.Equal(StoredEmbedding, separate.DefaultEmbeddingProvider);

        // 默认名写错（不在 Provider 表里）：照常报未配置，带出名字
        using var wrong = Catalog(new AIOptions { Providers = Providers(StoredChat), DefaultProvider = "missing" });
        var missing = wrong.ResolveChatClient().Error;
        Assert.Equal(AIMessages.ProviderNotFound, missing?.MessageKey);
        Assert.Equal("missing", missing?.Details?["name"]);
    }

    [Fact]
    public async Task Chat_WithoutAProviderName_AsksTheCatalog_NotTheConfiguration()
    {
        var factory = new FakeChatClientFactory();
        await using var host = await StartAsync(factory, new AIOptions { Providers = Providers(StoredChat, StoredEmbedding), DefaultProvider = StoredChat, DefaultEmbeddingProvider = StoredEmbedding });
        // 应用配置里没有任何 Provider：按配置推默认名只会得到空
        Assert.Empty(host.Services.GetRequiredService<AIOptions>().Providers);

        await using var scope = host.Services.CreateAsyncScope();
        var reply = await scope.ServiceProvider.GetRequiredService<IAIChat>().CompleteAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "hi")] }, CancellationToken.None);
        Assert.True(reply.IsSuccess, $"The unnamed chat request failed: {reply.Error}.");
        Assert.Equal("pong", reply.ValueOrThrow().Text);
        Assert.Single(factory.Created);
    }

    [Fact]
    public async Task Knowledge_WithoutAProviderName_UsesTheCatalogsDefaultEmbeddingProvider_AfterTheKnowledgeSetting()
    {
        var store = new FakeKnowledgeStore();
        await using var host = await StartAsync(new FakeChatClientFactory(), new AIOptions { Providers = Providers(StoredChat, StoredEmbedding), DefaultProvider = StoredChat, DefaultEmbeddingProvider = StoredEmbedding }, store);
        var ingested = await host.Services.CreateScope().ServiceProvider.GetRequiredService<IKnowledgeIngestor>().IngestAsync([new KnowledgeIngestRequest { DocumentId = "faq", Text = "Reset the password from the settings page." }], CancellationToken.None);
        Assert.True(ingested[0].IsSuccess, $"The unnamed ingestion failed: {ingested[0].Error}.");
        Assert.NotEmpty(store.Records(KnowledgeOptions.DefaultCollection));

        var retrieved = await host.Services.CreateScope().ServiceProvider.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(new KnowledgeQuery { Text = "password" }, CancellationToken.None);
        Assert.True(retrieved.IsSuccess, $"The unnamed retrieval failed: {retrieved.Error}.");
        Assert.Equal("faq", retrieved.ValueOrThrow()[0].DocumentId);

        // 知识库配置写了嵌入 Provider 时先于目录的默认名：写的名字不在目录里即未配置
        await using var configured = await StartAsync(new FakeChatClientFactory(), new AIOptions { Providers = Providers(StoredChat, StoredEmbedding), DefaultEmbeddingProvider = StoredEmbedding }, new FakeKnowledgeStore(), ("XiHan:AI:Knowledge:EmbeddingProvider", "elsewhere"));
        var overridden = await configured.Services.CreateScope().ServiceProvider.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(new KnowledgeQuery { Text = "password" }, CancellationToken.None);
        Assert.Equal(AIMessages.ProviderNotFound, overridden.Error?.MessageKey);
        Assert.Equal("elsewhere", overridden.Error?.Details?["name"]);
    }

    [Fact]
    public async Task WithoutADefault_UnnamedChatAndKnowledgeRequests_AreProviderNotFound()
    {
        var factory = new FakeChatClientFactory();
        await using var host = await StartAsync(factory, new AIOptions { Providers = Providers(StoredChat, StoredEmbedding) }, new FakeKnowledgeStore());

        await using var scope = host.Services.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAIChat>();
        var unnamed = await chat.CompleteAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "hi")] }, CancellationToken.None);
        Assert.Equal(AIErrors.ProviderNotFound, unnamed.Error?.Code);
        Assert.Equal(AIMessages.DefaultProviderMissing, unnamed.Error?.MessageKey);
        Assert.Equal(AIMessages.DefaultProviderMissing, (await chat.StreamAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "hi")] }, CancellationToken.None)).Error?.MessageKey);
        Assert.Empty(factory.Created);

        var ingested = await host.Services.CreateScope().ServiceProvider.GetRequiredService<IKnowledgeIngestor>().IngestAsync([new KnowledgeIngestRequest { DocumentId = "faq", Text = "Reset the password." }], CancellationToken.None);
        Assert.Equal(AIErrors.ProviderNotFound, ingested[0].Error?.Code);
        Assert.Equal(AIMessages.DefaultEmbeddingProviderMissing, ingested[0].Error?.MessageKey);
        var retrieved = await host.Services.CreateScope().ServiceProvider.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(new KnowledgeQuery { Text = "password" }, CancellationToken.None);
        Assert.Equal(AIMessages.DefaultEmbeddingProviderMissing, retrieved.Error?.MessageKey);

        // 写了名字照常
        Assert.True((await chat.CompleteAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "hi")], Provider = StoredChat }, CancellationToken.None)).IsSuccess);
        Assert.True((await host.Services.CreateScope().ServiceProvider.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(new KnowledgeQuery { Text = "password", Provider = StoredEmbedding }, CancellationToken.None)).IsSuccess);
    }

    /// <summary>
    /// 替换目录的 Provider 表
    /// </summary>
    /// <remarks>
    /// <see cref="StoredEmbedding"/> 是 <c>hashing</c> 种类（嵌入），其余是 <c>fake</c> 种类（聊天）
    /// </remarks>
    private static Dictionary<string, AIProviderOptions> Providers(params string[] names)
        => names.ToDictionary(
            static name => name,
            static name => new AIProviderOptions(name, name == StoredEmbedding ? HashingEmbeddingGeneratorFactory.KindName : "fake") { ChatModel = "model-1", EmbeddingDimensions = Dimensions },
            StringComparer.Ordinal);

    /// <summary>
    /// 只装两种工厂的框架目录
    /// </summary>
    private static AIProviderCatalog Catalog(AIOptions options)
        => new(options, [new FakeChatClientFactory()], [new HashingEmbeddingGeneratorFactory()]);

    /// <summary>
    /// 启动替换了目录的宿主
    /// </summary>
    /// <remarks>
    /// 应用配置里没有 Provider，目录换成按 <paramref name="stored"/> 建的框架目录：它的 Provider 表与默认名都不来自应用配置
    /// </remarks>
    private static Task<TestHost> StartAsync(FakeChatClientFactory factory, AIOptions stored, FakeKnowledgeStore? store = null, params (string Key, string Value)[] extra)
        => TestHost.StartAsync(
            static b => b.UseFeature<HashingEmbeddingFeature>().UseFeature<AIToolsFeature>(),
            new (string Key, string Value)[] { ("XiHan:AI:Knowledge:Dimensions", "64") }.Concat(extra).Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)),
            services =>
            {
                services.AddSingleton<IChatClientFactory>(factory);
                // 先于特性登记：AddAI 以 TryAdd 登记框架目录，应用的目录优先
                services.AddSingleton<IAIProviderCatalog>(sp => new AIProviderCatalog(stored, sp.GetServices<IChatClientFactory>(), sp.GetServices<IEmbeddingGeneratorFactory>()));
                if (store is not null)
                {
                    services.AddSingleton<IKnowledgeStore>(store);
                }
            });
}
