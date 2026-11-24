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
/// 命名实例的端点可能来自应用的库（租户自己填的存储配置），不可信（ADR-0117）。其余与 SDK 自己的工厂一致：不自动解压，重定向、连接上限、两个超时与代理跟随客户端配置。缓存沿用基类的行为：每个 SDK 客户端一份，随它释放
/// 配了代理时过策略的是代理的地址（ADR-0281）：本进程建的连接都通向代理，连接回调看到的就是它；端点由代理去解析、去连，本进程查不到，要限制经代理能到的目标就在代理上做
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
        // Apply 关掉了代理（环境变量里的系统代理不该替租户的端点做主）；实例自己配了代理才重新打开：
        // 连接回调照旧每条连接都过，此时它看到并核对的是代理的地址。口令与 SDK 自己的工厂一样在建处理器时才交给代理对象
        if (clientConfig.GetWebProxy() is { } proxy)
        {
            if (clientConfig.ProxyCredentials is { } credentials)
            {
                proxy.Credentials = credentials;
            }

            handler.Proxy = proxy;
            handler.UseProxy = true;
        }

        var client = new HttpClient(handler, disposeHandler: true);
        if (clientConfig.Timeout is { } timeout)
        {
            client.Timeout = timeout;
        }

        return client;
    }
}
