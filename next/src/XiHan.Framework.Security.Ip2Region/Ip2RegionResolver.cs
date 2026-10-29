// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;
using IP2Region.Net.XDB;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Network;

namespace XiHan.Framework.Security.Ip2Region;

/// <summary>
/// 基于 ip2region 离线库的归属地解析。
/// </summary>
/// <remarks>
/// IPv4 与 IPv6 各一个 xdb，首次用到时整库读进内存（<c>CachePolicy.Content</c>，线程安全，单次查询约百纳秒）。某个地址族的库文件不存在或加载失败时记一条警告，之后该族一律无值；回环、私有网段（IPv4 的 10/8、172.16/12、192.168/16，IPv6 的链路本地、站点本地、组播）不查。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class Ip2RegionResolver : IIpRegionResolver, IDisposable
{
    /// <summary>
    /// IPv4 查询。
    /// </summary>
    /// <remarks>
    /// 地址文本 → 记录文本；库不可用时值为 <see langword="null"/>。
    /// </remarks>
    private readonly Lazy<Func<string, string?>?> _v4;

    /// <summary>
    /// IPv6 查询。
    /// </summary>
    /// <remarks>
    /// 地址文本 → 记录文本；库不可用时值为 <see langword="null"/>。
    /// </remarks>
    private readonly Lazy<Func<string, string?>?> _v6;

    /// <summary>
    /// 已加载、需要释放的查询器。
    /// </summary>
    private readonly List<IDisposable> _loaded = [];

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 按选项里的库文件创建。
    /// </summary>
    /// <remarks>
    /// 文件在首次解析该地址族时才加载。
    /// </remarks>
    /// <param name="options">选项。</param>
    /// <param name="loggerFactory">日志工厂；内核总会登记一个（没有日志时为空实现）。</param>
    public Ip2RegionResolver(Ip2RegionOptions options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<Ip2RegionResolver>();
        _v4 = new Lazy<Func<string, string?>?>(() => Load("IPv4", Ip2RegionOptions.Resolve(options.V4Database)), LazyThreadSafetyMode.ExecutionAndPublication);
        _v6 = new Lazy<Func<string, string?>?>(() => Load("IPv6", Ip2RegionOptions.Resolve(options.V6Database)), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// 以给定查询创建（测试用）。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示该地址族的库不可用。
    /// </remarks>
    internal Ip2RegionResolver(Func<string, string?>? v4, Func<string, string?>? v6, ILogger logger)
    {
        _logger = logger;
        _v4 = new Lazy<Func<string, string?>?>(v4);
        _v6 = new Lazy<Func<string, string?>?>(v6);
    }

    /// <inheritdoc />
    public Maybe<IpRegion> Resolve(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (IPAddress.IsLoopback(normalized) || IsPrivate(normalized))
        {
            return Maybe.None<IpRegion>();
        }

        var search = normalized.AddressFamily switch
        {
            AddressFamily.InterNetwork => _v4.Value,
            AddressFamily.InterNetworkV6 => _v6.Value,
            _ => null,
        };
        if (search is null)
        {
            return Maybe.None<IpRegion>();
        }

        try
        {
            return Ip2RegionRecord.Parse(search(normalized.ToString())) is { } region ? Maybe.Some(region) : Maybe.None<IpRegion>();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // 查询失败（库损坏、地址超出库的范围等）不影响调用方：归属地当作取不到
            LogLookupFailed(_logger, exception);
            return Maybe.None<IpRegion>();
        }
    }

    /// <summary>
    /// 释放已加载的查询器。
    /// </summary>
    public void Dispose()
    {
        lock (_loaded)
        {
            foreach (var searcher in _loaded)
            {
                searcher.Dispose();
            }

            _loaded.Clear();
        }
    }

    /// <summary>
    /// 是否私有网段。
    /// </summary>
    /// <remarks>
    /// IPv4 的 10/8、172.16/12、192.168/16，IPv6 的链路本地、站点本地与组播。
    /// </remarks>
    private static bool IsPrivate(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) || (bytes[0] == 192 && bytes[1] == 168);
    }

    /// <summary>
    /// 加载一个地址族的库。
    /// </summary>
    /// <remarks>
    /// 文件不存在或加载失败时记警告并返回 <see langword="null"/>。
    /// </remarks>
    private Func<string, string?>? Load(string family, string path)
    {
        if (!File.Exists(path))
        {
            LogDatabaseMissing(_logger, family, path);
            return null;
        }

        try
        {
            var searcher = new Searcher(CachePolicy.Content, path);
            if (searcher is IDisposable disposable)
            {
                lock (_loaded)
                {
                    _loaded.Add(disposable);
                }
            }

            return ip => searcher.Search(ip);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            LogDatabaseUnloadable(_logger, exception, family, path);
            return null;
        }
    }

    /// <summary>
    /// 库文件不存在。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The ip2region {Family} database {Path} does not exist; addresses of this family will have no region.")]
    private static partial void LogDatabaseMissing(ILogger logger, string family, string path);

    /// <summary>
    /// 库文件加载失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The ip2region {Family} database {Path} could not be loaded; addresses of this family will have no region.")]
    private static partial void LogDatabaseUnloadable(ILogger logger, Exception exception, string family, string path);

    /// <summary>
    /// 单次查询失败。
    /// </summary>
    /// <remarks>
    /// 地址不进日志。
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Debug, Message = "An ip2region lookup failed; the address is treated as having no region.")]
    private static partial void LogLookupFailed(ILogger logger, Exception exception);
}
