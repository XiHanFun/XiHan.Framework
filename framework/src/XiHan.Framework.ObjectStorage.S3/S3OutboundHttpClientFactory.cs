// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using Amazon.Runtime;
using XiHan.Framework.Kernel.Security;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 建出过出站地址策略的 HTTP 客户端
/// </summary>
/// <remarks>
/// 命名实例的端点可能来自应用的库（租户自己填的存储配置），不可信（ADR-0117）。其余与 SDK 自己的工厂一致：不自动解压，重定向、连接上限与两个超时跟随客户端配置。缓存沿用基类的行为：每个 SDK 客户端一份，随它释放
/// </remarks>
internal sealed class S3OutboundHttpClientFactory(OutboundAddressPolicy addresses) : HttpClientFactory
{
    /// <inheritdoc />
    public override HttpClient CreateHttpClient(IClientConfig clientConfig)
    {
        ArgumentNullException.ThrowIfNull(clientConfig);
        var handler = new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.None, AllowAutoRedirect = clientConfig.AllowAutoRedirect };
        if (clientConfig.MaxConnectionsPerServer is { } connections)
        {
            handler.MaxConnectionsPerServer = connections;
        }

        if (clientConfig.ConnectTimeout is { } connectTimeout)
        {
            handler.ConnectTimeout = connectTimeout;
        }

        addresses.Apply(handler);
        var client = new HttpClient(handler, disposeHandler: true);
        if (clientConfig.Timeout is { } timeout)
        {
            client.Timeout = timeout;
        }

        return client;
    }
}
