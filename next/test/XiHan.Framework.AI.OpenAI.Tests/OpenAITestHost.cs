// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.OpenAI.Tests;

/// <summary>
/// 启动装配了 OpenAI Provider、工具目录与回显操作的宿主，请求经仿真端点。
/// </summary>
internal static class OpenAITestHost
{
    /// <summary>
    /// Provider 名。
    /// </summary>
    public const string ProviderName = "openai";

    /// <summary>
    /// 嵌入维度。
    /// </summary>
    public const int Dimensions = 8;

    /// <summary>
    /// 启动。
    /// </summary>
    public static Task<TestHost> StartAsync(FakeOpenAIHandler handler, params (string Key, string Value)[] extra)
    {
        (string Key, string Value)[] configuration =
        [
            ($"{AIOptions.ConfigurationSection}:Providers:{ProviderName}:Kind", "openai"),
            ($"{AIOptions.ConfigurationSection}:Providers:{ProviderName}:Endpoint", "http://localhost/v1"),
            ($"{AIOptions.ConfigurationSection}:Providers:{ProviderName}:ApiKey", "sk-test"),
            ($"{AIOptions.ConfigurationSection}:Providers:{ProviderName}:ChatModel", "chat-model"),
            ($"{AIOptions.ConfigurationSection}:Providers:{ProviderName}:EmbeddingModel", "embedding-model"),
            ($"{AIOptions.ConfigurationSection}:Providers:{ProviderName}:EmbeddingDimensions", Dimensions.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ];
        return TestHost.StartAsync(
            static b => b.UseFeature<AIToolsFeature>().UseFeature<OpenAIFeature>().UseFeature<EchoFeature>(),
            configuration.Concat(extra).Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)),
            services => services.AddHttpClient("xihan.ai.openai").ConfigurePrimaryHttpMessageHandler(() => handler));
    }
}
