#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProxyPoolHealthCheckService
// Guid:h3m5o7j9-ik0l-4m6n-jo2p-1k3l4m5n6o7i
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Http.Proxy;

/// <summary>
/// 代理池健康检查后台服务
/// </summary>
public class ProxyPoolHealthCheckService : BackgroundService
{
    private readonly IProxyPoolManager _proxyPoolManager;
    private readonly ILogger<ProxyPoolHealthCheckService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="proxyPoolManager">代理池管理器</param>
    /// <param name="logger">日志记录器</param>
    public ProxyPoolHealthCheckService(
        IProxyPoolManager proxyPoolManager,
        ILogger<ProxyPoolHealthCheckService> logger)
    {
        _proxyPoolManager = proxyPoolManager;
        _logger = logger;
    }

    /// <summary>
    /// 执行异步任务
    /// </summary>
    /// <param name="stoppingToken">停止令牌</param>
    /// <returns></returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("代理池健康检查服务启动");

        try
        {
            await _proxyPoolManager.StartHealthCheckAsync(stoppingToken);

            // 保持服务运行直到取消
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("代理池健康检查服务停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "代理池健康检查服务异常");
        }
    }

    /// <summary>
    /// 停止异步任务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止代理池健康检查服务");
        _proxyPoolManager.StopHealthCheck();
        return base.StopAsync(cancellationToken);
    }
}

