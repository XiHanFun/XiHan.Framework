// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 出站地址策略：向用户给的地址发请求时，决定能连哪些 IP
/// </summary>
/// <remarks>
/// 防服务端请求伪造（RFC-0008「出站地址策略」、ADR-0117）：缺省只能连公网地址，回环、私网、链路本地与其余不可公网路由的地址都拒绝，经 <see cref="OutboundAddressOptions"/> 放行。宿主登记为单例
/// 检查放在建立连接时，按解析出的实际 IP 判：只查 URL 或事先解析一次都会被 DNS 重绑定绕过。用法是 <see cref="Apply"/> 到 <see cref="SocketsHttpHandler"/> 上，命名客户端写 <c>.UseSocketsHttpHandler(static (handler, services) => services.GetRequiredService&lt;OutboundAddressPolicy&gt;().Apply(handler))</c>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class OutboundAddressPolicy
{
    /// <summary>
    /// 回环地址
    /// </summary>
    private static readonly IPNetwork[] _loopback = [IPNetwork.Parse("127.0.0.0/8"), IPNetwork.Parse("::1/128")];

    /// <summary>
    /// 落在私网段里的云元数据地址：AWS 的 IPv6 元数据服务
    /// </summary>
    /// <remarks>
    /// 其他云的元数据地址在链路本地或运营商级 NAT 段，本来就不属于私网
    /// </remarks>
    private static readonly IPNetwork[] _metadata = [IPNetwork.Parse("fd00:ec2::254/128")];

    /// <summary>
    /// 私网地址
    /// </summary>
    private static readonly IPNetwork[] _private = [IPNetwork.Parse("10.0.0.0/8"), IPNetwork.Parse("172.16.0.0/12"), IPNetwork.Parse("192.168.0.0/16"), IPNetwork.Parse("fc00::/7")];

    /// <summary>
    /// 其余不可公网路由的 IPv4 地址
    /// </summary>
    /// <remarks>
    /// 依次是本网络（在 Linux 上 <c>0.0.0.0</c> 连到本机）、运营商级 NAT（阿里云元数据 <c>100.100.100.200</c> 在这里）、链路本地（多数云的元数据 <c>169.254.169.254</c>）、IETF 协议（Oracle 元数据 <c>192.0.0.192</c>）、文档、6to4 中继、测试、文档、文档、组播、保留与广播
    /// </remarks>
    private static readonly IPNetwork[] _specialIPv4 =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("192.0.0.0/24"),
        IPNetwork.Parse("192.0.2.0/24"),
        IPNetwork.Parse("192.88.99.0/24"),
        IPNetwork.Parse("198.18.0.0/15"),
        IPNetwork.Parse("198.51.100.0/24"),
        IPNetwork.Parse("203.0.113.0/24"),
        IPNetwork.Parse("224.0.0.0/4"),
        IPNetwork.Parse("240.0.0.0/4"),
    ];

    /// <summary>
    /// 全球单播的 IPv6 地址
    /// </summary>
    /// <remarks>
    /// IPv6 按白名单判：链路本地、站点本地、组播等都不在这个范围里
    /// </remarks>
    private static readonly IPNetwork _globalIPv6 = IPNetwork.Parse("2000::/3");

    /// <summary>
    /// 全球单播范围里不可公网路由的 IPv6 地址：IETF 协议（含 Teredo）与两段文档地址
    /// </summary>
    private static readonly IPNetwork[] _specialIPv6 = [IPNetwork.Parse("2001::/23"), IPNetwork.Parse("2001:db8::/32"), IPNetwork.Parse("3fff::/20")];

    /// <summary>
    /// 6to4：第 3～6 字节是 IPv4 地址
    /// </summary>
    private static readonly IPNetwork _sixToFour = IPNetwork.Parse("2002::/16");

    /// <summary>
    /// NAT64：后 4 字节是 IPv4 地址
    /// </summary>
    private static readonly IPNetwork _nat64 = IPNetwork.Parse("64:ff9b::/96");

    /// <summary>
    /// 已废弃的 IPv4 兼容地址：后 4 字节是 IPv4 地址
    /// </summary>
    private static readonly IPNetwork _ipv4Compatible = IPNetwork.Parse("::/96");

    /// <summary>
    /// 放行项
    /// </summary>
    private readonly OutboundAddressOptions _options;

    /// <summary>
    /// 放行的主机名
    /// </summary>
    private readonly HashSet<string> _allowedHosts;

    /// <summary>
    /// 解析主机名
    /// </summary>
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 连接回调
    /// </summary>
    /// <remarks>
    /// 只建一个委托实例，重复 <see cref="Apply"/> 时能认出是自己
    /// </remarks>
    private readonly Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> _connect;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="options">放行项</param>
    /// <param name="loggerFactory">日志工厂（拒绝时记一条 Warning，带解析出的地址）</param>
    public OutboundAddressPolicy(OutboundAddressOptions options, ILoggerFactory loggerFactory)
        : this(options, loggerFactory, Dns.GetHostAddressesAsync)
    {
    }

    /// <summary>
    /// 创建，主机名由 <paramref name="resolve"/> 解析
    /// </summary>
    internal OutboundAddressPolicy(OutboundAddressOptions options, ILoggerFactory loggerFactory, Func<string, CancellationToken, Task<IPAddress[]>> resolve)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _allowedHosts = new HashSet<string>(options.AllowedHosts, StringComparer.OrdinalIgnoreCase);
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        _logger = loggerFactory.CreateLogger<OutboundAddressPolicy>();
        _connect = ConnectAsync;
    }

    /// <summary>
    /// 这个 IP 能否连接
    /// </summary>
    /// <remarks>
    /// 不考虑 <see cref="OutboundAddressOptions.AllowedHosts"/>：主机名的放行只在连接时才有主机名可看
    /// </remarks>
    public bool IsAllowed(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var target = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        // 顺序有讲究：运维明确放行的网段最先；回环在内嵌判定之前，::1 本身也落在 IPv4 兼容段里；元数据在私网之前，开私网不等于开元数据
        if (Contains(_options.AllowedNetworks, target))
        {
            return true;
        }

        if (Contains(_loopback, target))
        {
            return _options.AllowLoopback;
        }

        if (Contains(_metadata, target))
        {
            return false;
        }

        if (Contains(_private, target))
        {
            return _options.AllowPrivateNetwork;
        }

        // 内嵌 IPv4 的 IPv6 地址经转换机制可能到达 IPv4 内网，按内嵌的地址判
        return EmbeddedIPv4(target) is { } embedded ? IsAllowed(embedded) : IsGlobal(target);
    }

    /// <summary>
    /// 让这个处理器的连接都过策略
    /// </summary>
    /// <remarks>
    /// 设置 <see cref="SocketsHttpHandler.ConnectCallback"/>，同时关掉 <see cref="SocketsHttpHandler.UseProxy"/>：经代理时回调看到的是代理的地址，查不到真正的目标。之后再打开代理就绕过了策略，要用代理就在代理上做这层限制
    /// HTTP/3 走 QUIC，不经过连接回调；请求缺省是 HTTP/1.1，把 <see cref="HttpRequestMessage.Version"/> 设成 3.0 的请求不受保护
    /// </remarks>
    /// <exception cref="InvalidOperationException">处理器已经有别的连接回调：两者只能有一个，覆盖会丢掉应用的回调，保留则策略失效</exception>
    public void Apply(SocketsHttpHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (handler.ConnectCallback is not null && handler.ConnectCallback != _connect)
        {
            throw new InvalidOperationException("The handler already has a connect callback; the outbound address policy cannot replace it.");
        }

        handler.ConnectCallback = _connect;
        handler.UseProxy = false;
    }

    /// <summary>
    /// 建立连接：解析主机名，只连策略允许的地址
    /// </summary>
    /// <remarks>
    /// 这次连接用的就是刚检查过的地址，不会再解析一次，DNS 重绑定没有窗口。处理器的每条新连接都走这里，重定向的目标也一样
    /// </remarks>
    private async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint;
        var resolved = await _resolve(endpoint.Host, cancellationToken).ConfigureAwait(false);
        if (resolved.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        // 一部分地址被拒绝时只连其余的：连的都是检查过的地址，混入公网地址绕不过去，也不会因为多返回一个内网地址就连不上
        var allowed = _allowedHosts.Contains(endpoint.Host) ? resolved : Array.FindAll(resolved, IsAllowed);
        if (allowed.Length == 0)
        {
            LogDenied(_logger, endpoint.Host, endpoint.Port, string.Join<IPAddress>(", ", resolved));
            throw new OutboundAddressDeniedException(endpoint.Host, endpoint.Port);
        }

        if (allowed.Length < resolved.Length)
        {
            var skipped = string.Join(", ", resolved.Except(allowed));
            LogPartlyDenied(_logger, endpoint.Host, endpoint.Port, skipped);
        }

        // 与处理器缺省的连接方式一样按顺序逐个试，最后一个的异常原样抛出
        for (var index = 0; ; index++)
        {
            Socket? socket = null;
            try
            {
                // 建套接字也可能失败：系统没有开 IPv6 时，这个地址跳过，换下一个
                socket = new Socket(allowed[index].AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                await socket.ConnectAsync(new IPEndPoint(allowed[index], endpoint.Port), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException) when (index < allowed.Length - 1)
            {
                socket?.Dispose();
            }
            catch
            {
                socket?.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// 地址是否落在这些网段里
    /// </summary>
    private static bool Contains(IReadOnlyList<IPNetwork> networks, IPAddress address)
    {
        for (var index = 0; index < networks.Count; index++)
        {
            if (networks[index].Contains(address))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// IPv6 地址内嵌的 IPv4 地址
    /// </summary>
    private static IPAddress? EmbeddedIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return null;
        }

        Span<byte> bytes = stackalloc byte[16];
        _ = address.TryWriteBytes(bytes, out _);
        if (_sixToFour.Contains(address))
        {
            return new IPAddress(bytes.Slice(2, 4));
        }

        return _nat64.Contains(address) || _ipv4Compatible.Contains(address) ? new IPAddress(bytes.Slice(12, 4)) : null;
    }

    /// <summary>
    /// 是否公网可路由的地址
    /// </summary>
    private static bool IsGlobal(IPAddress address)
        => address.AddressFamily == AddressFamily.InterNetwork
            ? !Contains(_specialIPv4, address)
            : _globalIPv6.Contains(address) && !Contains(_specialIPv6, address);

    /// <summary>
    /// 全部地址都被拒绝
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The outbound address policy denied the connection to '{Host}:{Port}', which resolved to {Addresses}.")]
    private static partial void LogDenied(ILogger logger, string host, int port, string addresses);

    /// <summary>
    /// 一部分地址被拒绝
    /// </summary>
    [LoggerMessage(Level = LogLevel.Debug, Message = "The outbound address policy skipped {Addresses} when connecting to '{Host}:{Port}'.")]
    private static partial void LogPartlyDenied(ILogger logger, string host, int port, string addresses);
}
