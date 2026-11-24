// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.AI.Abstractions.Providers;

namespace XiHan.Framework.AI.Abstractions.Tests.Providers;

/// <summary>
/// 回放 <c>provider-defaults.json</c>：框架自带的 Provider 目录给出的默认名
/// </summary>
/// <remarks>
/// 规范 RFC-0022「Provider 目录」的「默认名」（ADR-0110、ADR-0277）：默认聊天 Provider 是配置的 <c>DefaultProvider</c>，没写且只配了一个 Provider 时是它，否则没有；默认嵌入 Provider 是配置的 <c>DefaultEmbeddingProvider</c>，没写时同默认聊天 Provider。没有默认时不给名字的解析是未配置的错误码，消息键分聊天与嵌入。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class ProviderDefaultVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ProviderDefaultVectors _vectors = ConformanceVectors.Load<ProviderDefaultVectors>("provider-defaults.json");

    [Fact]
    public void TheCodeAndTheMessageKeys_AreTheOnesInTheVectors()
    {
        Assert.Equal(AIErrors.ProviderNotFound, _vectors.Code);
        Assert.Equal(AIMessages.DefaultProviderMissing, _vectors.ChatMessageKey);
        Assert.Equal(AIMessages.DefaultEmbeddingProviderMissing, _vectors.EmbeddingMessageKey);
    }

    [Fact]
    public void Options_ResolveTheDefaultsOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var options = OptionsOf(vector);
            var actual = (options.ResolveDefaultProvider(), options.ResolveDefaultEmbeddingProvider());
            if (actual != (vector.Chat, vector.Embedding))
            {
                failures.Add($"{vector.Name}: resolved {Describe(actual.Item1)} for chat and {Describe(actual.Item2)} for embedding, the vector has {Describe(vector.Chat)} and {Describe(vector.Embedding)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Catalog_GivesTheDefaults_AndFailsUnnamedResolutionWithoutOne()
    {
        foreach (var vector in _vectors.Cases)
        {
            // 默认名与「没有默认」的错误都由目录给出；用例不涉及建客户端，目录不装工厂
            using var catalog = new AIProviderCatalog(OptionsOf(vector), [], []);
            Assert.Equal(vector.Providers.Order(StringComparer.Ordinal), catalog.Providers.Order(StringComparer.Ordinal));
            Assert.Equal(vector.Chat, catalog.DefaultProvider);
            Assert.Equal(vector.Embedding, catalog.DefaultEmbeddingProvider);

            // 没有默认：不给名字的解析是向量给的错误码，消息键分聊天与嵌入
            if (vector.Chat is null)
            {
                var chat = catalog.ResolveChatClient().Error;
                Assert.Equal((_vectors.Code, _vectors.ChatMessageKey), (chat?.Code, chat?.MessageKey));
            }

            if (vector.Embedding is null)
            {
                var embedding = catalog.ResolveEmbeddingGenerator().Error;
                Assert.Equal((_vectors.Code, _vectors.EmbeddingMessageKey), (embedding?.Code, embedding?.MessageKey));
            }

            // 默认名不保证在已配置的 Provider 里：按它解析时与别的未配置的名字一样
            if (vector.Chat is { } chatDefault && !vector.Providers.Contains(chatDefault, StringComparer.Ordinal))
            {
                var unconfigured = catalog.ResolveChatClient("some-unconfigured-name").Error;
                var byDefault = catalog.ResolveChatClient().Error;
                Assert.Equal(_vectors.Code, byDefault?.Code);
                Assert.Equal((unconfigured?.Code, unconfigured?.MessageKey), (byDefault?.Code, byDefault?.MessageKey));
            }

            if (vector.Embedding is { } embeddingDefault && !vector.Providers.Contains(embeddingDefault, StringComparer.Ordinal))
            {
                var unconfigured = catalog.ResolveEmbeddingGenerator("some-unconfigured-name").Error;
                var byDefault = catalog.ResolveEmbeddingGenerator().Error;
                Assert.Equal(_vectors.Code, byDefault?.Code);
                Assert.Equal((unconfigured?.Code, unconfigured?.MessageKey), (byDefault?.Code, byDefault?.MessageKey));
            }
        }
    }

    [Fact]
    public void TheVectors_CoverEveryWayADefaultComesAbout()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过：写明的、只配一个推出来的、配了几个而没有的、一个都没配的
        Assert.Contains(_vectors.Cases, static vector => vector.DefaultProvider is not null && vector.Chat == vector.DefaultProvider);
        Assert.Contains(_vectors.Cases, static vector => vector is { DefaultProvider: null, Providers.Length: 1 } && vector.Chat == vector.Providers[0]);
        Assert.Contains(_vectors.Cases, static vector => vector is { DefaultProvider: null, Providers.Length: > 1, Chat: null });
        Assert.Contains(_vectors.Cases, static vector => vector is { Providers.Length: 0, Chat: null, Embedding: null });

        // 嵌入：跟着聊天的默认、单独写明而聊天没有默认；默认名不在已配置的 Provider 里
        Assert.Contains(_vectors.Cases, static vector => vector.DefaultEmbeddingProvider is null && vector.Embedding is not null && vector.Embedding == vector.Chat);
        Assert.Contains(_vectors.Cases, static vector => vector.DefaultEmbeddingProvider is not null && vector.Chat is null && vector.Embedding == vector.DefaultEmbeddingProvider);
        Assert.Contains(_vectors.Cases, static vector => vector.Chat is { } chat && !vector.Providers.Contains(chat, StringComparer.Ordinal));
    }

    /// <summary>
    /// 把用例写成配置再读成选项
    /// </summary>
    /// <remarks>
    /// 用例说的是配置里 <c>XiHan:AI:Providers</c> 下的名字与两个默认键，所以经 <see cref="AIOptions.FromConfiguration"/> 读；Provider 的种类必填，统一写 <c>fake</c>
    /// </remarks>
    private static AIOptions OptionsOf(ProviderDefaultCase vector)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var provider in vector.Providers)
        {
            values[$"{AIOptions.ConfigurationSection}:Providers:{provider}:Kind"] = "fake";
        }

        if (vector.DefaultProvider is { } chat)
        {
            values[$"{AIOptions.ConfigurationSection}:DefaultProvider"] = chat;
        }

        if (vector.DefaultEmbeddingProvider is { } embedding)
        {
            values[$"{AIOptions.ConfigurationSection}:DefaultEmbeddingProvider"] = embedding;
        }

        return AIOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }

    /// <summary>
    /// 失败信息里的默认名
    /// </summary>
    private static string Describe(string? name)
        => name is null ? "no default" : $"'{name}'";

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Code">没有默认或默认名未配置时解析的错误码</param>
    /// <param name="ChatMessageKey">没有默认聊天 Provider 的消息键</param>
    /// <param name="EmbeddingMessageKey">没有默认嵌入 Provider 的消息键</param>
    /// <param name="Cases">用例</param>
    private sealed record ProviderDefaultVectors(string Code, string ChatMessageKey, string EmbeddingMessageKey, ProviderDefaultCase[] Cases);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Providers">配置里 <c>XiHan:AI:Providers</c> 下的名字</param>
    /// <param name="DefaultProvider">配置的默认聊天 Provider；<see langword="null"/> 表示没写</param>
    /// <param name="DefaultEmbeddingProvider">配置的默认嵌入 Provider；<see langword="null"/> 表示没写</param>
    /// <param name="Chat">目录的默认聊天 Provider 名；<see langword="null"/> 表示没有默认</param>
    /// <param name="Embedding">目录的默认嵌入 Provider 名；<see langword="null"/> 表示没有默认</param>
    private sealed record ProviderDefaultCase(string Name, string[] Providers, string? DefaultProvider, string? DefaultEmbeddingProvider, string? Chat, string? Embedding);
}
