// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Conformance.Providers;
using XiHan.Framework.AI.OpenAI.Tests.Fixtures;

namespace XiHan.Framework.AI.OpenAI.Tests;

/// <summary>
/// OpenAI Provider 对着仿真端点通过 Provider 目录套件
/// </summary>
/// <remarks>
/// 框架自带的目录从配置给出默认名：只配一个 Provider 时它就是默认；再配一个、不写 <c>DefaultProvider</c> 就没有默认；两个默认键各指一个就是分开的默认
/// </remarks>
public sealed class OpenAIProviderConformanceTests : AIProviderConformance
{
    /// <summary>
    /// 第二个 Provider 的名字
    /// </summary>
    private const string OtherProvider = "openai-embedding";

    /// <inheritdoc />
    protected override async Task<AIProviderHost> StartAsync()
    {
        var host = await OpenAITestHost.StartAsync(new FakeOpenAIHandler());
        return new AIProviderHost(host.Services, OpenAITestHost.ProviderName, OpenAITestHost.Dimensions, host);
    }

    /// <inheritdoc />
    protected override async Task<AIProviderHost> StartWithoutDefaultAsync()
    {
        var host = await OpenAITestHost.StartAsync(new FakeOpenAIHandler(), OtherProviderConfiguration());
        return new AIProviderHost(host.Services, OpenAITestHost.ProviderName, OpenAITestHost.Dimensions, host);
    }

    /// <inheritdoc />
    protected override async Task<AIProviderHost?> StartWithSeparateDefaultsAsync()
    {
        var host = await OpenAITestHost.StartAsync(
            new FakeOpenAIHandler(),
            [.. OtherProviderConfiguration(), ($"{AIOptions.ConfigurationSection}:DefaultProvider", OpenAITestHost.ProviderName), ($"{AIOptions.ConfigurationSection}:DefaultEmbeddingProvider", OtherProvider)]);
        return new AIProviderHost(host.Services, OpenAITestHost.ProviderName, OpenAITestHost.Dimensions, host) { EmbeddingProviderName = OtherProvider };
    }

    /// <summary>
    /// 第二个 Provider：同一个仿真端点、同样的维度
    /// </summary>
    private static (string Key, string Value)[] OtherProviderConfiguration() =>
    [
        ($"{AIOptions.ConfigurationSection}:Providers:{OtherProvider}:Kind", "openai"),
        ($"{AIOptions.ConfigurationSection}:Providers:{OtherProvider}:Endpoint", "http://localhost/v1"),
        ($"{AIOptions.ConfigurationSection}:Providers:{OtherProvider}:ApiKey", "sk-test"),
        ($"{AIOptions.ConfigurationSection}:Providers:{OtherProvider}:ChatModel", "chat-model"),
        ($"{AIOptions.ConfigurationSection}:Providers:{OtherProvider}:EmbeddingModel", "embedding-model"),
        ($"{AIOptions.ConfigurationSection}:Providers:{OtherProvider}:EmbeddingDimensions", OpenAITestHost.Dimensions.ToString(CultureInfo.InvariantCulture)),
    ];
}
