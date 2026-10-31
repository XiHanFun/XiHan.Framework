// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI;
using XiHan.Framework.AI.Abstractions.Providers;

namespace XiHan.Framework.AI.OpenAI;

/// <summary>
/// 按 Provider 配置创建 <see cref="OpenAIClient"/>。
/// </summary>
/// <remarks>
/// 端点、凭据、网络超时，以及容器里有 <see cref="IHttpClientFactory"/> 时经命名客户端的传输。超时只以 <see cref="AIProviderOptions.Timeout"/> 为准。
/// </remarks>
internal sealed class OpenAIClientFactory
{
    /// <summary>
    /// 命名 HttpClient 的名字。
    /// </summary>
    /// <remarks>
    /// 容器里有 <see cref="IHttpClientFactory"/>（应用在任何地方调用过 <c>AddHttpClient</c>）就经这个名字的客户端发请求，没有专门配置这个名字时取到的是工厂的缺省客户端；<c>services.AddHttpClient(OpenAIClientFactory.HttpClientName)</c> 可以给它配置处理器与代理。
    /// </remarks>
    public const string HttpClientName = "xihan.ai.openai";

    /// <summary>
    /// <see cref="AIProviderOptions.ApiKey"/> 为空时的占位凭据：本地兼容端点通常不校验。
    /// </summary>
    public const string PlaceholderApiKey = "no-key";

    /// <summary>
    /// HttpClient 工厂（可无）。
    /// </summary>
    private readonly IHttpClientFactory? _httpClients;

    /// <summary>
    /// 创建。
    /// </summary>
    public OpenAIClientFactory(IHttpClientFactory? httpClients = null)
    {
        _httpClients = httpClients;
    }

    /// <summary>
    /// 创建客户端。
    /// </summary>
    public OpenAIClient Create(AIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var clientOptions = new OpenAIClientOptions { NetworkTimeout = options.Timeout };
        if (options.Endpoint is not null)
        {
            clientOptions.Endpoint = options.Endpoint;
        }

        // 容器里有 HttpClient 工厂就经它的命名客户端发请求（代理、处理器、测试替身），没有时走 SDK 自己的传输。
        // 工厂建出的客户端带缺省 100 秒的 Timeout，会截断更长的 TimeoutSeconds：与 SDK 自己的传输一样把它置为不限，超时只由上面的 NetworkTimeout 决定
        var http = _httpClients?.CreateClient(HttpClientName);
        if (http is not null)
        {
            http.Timeout = Timeout.InfiniteTimeSpan;
            clientOptions.Transport = new HttpClientPipelineTransport(http);
        }

        return new OpenAIClient(new ApiKeyCredential(string.IsNullOrWhiteSpace(options.ApiKey) ? PlaceholderApiKey : options.ApiKey), clientOptions);
    }
}
