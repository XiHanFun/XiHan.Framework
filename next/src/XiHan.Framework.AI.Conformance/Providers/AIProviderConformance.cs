// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.AI.Conformance.Providers;

/// <summary>
/// 模型 Provider 的一致性套件（RFC-0022 §Provider 目录）。
/// </summary>
/// <remarks>
/// 目录按名字解析聊天与嵌入、嵌入维度、缓存与 <c>Invalidate</c>、未知名字。Provider 继承并实现 <see cref="StartAsync"/>（指向真实或仿真端点）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class AIProviderConformance
{
    /// <summary>
    /// 启动装配了被测 Provider 的夹具。
    /// </summary>
    protected abstract Task<AIProviderHost> StartAsync();

    /// <summary>
    /// 用例 1：聊天客户端能应答，嵌入生成器给出配置维度的向量，批量嵌入条数与输入一致。
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
    /// 用例 2：同名解析返回同一实例，<c>Invalidate</c> 后重建。
    /// </summary>
    /// <remarks>
    /// 未知名字 <c>AI_NOTFOUND_001</c>。
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
}
