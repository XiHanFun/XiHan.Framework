#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HttpContextClientInfoProvider
// Guid:45df2829-f8cf-44ac-bfce-b960402f67ed
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/07 14:40:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Net;
using System.Net.Sockets;
using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using Microsoft.Extensions.Options;
using UAParser;
using XiHan.Framework.Web.Core.Options;

namespace XiHan.Framework.Web.Core.Clients;

/// <summary>
/// 基于 HttpContext 的客户端信息提供器
/// </summary>
public sealed class HttpContextClientInfoProvider : IClientInfoProvider, IDisposable
{
    private static readonly Parser UserAgentParser = Parser.GetDefault();
    private readonly object _searcherSyncRoot = new();
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly XiHanClientInfoOptions _options;
    private readonly ILogger<HttpContextClientInfoProvider> _logger;
    private ISearcher? _ipSearcher;
    private bool _searcherInitialized;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpContextAccessor"></param>
    /// <param name="hostingEnvironment"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public HttpContextClientInfoProvider(
        IHttpContextAccessor httpContextAccessor,
        IWebHostEnvironment hostingEnvironment,
        IOptions<XiHanClientInfoOptions> options,
        ILogger<HttpContextClientInfoProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _hostingEnvironment = hostingEnvironment;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前客户端信息
    /// </summary>
    /// <returns></returns>
    public ClientInfo GetCurrent()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var ipAddress = ResolveClientIp(httpContext);
        var userAgent = NormalizeToNull(httpContext?.Request.Headers.UserAgent.ToString());
        var (browser, operatingSystem, deviceName) = ParseUserAgent(userAgent);

        return new ClientInfo
        {
            IpAddress = ipAddress,
            Location = ResolveLocation(ipAddress),
            UserAgent = userAgent,
            Browser = browser,
            OperatingSystem = operatingSystem,
            DeviceName = deviceName
        };
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_ipSearcher is IDisposable disposableSearcher)
        {
            disposableSearcher.Dispose();
        }
    }

    /// <summary>
    /// 解析客户端IP
    /// </summary>
    /// <param name="httpContext"></param>
    /// <returns></returns>
    private static string? ResolveClientIp(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var candidate = forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var normalized = NormalizeIp(candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        var realIp = NormalizeIp(httpContext.Request.Headers["X-Real-IP"].ToString());
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp;
        }

        return NormalizeIp(httpContext.Connection.RemoteIpAddress?.ToString());
    }

    /// <summary>
    /// 解析浏览器、系统、设备
    /// </summary>
    /// <param name="userAgent"></param>
    /// <returns></returns>
    private static (string? Browser, string? OperatingSystem, string? DeviceName) ParseUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return (null, null, null);
        }

        try
        {
            var parsed = UserAgentParser.Parse(userAgent);
            return (
                ComposeName(parsed.UA?.Family, parsed.UA?.Major, parsed.UA?.Minor, parsed.UA?.Patch),
                ComposeName(parsed.OS?.Family, parsed.OS?.Major, parsed.OS?.Minor, parsed.OS?.Patch),
                NormalizeDeviceName(parsed.Device?.Family));
        }
        catch
        {
            return (null, null, null);
        }
    }

    /// <summary>
    /// 解析IP地理位置
    /// </summary>
    /// <param name="ipAddress"></param>
    /// <returns></returns>
    private string? ResolveLocation(string? ipAddress)
    {
        if (!_options.EnableIpRegion || string.IsNullOrWhiteSpace(ipAddress) || IsPrivateOrLoopbackIp(ipAddress))
        {
            return null;
        }

        var searcher = GetOrCreateSearcher();
        if (searcher is null)
        {
            return null;
        }

        try
        {
            var region = searcher.Search(ipAddress);
            return NormalizeRegion(region);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "IP地理位置解析失败，IP: {IpAddress}", ipAddress);
            return null;
        }
    }

    /// <summary>
    /// 获取或创建IP查询器
    /// </summary>
    /// <returns></returns>
    private ISearcher? GetOrCreateSearcher()
    {
        if (_searcherInitialized)
        {
            return _ipSearcher;
        }

        lock (_searcherSyncRoot)
        {
            if (_searcherInitialized)
            {
                return _ipSearcher;
            }

            _ipSearcher = CreateSearcher();
            _searcherInitialized = true;
            return _ipSearcher;
        }
    }

    /// <summary>
    /// 创建IP查询器
    /// </summary>
    /// <returns></returns>
    private ISearcher? CreateSearcher()
    {
        var configuredPath = NormalizeToNull(_options.Ip2RegionDbPath);
        if (configuredPath is null)
        {
            return null;
        }

        var dbPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_hostingEnvironment.ContentRootPath, configuredPath);

        if (!File.Exists(dbPath))
        {
            _logger.LogDebug("未找到 ip2region 数据库文件，跳过IP地理位置解析。Path: {Path}", dbPath);
            return null;
        }

        try
        {
            return new Searcher(CachePolicy.Content, dbPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "初始化 IP2Region 查询器失败。Path: {Path}", dbPath);
            return null;
        }
    }

    /// <summary>
    /// 规范化IP
    /// </summary>
    /// <param name="ipAddress"></param>
    /// <returns></returns>
    private static string? NormalizeIp(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        var normalized = ipAddress.Trim();
        if (IPAddress.TryParse(normalized, out var parsedAddress))
        {
            if (parsedAddress.IsIPv4MappedToIPv6)
            {
                parsedAddress = parsedAddress.MapToIPv4();
            }

            return parsedAddress.ToString();
        }

        return normalized;
    }

    /// <summary>
    /// 是否内网/回环地址
    /// </summary>
    /// <param name="ipAddress"></param>
    /// <returns></returns>
    private static bool IsPrivateOrLoopbackIp(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                return IsPrivateOrLoopbackIp(address.MapToIPv4().ToString());
            }

            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               bytes[0] == 127 ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    /// <summary>
    /// 组合名称与版本
    /// </summary>
    /// <param name="name"></param>
    /// <param name="major"></param>
    /// <param name="minor"></param>
    /// <param name="patch"></param>
    /// <returns></returns>
    private static string? ComposeName(string? name, string? major, string? minor, string? patch)
    {
        name = NormalizeToNull(name);
        if (name is null || string.Equals(name, "Other", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var versions = new[] { major, minor, patch }
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return versions.Length == 0
            ? name
            : $"{name} {string.Join('.', versions)}";
    }

    /// <summary>
    /// 规范化设备名称
    /// </summary>
    /// <param name="deviceName"></param>
    /// <returns></returns>
    private static string? NormalizeDeviceName(string? deviceName)
    {
        deviceName = NormalizeToNull(deviceName);
        return string.Equals(deviceName, "Other", StringComparison.OrdinalIgnoreCase)
            ? null
            : deviceName;
    }

    /// <summary>
    /// 规范化位置字符串
    /// </summary>
    /// <param name="region"></param>
    /// <returns></returns>
    private static string? NormalizeRegion(string? region)
    {
        var tokens = region?
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(static token => !string.Equals(token, "0", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return tokens is { Length: > 0 } ? string.Join(' ', tokens) : null;
    }

    /// <summary>
    /// 空白转 null
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static string? NormalizeToNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
