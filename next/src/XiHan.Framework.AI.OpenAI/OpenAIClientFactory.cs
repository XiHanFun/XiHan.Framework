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
/// 端点、凭据、网络超时，以及注册了命名 <see cref="HttpClient"/> 时的传输。
/// </remarks>
internal sealed class OpenAIClientFactory
{
    /// <summary>
    /// 命名 HttpClient 的名字。
    /// </summary>
    /// <remarks>
    /// 注册了它（<c>services.AddHttpClient(OpenAIClientFactory.HttpClientName)</c>）就经它发请求。
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

        // 注册了命名 HttpClient 才替换传输：测试与代理场景用，默认走 SDK 自己的传输
        var http = _httpClients?.CreateClient(HttpClientName);
        if (http is not null)
        {
            clientOptions.Transport = new HttpClientPipelineTransport(http);
        }

        return new OpenAIClient(new ApiKeyCredential(string.IsNullOrWhiteSpace(options.ApiKey) ? PlaceholderApiKey : options.ApiKey), clientOptions);
    }
}
