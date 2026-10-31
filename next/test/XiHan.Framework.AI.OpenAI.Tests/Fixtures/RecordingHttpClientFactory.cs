// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.AI.OpenAI.Tests.Fixtures;

/// <summary>
/// 记录建出的客户端的 <see cref="IHttpClientFactory"/>。
/// </summary>
/// <remarks>
/// 每次按名字建一个以给定处理器发请求的客户端，<see cref="HttpClient.Timeout"/> 保持缺省的 100 秒，与工厂的缺省配置一致。
/// </remarks>
internal sealed class RecordingHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    /// <summary>
    /// 建出的客户端与请求的名字。
    /// </summary>
    public List<(string Name, HttpClient Client)> Created { get; } = [];

    /// <inheritdoc />
    public HttpClient CreateClient(string name)
    {
        var client = new HttpClient(handler, disposeHandler: false);
        Created.Add((name, client));
        return client;
    }
}
