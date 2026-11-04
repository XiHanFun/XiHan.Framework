// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 出站地址策略的放行项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Security:Outbound</c> 显式读取，不经反射绑定；装配时读一次，改了要重启应用。全部缺省时只能连公网地址，见 <see cref="OutboundAddressPolicy"/>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OutboundAddressOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Security:Outbound";

    /// <summary>
    /// 是否放行回环地址（<c>127.0.0.0/8</c>、<c>::1</c>）
    /// </summary>
    /// <remarks>
    /// 本地开发用，生产环境不要打开：回环地址上通常是不设防的管理端口
    /// </remarks>
    public bool AllowLoopback { get; init; }

    /// <summary>
    /// 是否放行私网地址（<c>10.0.0.0/8</c>、<c>172.16.0.0/12</c>、<c>192.168.0.0/16</c>、<c>fc00::/7</c>）
    /// </summary>
    /// <remarks>
    /// 不包含链路本地地址、运营商级 NAT 地址与云元数据地址，它们只能由 <see cref="AllowedNetworks"/> 放行。只需要内网里的几个服务时，用 <see cref="AllowedHosts"/> 或 <see cref="AllowedNetworks"/> 收窄范围
    /// </remarks>
    public bool AllowPrivateNetwork { get; init; }

    /// <summary>
    /// 放行的网段
    /// </summary>
    /// <remarks>
    /// 优先于全部拒绝规则，单个地址写成 <c>/32</c> 或 <c>/128</c>
    /// </remarks>
    public IReadOnlyList<IPNetwork> AllowedNetworks { get; init; } = [];

    /// <summary>
    /// 放行的主机名（不区分大小写，整名相等，不支持通配符）
    /// </summary>
    /// <remarks>
    /// 只看请求里写的主机名，不再看它解析到哪里，所以这个名字的 DNS 要是可信的。国际化域名写 ASCII 形式
    /// </remarks>
    public IReadOnlyList<string> AllowedHosts { get; init; } = [];

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// 缺省键保持默认值，非法值抛出 <see cref="ArgumentException"/>，宿主启动失败
    /// </remarks>
    public static OutboundAddressOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        return new OutboundAddressOptions
        {
            AllowLoopback = ReadSwitch(section, nameof(AllowLoopback)),
            AllowPrivateNetwork = ReadSwitch(section, nameof(AllowPrivateNetwork)),
            AllowedNetworks = [.. section.GetSection(nameof(AllowedNetworks)).GetChildren().Select(ParseNetwork)],
            AllowedHosts = [.. section.GetSection(nameof(AllowedHosts)).GetChildren().Select(ParseHost)],
        };
    }

    /// <summary>
    /// 读一个开关
    /// </summary>
    private static bool ReadSwitch(IConfigurationSection section, string key)
    {
        var raw = section[key];
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        return bool.TryParse(raw, out var value) ? value : throw new ArgumentException($"'{section.Path}:{key}' must be 'true' or 'false', but was '{raw}'.");
    }

    /// <summary>
    /// 解析一个网段
    /// </summary>
    /// <remarks>
    /// <see cref="IPNetwork"/> 把 <c>10.0.0.1/8</c> 静默掩码成 <c>10.0.0.0/8</c>。在放行项里这多半是手误，悄悄开放整段比报错糟，所以要求主机位为零
    /// </remarks>
    private static IPNetwork ParseNetwork(IConfigurationSection item)
        => item.Value is { } text && IPNetwork.TryParse(text, out var network) && IPAddress.Parse(text.AsSpan(0, text.IndexOf('/'))).Equals(network.BaseAddress)
            ? network
            : throw new ArgumentException($"'{item.Path}' must be a network in CIDR notation with no host bits set, such as '10.1.0.0/16', but was '{item.Value}'.");

    /// <summary>
    /// 解析一个主机名
    /// </summary>
    /// <remarks>
    /// 写成网址或带端口的条目永远匹配不上，放任不管只会变成运行时被拒绝，这里直接报错
    /// </remarks>
    private static string ParseHost(IConfigurationSection item)
        => item.Value is { Length: > 0 } host && Uri.CheckHostName(host) != UriHostNameType.Unknown ? host : throw new ArgumentException($"'{item.Path}' must be a host name without scheme or port, but was '{item.Value}'.");
}
