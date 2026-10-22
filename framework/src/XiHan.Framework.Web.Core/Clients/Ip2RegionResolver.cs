// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using XiHan.Framework.Web.Core.Options;

namespace XiHan.Framework.Web.Core.Clients;

/// <summary>
/// 基于 ip2region 离线库的归属地解析（单例，线程安全）
/// </summary>
/// <remarks>
/// IPv4 与 IPv6 各用一个 xdb（<see cref="XiHanClientInfoOptions.Ip2RegionDbPath"/>、<see cref="XiHanClientInfoOptions.Ip2RegionV6DbPath"/>），
/// 首次用到时整库载入内存（<see cref="CachePolicy.Content"/>，查一次几十到一百多纳秒）。
/// 库文件不存在或加载失败时该地址族解析为空，不影响请求。
/// </remarks>
public sealed class Ip2RegionResolver : IIpRegionResolver, IDisposable
{
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly XiHanClientInfoOptions _options;
    private readonly ILogger<Ip2RegionResolver> _logger;
    private readonly Lazy<ISearcher?> _v4Searcher;
    private readonly Lazy<ISearcher?> _v6Searcher;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="hostingEnvironment">宿主环境（相对路径按内容根解析）</param>
    /// <param name="options">客户端信息选项</param>
    /// <param name="logger">日志</param>
    public Ip2RegionResolver(
        IWebHostEnvironment hostingEnvironment,
        IOptions<XiHanClientInfoOptions> options,
        ILogger<Ip2RegionResolver> logger)
    {
        _hostingEnvironment = hostingEnvironment;
        _options = options.Value;
        _logger = logger;
        _v4Searcher = new Lazy<ISearcher?>(
            () => CreateSearcher("IPv4", _options.Ip2RegionDbPath, "IpDatabases/ip2region.xdb", "IpDatabases/ip2region_v4.xdb", "ip2region.xdb"),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _v6Searcher = new Lazy<ISearcher?>(
            () => CreateSearcher("IPv6", _options.Ip2RegionV6DbPath, "IpDatabases/ip2region_v6.xdb"),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public IpRegion? Resolve(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!_options.EnableIpRegion)
        {
            return null;
        }

        var normalized = IpAddressScope.Normalize(address);
        if (IpAddressScope.IsLoopback(normalized) || IpAddressScope.IsPrivate(normalized))
        {
            return null;
        }

        var searcher = normalized.AddressFamily switch
        {
            AddressFamily.InterNetwork => _v4Searcher.Value,
            AddressFamily.InterNetworkV6 => _v6Searcher.Value,
            _ => null
        };
        if (searcher is null)
        {
            return null;
        }

        try
        {
            return IpRegion.TryParse(searcher.Search(normalized.ToString()), out var region) ? region : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "IP 归属地解析失败，IP: {IpAddress}", normalized);
            return null;
        }
    }

    /// <summary>
    /// 释放已加载的查询器
    /// </summary>
    public void Dispose()
    {
        foreach (var searcher in new[] { _v4Searcher, _v6Searcher })
        {
            if (searcher.IsValueCreated && searcher.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private ISearcher? CreateSearcher(string family, string? configuredPath, params string[] fallbackPaths)
    {
        var candidates = new[] { configuredPath }
            .Concat(fallbackPaths)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.IsPathRooted(path!.Trim()) ? path.Trim() : Path.Combine(_hostingEnvironment.ContentRootPath, path.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var dbPath in candidates)
        {
            if (!File.Exists(dbPath))
            {
                continue;
            }

            try
            {
                return new Searcher(CachePolicy.Content, dbPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载 {Family} 归属地库失败。Path: {Path}", family, dbPath);
            }
        }

        _logger.LogDebug("未找到可用的 {Family} 归属地库，跳过该地址族的归属地解析。Candidates: {Candidates}", family, string.Join("; ", candidates));
        return null;
    }
}
