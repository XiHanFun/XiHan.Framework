// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Security;

/// <summary>
/// 受信反向代理配置：转发头（X-Forwarded-For / Proto / Host）只在请求来自这些代理时才被采信
/// </summary>
/// <remarks>
/// 本机回环（127.0.0.0/8、::1）始终受信，这里配置的是追加项；其他来源带的转发头一律忽略。
/// </remarks>
public class XiHanForwardedHeadersOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Web:Api:ForwardedHeaders";

    /// <summary>
    /// 受信代理的地址，如 <c>10.0.0.5</c>、<c>2001:db8::5</c>
    /// </summary>
    public List<string> KnownProxies { get; set; } = [];

    /// <summary>
    /// 受信代理所在的网段（CIDR），如 <c>172.18.0.0/16</c>；主机位须为 0
    /// </summary>
    public List<string> KnownNetworks { get; set; } = [];

    /// <summary>
    /// 最多从 X-Forwarded-For 右端剥几层受信代理，缺省 1；多级代理（如 CDN → 负载均衡 → nginx）按层数调大
    /// </summary>
    public int ForwardLimit { get; set; } = 1;
}
