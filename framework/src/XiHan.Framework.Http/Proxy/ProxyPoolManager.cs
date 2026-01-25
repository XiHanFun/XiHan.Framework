#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProxyPoolManager
// Guid:g2l4n6i8-hj9k-3l5m-in1o-0j2k3l4m5n6h
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Enums;
using XiHan.Framework.Http.Models;
using XiHan.Framework.Http.Options;

namespace XiHan.Framework.Http.Proxy;

/// <summary>
/// 代理池管理器实现
/// </summary>
public class ProxyPoolManager : IProxyPoolManager, IDisposable
{
    private readonly ILogger<ProxyPoolManager> _logger;
    private readonly IProxyValidator _proxyValidator;
    private readonly XiHanProxyPoolOptions _options;
    private readonly ConcurrentDictionary<string, ProxyConfiguration> _proxies;
    private readonly ConcurrentDictionary<string, ProxyStatistics> _statistics;
    private readonly Random _random;
    private readonly SemaphoreSlim _healthCheckLock;
    private int _currentIndex;
    private Timer? _healthCheckTimer;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="proxyValidator">代理验证器</param>
    /// <param name="options">代理池选项</param>
    public ProxyPoolManager(
        ILogger<ProxyPoolManager> logger,
        IProxyValidator proxyValidator,
        IOptions<XiHanProxyPoolOptions> options)
    {
        _logger = logger;
        _proxyValidator = proxyValidator;
        _options = options.Value;
        _proxies = new ConcurrentDictionary<string, ProxyConfiguration>();
        _statistics = new ConcurrentDictionary<string, ProxyStatistics>();
        _random = new Random();
        _currentIndex = 0;
        _healthCheckLock = new SemaphoreSlim(1, 1);

        // 初始化代理池
        InitializeProxyPool();
    }

    /// <summary>
    /// 获取下一个可用的代理
    /// </summary>
    /// <returns></returns>
    public ProxyConfiguration? GetNextProxy()
    {
        var availableProxies = GetAvailableProxiesInternal();

        if (availableProxies.Count == 0)
        {
            _logger.LogWarning("没有可用的代理");
            return null;
        }

        var selectedProxy = _options.SelectionStrategy switch
        {
            ProxySelectionStrategy.RoundRobin => SelectRoundRobin(availableProxies),
            ProxySelectionStrategy.Random => SelectRandom(availableProxies),
            ProxySelectionStrategy.LeastUsed => SelectLeastUsed(availableProxies),
            ProxySelectionStrategy.FastestResponse => SelectFastestResponse(availableProxies),
            ProxySelectionStrategy.Priority => SelectByPriority(availableProxies),
            _ => SelectRoundRobin(availableProxies)
        };

        if (selectedProxy != null)
        {
            var stats = _statistics[selectedProxy.GetProxyAddress()];
            stats.CurrentConnections++;
        }

        return selectedProxy;
    }

    /// <summary>
    /// 获取所有可用的代理
    /// </summary>
    /// <returns></returns>
    public IEnumerable<ProxyConfiguration> GetAvailableProxies()
    {
        return GetAvailableProxiesInternal();
    }

    /// <summary>
    /// 添加代理
    /// </summary>
    /// <param name="proxy">代理配置</param>
    /// <returns></returns>
    public async Task<bool> AddProxyAsync(ProxyConfiguration proxy)
    {
        if (!proxy.Validate())
        {
            _logger.LogWarning("无效的代理配置");
            return false;
        }

        var address = proxy.GetProxyAddress();

        // 验证代理
        if (_options.ValidateOnStartup)
        {
            var result = await _proxyValidator.ValidateAsync(proxy, _options.HealthCheckUrl, _options.HealthCheckTimeoutSeconds);
            if (!result.IsAvailable)
            {
                _logger.LogWarning("代理验证失败: {ProxyAddress}, 错误: {Error}",
                    address, result.ErrorMessage);
                return false;
            }
        }

        if (_proxies.TryAdd(address, proxy))
        {
            _statistics[address] = new ProxyStatistics
            {
                ProxyAddress = address,
                IsAvailable = true
            };

            _logger.LogInformation("成功添加代理: {ProxyAddress}", address);
            return true;
        }

        _logger.LogWarning("代理已存在: {ProxyAddress}", address);
        return false;
    }

    /// <summary>
    /// 移除代理
    /// </summary>
    /// <param name="proxyAddress">代理地址</param>
    /// <returns></returns>
    public bool RemoveProxy(string proxyAddress)
    {
        if (_proxies.TryRemove(proxyAddress, out _))
        {
            _statistics.TryRemove(proxyAddress, out _);
            _logger.LogInformation("移除代理: {ProxyAddress}", proxyAddress);
            return true;
        }

        _logger.LogWarning("代理不存在: {ProxyAddress}", proxyAddress);
        return false;
    }

    /// <summary>
    /// 获取代理统计信息
    /// </summary>
    /// <param name="proxyAddress">代理地址</param>
    /// <returns></returns>
    public ProxyStatistics? GetProxyStatistics(string proxyAddress)
    {
        return _statistics.TryGetValue(proxyAddress, out var stats) ? stats : null;
    }

    /// <summary>
    /// 获取所有代理统计信息
    /// </summary>
    /// <returns></returns>
    public IEnumerable<ProxyStatistics> GetAllStatistics()
    {
        return [.. _statistics.Values];
    }

    /// <summary>
    /// 记录代理使用结果
    /// </summary>
    /// <param name="proxyAddress">代理地址</param>
    /// <param name="success">是否成功</param>
    /// <param name="responseTime">响应时间</param>
    public void RecordProxyResult(string proxyAddress, bool success, long responseTime)
    {
        if (_statistics.TryGetValue(proxyAddress, out var stats))
        {
            stats.RecordRequest(success, responseTime);
            stats.CurrentConnections = Math.Max(0, stats.CurrentConnections - 1);

            // 检查是否需要标记为不可用
            if (!success && stats.ConsecutiveFailures >= _options.FailureThreshold)
            {
                MarkProxyUnavailable(proxyAddress);
            }

            _logger.LogDebug("记录代理结果: {ProxyAddress}, 成功: {Success}, 响应时间: {ResponseTime}ms",
                proxyAddress, success, responseTime);
        }
    }

    /// <summary>
    /// 标记代理为不可用
    /// </summary>
    /// <param name="proxyAddress">代理地址</param>
    public void MarkProxyUnavailable(string proxyAddress)
    {
        if (_statistics.TryGetValue(proxyAddress, out var stats))
        {
            stats.IsAvailable = false;
            _logger.LogWarning("标记代理为不可用: {ProxyAddress}, 连续失败次数: {ConsecutiveFailures}",
                proxyAddress, stats.ConsecutiveFailures);

            // 如果启用自动移除,则移除失败的代理
            if (_options.AutoRemoveFailedProxy)
            {
                RemoveProxy(proxyAddress);
            }
        }
    }

    /// <summary>
    /// 标记代理为可用
    /// </summary>
    /// <param name="proxyAddress">代理地址</param>
    public void MarkProxyAvailable(string proxyAddress)
    {
        if (_statistics.TryGetValue(proxyAddress, out var stats))
        {
            stats.IsAvailable = true;
            stats.ConsecutiveFailures = 0;
            _logger.LogInformation("标记代理为可用: {ProxyAddress}", proxyAddress);
        }
    }

    /// <summary>
    /// 启动健康检查
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task StartHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableHealthCheck)
        {
            _logger.LogInformation("健康检查未启用");
            return Task.CompletedTask;
        }

        _logger.LogInformation("启动代理健康检查,间隔: {Interval}秒", _options.HealthCheckIntervalSeconds);

        _healthCheckTimer = new Timer(
            async _ => await PerformHealthCheckAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds));

        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止健康检查
    /// </summary>
    public void StopHealthCheck()
    {
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;
        _logger.LogInformation("停止代理健康检查");
    }

    /// <summary>
    /// 获取代理池大小
    /// </summary>
    /// <returns></returns>
    public int GetPoolSize()
    {
        return _proxies.Count;
    }

    /// <summary>
    /// 获取可用代理数量
    /// </summary>
    /// <returns></returns>
    public int GetAvailableCount()
    {
        return GetAvailableProxiesInternal().Count;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        StopHealthCheck();
        _healthCheckLock?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 初始化代理池
    /// </summary>
    private void InitializeProxyPool()
    {
        if (!_options.Enabled || _options.Proxies.Count == 0)
        {
            _logger.LogInformation("代理池未启用或没有配置代理");
            return;
        }

        foreach (var proxy in _options.Proxies.Where(p => p.Enabled && p.Validate()))
        {
            var address = proxy.GetProxyAddress();
            if (_proxies.TryAdd(address, proxy))
            {
                _statistics[address] = new ProxyStatistics
                {
                    ProxyAddress = address,
                    IsAvailable = true
                };

                _logger.LogInformation("添加代理到池: {ProxyAddress}", address);
            }
        }

        _logger.LogInformation("代理池初始化完成,共 {Count} 个代理", _proxies.Count);

        // 如果启用了验证,在启动时验证所有代理
        if (_options.ValidateOnStartup && !_proxies.IsEmpty)
        {
            _ = Task.Run(ValidateAllProxiesAsync);
        }
    }

    #region 私有方法

    /// <summary>
    /// 按优先级选择
    /// </summary>
    /// <param name="proxies">代理列表</param>
    /// <returns></returns>
    private static ProxyConfiguration SelectByPriority(List<ProxyConfiguration> proxies)
    {
        return proxies.OrderBy(p => p.Priority).First();
    }

    /// <summary>
    /// 获取可用的代理列表(内部方法)
    /// </summary>
    /// <returns></returns>
    private List<ProxyConfiguration> GetAvailableProxiesInternal()
    {
        var now = DateTime.UtcNow;
        var availableProxies = new List<ProxyConfiguration>();

        foreach (var kvp in _proxies)
        {
            var proxy = kvp.Value;
            var address = kvp.Key;

            if (!_statistics.TryGetValue(address, out var stats))
            {
                continue;
            }

            // 检查是否可用
            if (stats.IsAvailable)
            {
                availableProxies.Add(proxy);
                continue;
            }

            // 检查是否可以恢复
            if (stats.LastFailureAt.HasValue &&
                (now - stats.LastFailureAt.Value).TotalSeconds >= _options.RecoveryTimeSeconds)
            {
                _logger.LogInformation("尝试恢复代理: {ProxyAddress}", address);
                stats.IsAvailable = true;
                availableProxies.Add(proxy);
            }
        }

        return availableProxies;
    }

    /// <summary>
    /// 轮询选择
    /// </summary>
    /// <param name="proxies">代理列表</param>
    /// <returns></returns>
    private ProxyConfiguration SelectRoundRobin(List<ProxyConfiguration> proxies)
    {
        var index = Interlocked.Increment(ref _currentIndex) % proxies.Count;
        return proxies[index];
    }

    /// <summary>
    /// 随机选择
    /// </summary>
    /// <param name="proxies">代理列表</param>
    /// <returns></returns>
    private ProxyConfiguration SelectRandom(List<ProxyConfiguration> proxies)
    {
        var index = _random.Next(proxies.Count);
        return proxies[index];
    }

    /// <summary>
    /// 选择使用次数最少的代理
    /// </summary>
    /// <param name="proxies">代理列表</param>
    /// <returns></returns>
    private ProxyConfiguration SelectLeastUsed(List<ProxyConfiguration> proxies)
    {
        ProxyConfiguration? selectedProxy = null;
        var minRequests = long.MaxValue;

        foreach (var proxy in proxies)
        {
            var address = proxy.GetProxyAddress();
            if (_statistics.TryGetValue(address, out var stats))
            {
                if (stats.TotalRequests < minRequests)
                {
                    minRequests = stats.TotalRequests;
                    selectedProxy = proxy;
                }
            }
        }

        return selectedProxy ?? proxies[0];
    }

    /// <summary>
    /// 选择响应最快的代理
    /// </summary>
    /// <param name="proxies">代理列表</param>
    /// <returns></returns>
    private ProxyConfiguration SelectFastestResponse(List<ProxyConfiguration> proxies)
    {
        ProxyConfiguration? selectedProxy = null;
        var minResponseTime = double.MaxValue;

        foreach (var proxy in proxies)
        {
            var address = proxy.GetProxyAddress();
            if (_statistics.TryGetValue(address, out var stats) && stats.SuccessCount > 0)
            {
                if (stats.AverageResponseTime < minResponseTime)
                {
                    minResponseTime = stats.AverageResponseTime;
                    selectedProxy = proxy;
                }
            }
        }

        return selectedProxy ?? proxies[0];
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    /// <returns></returns>
    private async Task PerformHealthCheckAsync()
    {
        // 使用锁防止并发执行
        if (!await _healthCheckLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            _logger.LogDebug("开始执行代理健康检查");

            var results = await _proxyValidator.ValidateBatchAsync(
                _proxies.Values,
                _options.HealthCheckUrl,
                _options.HealthCheckTimeoutSeconds,
                10);

            foreach (var result in results)
            {
                var address = result.Proxy.GetProxyAddress();
                if (_statistics.TryGetValue(address, out var stats))
                {
                    stats.RecordValidation(result.IsAvailable);

                    if (!result.IsAvailable && stats.ConsecutiveFailures >= _options.FailureThreshold)
                    {
                        MarkProxyUnavailable(address);
                    }
                }
            }

            _logger.LogDebug("代理健康检查完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "代理健康检查出错");
        }
        finally
        {
            _healthCheckLock.Release();
        }
    }

    /// <summary>
    /// 验证所有代理
    /// </summary>
    /// <returns></returns>
    private async Task ValidateAllProxiesAsync()
    {
        try
        {
            _logger.LogInformation("开始验证所有代理");

            var results = await _proxyValidator.ValidateBatchAsync(
                _proxies.Values,
                _options.HealthCheckUrl,
                _options.HealthCheckTimeoutSeconds,
                10);

            foreach (var result in results)
            {
                var address = result.Proxy.GetProxyAddress();
                if (_statistics.TryGetValue(address, out var stats))
                {
                    stats.RecordValidation(result.IsAvailable);
                }
            }

            var availableCount = results.Count(r => r.IsAvailable);
            _logger.LogInformation("代理验证完成,可用: {AvailableCount}/{TotalCount}",
                availableCount, _proxies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证代理时出错");
        }
    }

    #endregion
}
