// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using UAParser;

namespace XiHan.Framework.Web.Core.Clients;

/// <summary>
/// 基于 HttpContext 的客户端信息提供器
/// </summary>
/// <remarks>
/// 客户端 IP 只取 <c>Connection.RemoteIpAddress</c>（部署在反向代理后面时由转发头中间件按受信代理还原），不读转发类请求头。
/// 归属地交给 <see cref="IIpRegionResolver"/>（与访问策略等共用一份离线库）；回环地址记「本机」，内网地址记「局域网」。
/// </remarks>
public sealed class HttpContextClientInfoProvider : IClientInfoProvider
{
    private static readonly Parser UserAgentParser = Parser.GetDefault();
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IIpRegionResolver _ipRegionResolver;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpContextAccessor">HttpContext 访问器</param>
    /// <param name="ipRegionResolver">IP 归属地解析</param>
    public HttpContextClientInfoProvider(IHttpContextAccessor httpContextAccessor, IIpRegionResolver ipRegionResolver)
    {
        _httpContextAccessor = httpContextAccessor;
        _ipRegionResolver = ipRegionResolver;
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
    /// 解析客户端IP：转发头中间件按受信代理还原后的连接地址，IPv4 映射地址还原成 IPv4
    /// </summary>
    /// <param name="httpContext"></param>
    /// <returns></returns>
    private static string? ResolveClientIp(HttpContext? httpContext)
    {
        var address = httpContext?.Connection.RemoteIpAddress;
        if (address is null)
        {
            return null;
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
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
        if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out var address))
        {
            return null;
        }

        if (IpAddressScope.IsLoopback(address))
        {
            return "本机";
        }

        if (IpAddressScope.IsPrivate(address))
        {
            return "局域网";
        }

        return _ipRegionResolver.Resolve(address)?.Location;
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
    /// 规范化设备名称，桌面浏览器 UA 的 Device.Family 通常为 "Other"，回退为 "PC"
    /// </summary>
    /// <param name="deviceName"></param>
    /// <returns></returns>
    private static string? NormalizeDeviceName(string? deviceName)
    {
        deviceName = NormalizeToNull(deviceName);
        return string.Equals(deviceName, "Other", StringComparison.OrdinalIgnoreCase)
            ? "PC"
            : deviceName;
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
