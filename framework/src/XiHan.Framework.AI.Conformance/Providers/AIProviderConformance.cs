// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.AI.Conformance.Providers;

/// <summary>
/// 模型 Provider 的一致性套件（RFC-0022 §Provider 目录）
/// </summary>
/// <remarks>
/// 目录按名字解析聊天与嵌入、嵌入维度、缓存与 <c>Invalidate</c>、未知名字，以及默认名：有默认、没有默认、聊天与嵌入分开的默认
/// Provider 继承并实现 <see cref="StartAsync"/> 与 <see cref="StartWithoutDefaultAsync"/>（指向真实或仿真端点）；目录支持聊天与嵌入分开设默认时还要覆写 <see cref="StartWithSeparateDefaultsAsync"/>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class AIProviderConformance
{
    /// <summary>
    /// 启动装配了被测 Provider 的夹具
    /// </summary>
    /// <remarks>
    /// 被测 Provider <see cref="AIProviderHost.ProviderName"/> 同时是目录的默认聊天与默认嵌入 Provider
    /// </remarks>
    protected abstract Task<AIProviderHost> StartAsync();

    /// <summary>
    /// 启动目录里有被测 Provider、但没有默认 Provider 的夹具
    /// </summary>
    /// <remarks>
    /// 目录的 <see cref="IAIProviderCatalog.DefaultProvider"/> 与 <see cref="IAIProviderCatalog.DefaultEmbeddingProvider"/> 都应为空；按名字仍解析得出被测 Provider
    /// </remarks>
    protected abstract Task<AIProviderHost> StartWithoutDefaultAsync();

    /// <summary>
    /// 启动聊天与嵌入分开设默认的夹具
    /// </summary>
    /// <remarks>
    /// 默认聊天 Provider 是 <see cref="AIProviderHost.ProviderName"/>，默认嵌入 Provider 是另一个 Provider <see cref="AIProviderHost.EmbeddingProviderName"/>
    /// 只支持一个默认的目录不覆写，返回 <see langword="null"/>，对应用例跳过
    /// </remarks>
    protected virtual Task<AIProviderHost?> StartWithSeparateDefaultsAsync() => Task.FromResult<AIProviderHost?>(null);

    /// <summary>
    /// 用例 1：聊天客户端能应答，嵌入生成器给出配置维度的向量，批量嵌入条数与输入一致
    /// </summary>
    [Fact]
    public async Task Catalog_ResolvesChatAndEmbedding()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var catalog = host.Services.GetRequiredService<IAIProviderCatalog>();
        Assert.Contains(host.ProviderName, catalog.Providers);
        Assert.True(catalog.Find(host.ProviderName).HasValue);

        var chat = catalog.ResolveChatClient(host.ProviderName).ValueOrThrow();
        var reply = await chat.GetResponseAsync("ping", cancellationToken: CancellationToken.None).ConfigureAwait(false);
        Assert.False(string.IsNullOrWhiteSpace(reply.Text));

        var embeddings = catalog.ResolveEmbeddingGenerator(host.ProviderName).ValueOrThrow();
        var generated = await embeddings.GenerateAsync(["alpha", "beta"], cancellationToken: CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(2, generated.Count);
        Assert.All(generated, embedding => Assert.Equal(host.EmbeddingDimensions, embedding.Vector.Length));
    }

    /// <summary>
    /// 用例 2：同名解析返回同一实例，<c>Invalidate</c> 后重建
    /// </summary>
    /// <remarks>
    /// 未知名字 <c>AI_NOTFOUND_001</c>
    /// </remarks>
    [Fact]
    public async Task Catalog_CachesUntilInvalidated_AndRejectsUnknownNames()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var catalog = host.Services.GetRequiredService<IAIProviderCatalog>();
        var first = catalog.ResolveChatClient(host.ProviderName).ValueOrThrow();
        Assert.Same(first, catalog.ResolveChatClient(host.ProviderName).ValueOrThrow());
        var generator = catalog.ResolveEmbeddingGenerator(host.ProviderName).ValueOrThrow();
        Assert.Same(generator, catalog.ResolveEmbeddingGenerator(host.ProviderName).ValueOrThrow());

        catalog.Invalidate(host.ProviderName);
        Assert.NotSame(first, catalog.ResolveChatClient(host.ProviderName).ValueOrThrow());
        Assert.NotSame(generator, catalog.ResolveEmbeddingGenerator(host.ProviderName).ValueOrThrow());
        catalog.Invalidate();
        Assert.True(catalog.ResolveChatClient(host.ProviderName).IsSuccess);

        Assert.Equal(AIErrors.ProviderNotFound, catalog.ResolveChatClient("no-such-provider").Error?.Code);
        Assert.Equal(AIErrors.ProviderNotFound, catalog.ResolveEmbeddingGenerator("no-such-provider").Error?.Code);
    }

    /// <summary>
    /// 用例 3：有默认时不写名字即按默认解析
    /// </summary>
    /// <remarks>
    /// 被测 Provider 是默认：两个默认名都是它，不写名字的解析与写名字的是同一实例，聊天入口不写 Provider 也由它应答
    /// 聊天入口只问目录的默认名（RFC-0022「默认名」）：目录不是从配置读出的也一样
    /// </remarks>
    [Fact]
    public async Task Catalog_WithADefault_ResolvesUnnamedRequestsToIt()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var catalog = host.Services.GetRequiredService<IAIProviderCatalog>();
        Assert.Equal(host.ProviderName, catalog.DefaultProvider);
        Assert.Equal(host.ProviderName, catalog.DefaultEmbeddingProvider);
        Assert.Same(catalog.ResolveChatClient(host.ProviderName).ValueOrThrow(), catalog.ResolveChatClient().ValueOrThrow());
        Assert.Same(catalog.ResolveEmbeddingGenerator(host.ProviderName).ValueOrThrow(), catalog.ResolveEmbeddingGenerator().ValueOrThrow());

        var reply = await CompleteAsync(host, provider: null).ConfigureAwait(false);
        Assert.True(reply.IsSuccess, $"An unnamed chat request failed: {reply.Error}.");
        Assert.False(string.IsNullOrWhiteSpace(reply.ValueOrThrow().Text));
    }

    /// <summary>
    /// 用例 4：没有默认时不写名字即未找到
    /// </summary>
    /// <remarks>
    /// 两个默认名都为空，不写名字的解析与聊天入口都是 <c>AI_NOTFOUND_001</c>（聊天与嵌入各一个消息键），写了名字照常
    /// </remarks>
    [Fact]
    public async Task Catalog_WithoutADefault_RejectsUnnamedRequests_AndServesNamedOnes()
    {
        await using var host = await StartWithoutDefaultAsync().ConfigureAwait(false);
        var catalog = host.Services.GetRequiredService<IAIProviderCatalog>();
        Assert.Null(catalog.DefaultProvider);
        Assert.Null(catalog.DefaultEmbeddingProvider);

        AssertDefaultMissing(catalog.ResolveChatClient().Error, AIMessages.DefaultProviderMissing);
        AssertDefaultMissing(catalog.ResolveEmbeddingGenerator().Error, AIMessages.DefaultEmbeddingProviderMissing);
        AssertDefaultMissing((await CompleteAsync(host, provider: null).ConfigureAwait(false)).Error, AIMessages.DefaultProviderMissing);

        Assert.True(catalog.ResolveChatClient(host.ProviderName).IsSuccess);
        Assert.True(catalog.ResolveEmbeddingGenerator(host.ProviderName).IsSuccess);
        var named = await CompleteAsync(host, host.ProviderName).ConfigureAwait(false);
        Assert.True(named.IsSuccess, $"A chat request naming '{host.ProviderName}' failed: {named.Error}.");
    }

    /// <summary>
    /// 用例 5：聊天与嵌入分开的默认各按各的解析
    /// </summary>
    /// <remarks>
    /// 不写名字的聊天按默认聊天 Provider、嵌入按默认嵌入 Provider 解析；没有覆写 <see cref="StartWithSeparateDefaultsAsync"/> 时跳过
    /// </remarks>
    [Fact]
    public async Task Catalog_WithSeparateDefaults_ResolvesChatAndEmbeddingEachToItsOwn()
    {
        var started = await StartWithSeparateDefaultsAsync().ConfigureAwait(false);
        Assert.SkipWhen(started is null, "The catalog keeps one default for chat and embedding; override StartWithSeparateDefaultsAsync when it can set them apart.");
        await using var host = started!;
        // 夹具必须在 EmbeddingProviderName 里给出默认嵌入 Provider，且不同于默认聊天 Provider
        var embeddingProvider = host.EmbeddingProviderName;
        Assert.NotNull(embeddingProvider);
        Assert.NotEqual(host.ProviderName, embeddingProvider);

        var catalog = host.Services.GetRequiredService<IAIProviderCatalog>();
        Assert.Equal(host.ProviderName, catalog.DefaultProvider);
        Assert.Equal(embeddingProvider, catalog.DefaultEmbeddingProvider);
        Assert.Same(catalog.ResolveChatClient(host.ProviderName).ValueOrThrow(), catalog.ResolveChatClient().ValueOrThrow());
        var generator = catalog.ResolveEmbeddingGenerator().ValueOrThrow();
        Assert.Same(catalog.ResolveEmbeddingGenerator(embeddingProvider).ValueOrThrow(), generator);

        var generated = await generator.GenerateAsync(["alpha"], cancellationToken: CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(host.EmbeddingDimensions, Assert.Single(generated).Vector.Length);
        var reply = await CompleteAsync(host, provider: null).ConfigureAwait(false);
        Assert.True(reply.IsSuccess, $"An unnamed chat request failed: {reply.Error}.");
    }

    /// <summary>
    /// 经聊天入口发一次 <c>ping</c>
    /// </summary>
    /// <remarks>
    /// 聊天入口是作用域服务：在新作用域里取用。<paramref name="provider"/> 为空表示不写 Provider
    /// </remarks>
    private static async Task<Result<ChatResponse>> CompleteAsync(AIProviderHost host, string? provider)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAIChat>();
        return await chat.CompleteAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "ping")], Provider = provider }, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 没给名字而没有默认：<c>AI_NOTFOUND_001</c> 与给定的消息键
    /// </summary>
    private static void AssertDefaultMissing(Error? error, string messageKey)
    {
        Assert.NotNull(error);
        Assert.Equal(AIErrors.ProviderNotFound, error.Code);
        Assert.Equal(messageKey, error.MessageKey);
    }
}
