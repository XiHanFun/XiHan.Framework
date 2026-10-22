// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace XiHan.Framework.Web.Api.Security;

/// <summary>
/// 把 <see cref="XiHanForwardedHeadersOptions"/> 落到 ASP.NET Core 的 <see cref="ForwardedHeadersOptions"/>
/// </summary>
/// <remarks>
/// 还原 X-Forwarded-For / Proto / Host；受信代理在缺省的本机回环之上追加配置项，配置写错时抛出 <see cref="InvalidOperationException"/>。
/// </remarks>
public sealed class XiHanForwardedHeadersConfigurator : IConfigureOptions<ForwardedHeadersOptions>
{
    private readonly XiHanForwardedHeadersOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanForwardedHeadersConfigurator(IOptions<XiHanForwardedHeadersOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public void Configure(ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedProto
            | ForwardedHeaders.XForwardedHost;

        if (_options.ForwardLimit < 1)
        {
            throw new InvalidOperationException($"配置 {XiHanForwardedHeadersOptions.SectionName}:ForwardLimit 至少为 1，当前为 {_options.ForwardLimit}。");
        }

        options.ForwardLimit = _options.ForwardLimit;

        foreach (var proxy in _options.KnownProxies.Where(static item => !string.IsNullOrWhiteSpace(item)))
        {
            if (!IPAddress.TryParse(proxy.Trim(), out var address))
            {
                throw new InvalidOperationException($"配置 {XiHanForwardedHeadersOptions.SectionName}:KnownProxies 里的 '{proxy}' 不是合法的 IP 地址。");
            }

            options.KnownProxies.Add(address);
        }

        foreach (var network in _options.KnownNetworks.Where(static item => !string.IsNullOrWhiteSpace(item)))
        {
            if (!TryParseNetwork(network.Trim(), out var parsed))
            {
                throw new InvalidOperationException($"配置 {XiHanForwardedHeadersOptions.SectionName}:KnownNetworks 里的 '{network}' 不是合法的 CIDR 网段：写成「地址/前缀长度」且主机位为 0，单个地址写进 KnownProxies。");
            }

            options.KnownIPNetworks.Add(parsed);
        }
    }

    /// <summary>
    /// 解析 CIDR 网段，主机位不为 0 时返回 false
    /// </summary>
    private static bool TryParseNetwork(string value, out System.Net.IPNetwork network)
    {
        var slash = value.IndexOf('/');
        return System.Net.IPNetwork.TryParse(value, out network)
            && slash > 0
            && IPAddress.TryParse(value[..slash], out var address)
            && address.Equals(network.BaseAddress);
    }
}
