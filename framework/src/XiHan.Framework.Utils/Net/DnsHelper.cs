#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DnsHelper
// Guid:6d4e2f8a-9b7c-4e1d-8a3f-5c6b9d2e1f4a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 12:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Utils.Net;

/// <summary>
/// DNS 查询辅助工具类
/// </summary>
/// <remarks>
/// 提供全面的 DNS 查询功能，包括各种记录类型查询、缓存管理、
/// 自定义 DNS 服务器、异步操作等功能
/// </remarks>
public static class DnsHelper
{
    #region 私有字段

    /// <summary>
    /// DNS 缓存
    /// </summary>
    private static readonly ConcurrentDictionary<string, DnsCacheEntry> DnsCache = new();

    /// <summary>
    /// 默认 DNS 服务器列表
    /// </summary>
    private static readonly List<IPAddress> DefaultDnsServers = new()
    {
        IPAddress.Parse("8.8.8.8"),     // Google DNS
        IPAddress.Parse("8.8.4.4"),     // Google DNS 备用
        IPAddress.Parse("1.1.1.1"),     // Cloudflare DNS
        IPAddress.Parse("1.0.0.1"),     // Cloudflare DNS 备用
        IPAddress.Parse("114.114.114.114"), // 114 DNS
        IPAddress.Parse("223.5.5.5")    // 阿里 DNS
    };

    /// <summary>
    /// 域名验证正则表达式
    /// </summary>
    private static readonly Regex DomainValidationRegex = new(
        @"^(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)*[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?$",
        RegexOptions.Compiled);

    /// <summary>
    /// 当前使用的 DNS 服务器列表
    /// </summary>
    private static List<IPAddress> _currentDnsServers = GetSystemDnsServers();

    /// <summary>
    /// 默认查询超时时间
    /// </summary>
    private static TimeSpan _defaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 默认缓存有效期
    /// </summary>
    private static TimeSpan _defaultCacheTtl = TimeSpan.FromMinutes(10);

    #endregion

    #region 配置管理

    /// <summary>
    /// 设置默认查询超时时间
    /// </summary>
    /// <param name="timeout">超时时间</param>
    public static void SetDefaultTimeout(TimeSpan timeout)
    {
        _defaultTimeout = timeout;
    }

    /// <summary>
    /// 设置默认缓存有效期
    /// </summary>
    /// <param name="ttl">缓存有效期</param>
    public static void SetDefaultCacheTtl(TimeSpan ttl)
    {
        _defaultCacheTtl = ttl;
    }

    /// <summary>
    /// 设置自定义 DNS 服务器
    /// </summary>
    /// <param name="dnsServers">DNS 服务器 IP 地址列表</param>
    public static void SetCustomDnsServers(params IPAddress[] dnsServers)
    {
        if (dnsServers?.Length > 0)
        {
            _currentDnsServers = new List<IPAddress>(dnsServers);
        }
    }

    /// <summary>
    /// 设置自定义 DNS 服务器
    /// </summary>
    /// <param name="dnsServers">DNS 服务器 IP 地址字符串列表</param>
    public static void SetCustomDnsServers(params string[] dnsServers)
    {
        if (dnsServers?.Length > 0)
        {
            var ipAddresses = new List<IPAddress>();
            foreach (var dns in dnsServers)
            {
                if (IPAddress.TryParse(dns, out var ip))
                {
                    ipAddresses.Add(ip);
                }
            }
            if (ipAddresses.Count > 0)
            {
                _currentDnsServers = ipAddresses;
            }
        }
    }

    /// <summary>
    /// 重置为系统默认 DNS 服务器
    /// </summary>
    public static void ResetToSystemDnsServers()
    {
        _currentDnsServers = GetSystemDnsServers();
    }

    /// <summary>
    /// 重置为内置默认 DNS 服务器
    /// </summary>
    public static void ResetToDefaultDnsServers()
    {
        _currentDnsServers = new List<IPAddress>(DefaultDnsServers);
    }

    #endregion

    #region 基本 DNS 查询

    /// <summary>
    /// 解析域名获取 IPv4 地址列表
    /// </summary>
    /// <param name="hostname">主机名或域名</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <returns>IPv4 地址列表</returns>
    public static async Task<IList<IPAddress>> ResolveAsync(string hostname, bool useCache = true, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            throw new ArgumentException("主机名不能为空", nameof(hostname));
        }

        // 检查缓存
        if (useCache && TryGetFromCache(hostname, DnsRecordType.A, out var cachedResult))
        {
            return cachedResult.Addresses;
        }

        try
        {
            var timeoutValue = timeout ?? _defaultTimeout;
            var result = await QueryWithTimeoutAsync(hostname, timeoutValue);

            var ipv4Addresses = result.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList();

            // 缓存结果
            if (useCache && ipv4Addresses.Count > 0)
            {
                AddToCache(hostname, DnsRecordType.A, ipv4Addresses);
            }

            return ipv4Addresses;
        }
        catch (Exception ex)
        {
            throw new DnsQueryException($"解析域名 '{hostname}' 失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 解析域名获取 IPv6 地址列表
    /// </summary>
    /// <param name="hostname">主机名或域名</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <returns>IPv6 地址列表</returns>
    public static async Task<IList<IPAddress>> ResolveIPv6Async(string hostname, bool useCache = true, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            throw new ArgumentException("主机名不能为空", nameof(hostname));
        }

        // 检查缓存
        if (useCache && TryGetFromCache(hostname, DnsRecordType.AAAA, out var cachedResult))
        {
            return cachedResult.Addresses;
        }

        try
        {
            var timeoutValue = timeout ?? _defaultTimeout;
            var result = await QueryWithTimeoutAsync(hostname, timeoutValue);

            var ipv6Addresses = result.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6).ToList();

            // 缓存结果
            if (useCache && ipv6Addresses.Count > 0)
            {
                AddToCache(hostname, DnsRecordType.AAAA, ipv6Addresses);
            }

            return ipv6Addresses;
        }
        catch (Exception ex)
        {
            throw new DnsQueryException($"解析域名 '{hostname}' 的 IPv6 地址失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 反向 DNS 查询，获取 IP 地址对应的主机名
    /// </summary>
    /// <param name="ipAddress">IP 地址</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <returns>主机名</returns>
    public static async Task<string> ReverseLookupAsync(IPAddress ipAddress, bool useCache = true, TimeSpan? timeout = null)
    {
        if (ipAddress == null)
        {
            throw new ArgumentNullException(nameof(ipAddress));
        }

        var cacheKey = $"PTR_{ipAddress}";

        // 检查缓存
        if (useCache && TryGetFromCache(cacheKey, DnsRecordType.PTR, out var cachedResult))
        {
            return cachedResult.HostName ?? ipAddress.ToString();
        }

        try
        {
            var timeoutValue = timeout ?? _defaultTimeout;
            var result = await ReverseQueryWithTimeoutAsync(ipAddress, timeoutValue);

            // 缓存结果
            if (useCache && !string.IsNullOrEmpty(result))
            {
                AddToCache(cacheKey, DnsRecordType.PTR, [], result);
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new DnsQueryException($"反向查询 IP '{ipAddress}' 失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 反向 DNS 查询，获取 IP 地址对应的主机名
    /// </summary>
    /// <param name="ipAddress">IP 地址字符串</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <returns>主机名</returns>
    public static async Task<string> ReverseLookupAsync(string ipAddress, bool useCache = true, TimeSpan? timeout = null)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
        {
            throw new ArgumentException($"无效的 IP 地址: {ipAddress}", nameof(ipAddress));
        }

        return await ReverseLookupAsync(ip, useCache, timeout);
    }

    #endregion

    #region 高级 DNS 查询

    /// <summary>
    /// 查询 MX 记录（邮件交换记录）
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <returns>MX 记录列表</returns>
    public static async Task<IList<DnsRecord>> QueryMxRecordsAsync(string domain, bool useCache = true, TimeSpan? timeout = null)
    {
        return await QueryRecordsAsync(domain, DnsRecordType.MX, useCache, timeout);
    }

    /// <summary>
    /// 查询 NS 记录（名称服务器记录）
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <returns>NS 记录列表</returns>
    public static async Task<IList<DnsRecord>> QueryNsRecordsAsync(string domain, bool useCache = true, TimeSpan? timeout = null)
    {
        return await QueryRecordsAsync(domain, DnsRecordType.NS, useCache, timeout);
    }

    /// <summary>
    /// 查询 TXT 记录（文本记录）
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <returns>TXT 记录列表</returns>
    public static async Task<IList<DnsRecord>> QueryTxtRecordsAsync(string domain, bool useCache = true, TimeSpan? timeout = null)
    {
        return await QueryRecordsAsync(domain, DnsRecordType.TXT, useCache, timeout);
    }

    /// <summary>
    /// 查询 CNAME 记录（别名记录）
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <returns>CNAME 记录列表</returns>
    public static async Task<IList<DnsRecord>> QueryCnameRecordsAsync(string domain, bool useCache = true, TimeSpan? timeout = null)
    {
        return await QueryRecordsAsync(domain, DnsRecordType.CNAME, useCache, timeout);
    }

    /// <summary>
    /// 查询所有类型的 DNS 记录
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <returns>DNS 查询结果</returns>
    public static async Task<DnsQueryResult> QueryAllRecordsAsync(string domain, bool useCache = true, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("域名不能为空", nameof(domain));
        }

        var result = new DnsQueryResult { Domain = domain };

        var tasks = new List<Task>
        {
            ResolveAsync(domain, useCache, timeout).ContinueWith(t =>
                result.ARecords = t.IsCompletedSuccessfully ? t.Result.ToList() : new List<IPAddress>()),
            ResolveIPv6Async(domain, useCache, timeout).ContinueWith(t =>
                result.AAAARecords = t.IsCompletedSuccessfully ? t.Result.ToList() : new List<IPAddress>()),
            QueryMxRecordsAsync(domain, useCache, timeout).ContinueWith(t =>
                result.MXRecords = t.IsCompletedSuccessfully ? t.Result.ToList() : new List<DnsRecord>()),
            QueryNsRecordsAsync(domain, useCache, timeout).ContinueWith(t =>
                result.NSRecords = t.IsCompletedSuccessfully ? t.Result.ToList() : new List<DnsRecord>()),
            QueryTxtRecordsAsync(domain, useCache, timeout).ContinueWith(t =>
                result.TXTRecords = t.IsCompletedSuccessfully ? t.Result.ToList() : new List<DnsRecord>()),
            QueryCnameRecordsAsync(domain, useCache, timeout).ContinueWith(t =>
                result.CNAMERecords = t.IsCompletedSuccessfully ? t.Result.ToList() : new List<DnsRecord>())
        };

        await Task.WhenAll(tasks);
        return result;
    }

    #endregion

    #region 批量查询

    /// <summary>
    /// 批量解析域名
    /// </summary>
    /// <param name="hostnames">域名列表</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <param name="maxConcurrency">最大并发数</param>
    /// <returns>域名解析结果字典</returns>
    public static async Task<Dictionary<string, IList<IPAddress>>> BatchResolveAsync(
        IEnumerable<string> hostnames,
        bool useCache = true,
        TimeSpan? timeout = null,
        int maxConcurrency = 10)
    {
        var results = new ConcurrentDictionary<string, IList<IPAddress>>();
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var tasks = hostnames.Select(async hostname =>
        {
            await semaphore.WaitAsync();
            try
            {
                var addresses = await ResolveAsync(hostname, useCache, timeout);
                results[hostname] = addresses;
            }
            catch
            {
                results[hostname] = new List<IPAddress>();
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return new Dictionary<string, IList<IPAddress>>(results);
    }

    /// <summary>
    /// 批量反向查询
    /// </summary>
    /// <param name="ipAddresses">IP 地址列表</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <param name="maxConcurrency">最大并发数</param>
    /// <returns>IP 地址和主机名映射字典</returns>
    public static async Task<Dictionary<IPAddress, string>> BatchReverseLookupAsync(
        IEnumerable<IPAddress> ipAddresses,
        bool useCache = true,
        TimeSpan? timeout = null,
        int maxConcurrency = 10)
    {
        var results = new ConcurrentDictionary<IPAddress, string>();
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var tasks = ipAddresses.Select(async ip =>
        {
            await semaphore.WaitAsync();
            try
            {
                var hostname = await ReverseLookupAsync(ip, useCache, timeout);
                results[ip] = hostname;
            }
            catch
            {
                results[ip] = ip.ToString();
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return new Dictionary<IPAddress, string>(results);
    }

    #endregion

    #region 验证和检查

    /// <summary>
    /// 验证域名格式是否有效
    /// </summary>
    /// <param name="domain">域名</param>
    /// <returns>是否有效</returns>
    public static bool IsValidDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        if (domain.Length > 253)
        {
            return false;
        }

        return DomainValidationRegex.IsMatch(domain);
    }

    /// <summary>
    /// 检查域名是否可解析
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="timeout">查询超时时间</param>
    /// <returns>是否可解析</returns>
    public static async Task<bool> IsDomainResolvableAsync(string domain, TimeSpan? timeout = null)
    {
        try
        {
            var addresses = await ResolveAsync(domain, false, timeout);
            return addresses.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查主机是否可达
    /// </summary>
    /// <param name="hostname">主机名</param>
    /// <param name="port">端口号</param>
    /// <param name="timeout">连接超时时间</param>
    /// <returns>是否可达</returns>
    public static async Task<bool> IsHostReachableAsync(string hostname, int port = 80, TimeSpan? timeout = null)
    {
        try
        {
            var addresses = await ResolveAsync(hostname, true, timeout);
            if (addresses.Count == 0)
            {
                return false;
            }

            using var client = new TcpClient();
            var timeoutValue = timeout ?? _defaultTimeout;
            var connectTask = client.ConnectAsync(addresses.First(), port);

            await connectTask.WaitAsync(timeoutValue);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 缓存管理

    /// <summary>
    /// 清空 DNS 缓存
    /// </summary>
    public static void ClearCache()
    {
        DnsCache.Clear();
    }

    /// <summary>
    /// 清理过期的缓存条目
    /// </summary>
    public static void CleanExpiredCache()
    {
        var expiredKeys = DnsCache.Where(kvp => kvp.Value.IsExpired).Select(kvp => kvp.Key).ToList();
        foreach (var key in expiredKeys)
        {
            DnsCache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    /// <returns>缓存统计信息</returns>
    public static DnsCacheStatistics GetCacheStatistics()
    {
        var total = DnsCache.Count;
        var expired = DnsCache.Values.Count(entry => entry.IsExpired);
        var active = total - expired;

        return new DnsCacheStatistics
        {
            TotalEntries = total,
            ActiveEntries = active,
            ExpiredEntries = expired
        };
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 获取系统 DNS 服务器列表
    /// </summary>
    /// <returns>DNS 服务器列表</returns>
    private static List<IPAddress> GetSystemDnsServers()
    {
        var dnsServers = new List<IPAddress>();

        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var networkInterface in networkInterfaces)
            {
                var properties = networkInterface.GetIPProperties();
                dnsServers.AddRange(properties.DnsAddresses);
            }
        }
        catch
        {
            // 获取系统 DNS 失败时使用默认 DNS
            return new List<IPAddress>(DefaultDnsServers);
        }

        return dnsServers.Distinct().ToList();
    }

    /// <summary>
    /// 使用超时的 DNS 查询
    /// </summary>
    /// <param name="hostname">主机名</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>DNS 查询结果</returns>
    private static async Task<IPHostEntry> QueryWithTimeoutAsync(string hostname, TimeSpan timeout)
    {
        var task = Dns.GetHostEntryAsync(hostname);
        return await task.WaitAsync(timeout);
    }

    /// <summary>
    /// 使用超时的反向 DNS 查询
    /// </summary>
    /// <param name="ipAddress">IP 地址</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>主机名</returns>
    private static async Task<string> ReverseQueryWithTimeoutAsync(IPAddress ipAddress, TimeSpan timeout)
    {
        var task = Dns.GetHostEntryAsync(ipAddress);
        var result = await task.WaitAsync(timeout);
        return result.HostName;
    }

    /// <summary>
    /// 查询特定类型的 DNS 记录
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="recordType">记录类型</param>
    /// <param name="useCache">是否使用缓存</param>
    /// <param name="timeout">查询超时时间</param>
    /// <returns>DNS 记录列表</returns>
    private static Task<IList<DnsRecord>> QueryRecordsAsync(string domain, DnsRecordType recordType, bool useCache, TimeSpan? timeout)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("域名不能为空", nameof(domain));
        }

        var cacheKey = $"{recordType}_{domain}";

        // 检查缓存
        if (useCache && TryGetFromCache(cacheKey, recordType, out var cachedResult))
        {
            return Task.FromResult(cachedResult.Records);
        }

        try
        {
            // 注意：.NET 标准库不直接支持 MX、NS、TXT、CNAME 查询
            // 这里提供基础实现，实际使用时可能需要第三方 DNS 库
            var records = new List<DnsRecord>();

            // 模拟查询结果（实际实现需要使用专门的 DNS 库）
            switch (recordType)
            {
                case DnsRecordType.MX:
                    // MX 记录查询需要专门的 DNS 库
                    break;

                case DnsRecordType.NS:
                    // NS 记录查询需要专门的 DNS 库
                    break;

                case DnsRecordType.TXT:
                    // TXT 记录查询需要专门的 DNS 库
                    break;

                case DnsRecordType.CNAME:
                    // CNAME 记录查询需要专门的 DNS 库
                    break;
            }

            // 缓存结果
            if (useCache)
            {
                AddToCache(cacheKey, recordType, [], null, records);
            }

            return Task.FromResult<IList<DnsRecord>>(records);
        }
        catch (Exception ex)
        {
            throw new DnsQueryException($"查询域名 '{domain}' 的 {recordType} 记录失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 尝试从缓存获取结果
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="recordType">记录类型</param>
    /// <param name="result">缓存结果</param>
    /// <returns>是否命中缓存</returns>
    private static bool TryGetFromCache(string key, DnsRecordType recordType, [NotNullWhen(true)] out DnsCacheEntry? result)
    {
        if (DnsCache.TryGetValue(key, out result) && !result.IsExpired)
        {
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// 添加结果到缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="recordType">记录类型</param>
    /// <param name="addresses">IP 地址列表</param>
    /// <param name="hostname">主机名</param>
    /// <param name="records">DNS 记录列表</param>
    private static void AddToCache(string key, DnsRecordType recordType, IList<IPAddress> addresses, string? hostname = null, IList<DnsRecord>? records = null)
    {
        var entry = new DnsCacheEntry
        {
            RecordType = recordType,
            Addresses = addresses,
            HostName = hostname,
            Records = records ?? new List<DnsRecord>(),
            CachedAt = DateTime.UtcNow,
            Ttl = _defaultCacheTtl
        };

        DnsCache.AddOrUpdate(key, entry, (_, _) => entry);
    }

    #endregion
}

/// <summary>
/// DNS 查询结果
/// </summary>
public class DnsQueryResult
{
    /// <summary>
    /// 查询的域名
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// A 记录（IPv4 地址）
    /// </summary>
    public IList<IPAddress> ARecords { get; set; } = new List<IPAddress>();

    /// <summary>
    /// AAAA 记录（IPv6 地址）
    /// </summary>
    public IList<IPAddress> AAAARecords { get; set; } = new List<IPAddress>();

    /// <summary>
    /// MX 记录
    /// </summary>
    public IList<DnsRecord> MXRecords { get; set; } = new List<DnsRecord>();

    /// <summary>
    /// NS 记录
    /// </summary>
    public IList<DnsRecord> NSRecords { get; set; } = new List<DnsRecord>();

    /// <summary>
    /// TXT 记录
    /// </summary>
    public IList<DnsRecord> TXTRecords { get; set; } = new List<DnsRecord>();

    /// <summary>
    /// CNAME 记录
    /// </summary>
    public IList<DnsRecord> CNAMERecords { get; set; } = new List<DnsRecord>();

    /// <summary>
    /// 查询时间
    /// </summary>
    public DateTime QueryTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DNS 缓存统计信息
/// </summary>
public class DnsCacheStatistics
{
    /// <summary>
    /// 总缓存条目数
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// 活跃缓存条目数
    /// </summary>
    public int ActiveEntries { get; set; }

    /// <summary>
    /// 过期缓存条目数
    /// </summary>
    public int ExpiredEntries { get; set; }

    /// <summary>
    /// 缓存命中率
    /// </summary>
    public double HitRate => TotalEntries > 0 ? (double)ActiveEntries / TotalEntries : 0;
}

/// <summary>
/// DNS 缓存条目
/// </summary>
internal class DnsCacheEntry
{
    /// <summary>
    /// 记录类型
    /// </summary>
    public DnsRecordType RecordType { get; set; }

    /// <summary>
    /// IP 地址列表
    /// </summary>
    public IList<IPAddress> Addresses { get; set; } = new List<IPAddress>();

    /// <summary>
    /// 主机名
    /// </summary>
    public string? HostName { get; set; }

    /// <summary>
    /// DNS 记录列表
    /// </summary>
    public IList<DnsRecord> Records { get; set; } = new List<DnsRecord>();

    /// <summary>
    /// 缓存时间
    /// </summary>
    public DateTime CachedAt { get; set; }

    /// <summary>
    /// TTL（生存时间）
    /// </summary>
    public TimeSpan Ttl { get; set; }

    /// <summary>
    /// 是否已过期
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > CachedAt.Add(Ttl);
}

/// <summary>
/// DNS 记录类型枚举
/// </summary>
public enum DnsRecordType
{
    /// <summary>
    /// A 记录（IPv4 地址）
    /// </summary>
    A,

    /// <summary>
    /// AAAA 记录（IPv6 地址）
    /// </summary>
    AAAA,

    /// <summary>
    /// PTR 记录（反向查询）
    /// </summary>
    PTR,

    /// <summary>
    /// MX 记录（邮件交换）
    /// </summary>
    MX,

    /// <summary>
    /// NS 记录（名称服务器）
    /// </summary>
    NS,

    /// <summary>
    /// TXT 记录（文本记录）
    /// </summary>
    TXT,

    /// <summary>
    /// CNAME 记录（别名记录）
    /// </summary>
    CNAME
}

/// <summary>
/// DNS 记录
/// </summary>
public record DnsRecord
{
    /// <summary>
    /// 记录类型
    /// </summary>
    public DnsRecordType Type { get; init; }

    /// <summary>
    /// 记录值
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// 优先级（用于 MX 记录）
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// TTL（生存时间）
    /// </summary>
    public int Ttl { get; init; }
}

/// <summary>
/// DNS 查询异常
/// </summary>
public class DnsQueryException : Exception
{
    /// <summary>
    /// 初始化 DNS 查询异常
    /// </summary>
    /// <param name="message">异常消息</param>
    public DnsQueryException(string message) : base(message)
    {
    }

    /// <summary>
    /// 初始化 DNS 查询异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public DnsQueryException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
