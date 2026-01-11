#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IProxyPoolManager
// Guid:f1k3m5h7-gi8j-2k4l-hm0n-9i1j2k3l4m5g
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Models;

namespace XiHan.Framework.Http.Proxy;

/// <summary>
/// 代理池管理器接口
/// </summary>
public interface IProxyPoolManager
{
    /// <summary>
    /// 获取下一个可用的代理
    /// </summary>
    /// <returns></returns>
    ProxyConfiguration? GetNextProxy();

    /// <summary>
    /// 获取所有可用的代理
    /// </summary>
    /// <returns></returns>
    IEnumerable<ProxyConfiguration> GetAvailableProxies();

    /// <summary>
    /// 添加代理
    /// </summary>
    /// <param name="proxy">代理配置</param>
    /// <returns></returns>
    Task<bool> AddProxyAsync(ProxyConfiguration proxy);

    /// <summary>
    /// 移除代理
    /// </summary>
    /// <param name="proxyAddress">代理地址</param>
    /// <returns></returns>
    bool RemoveProxy(string proxyAddress);

    /// <summary>
    /// 获取代理统计信息
    /// </summary>
    /// <param name="proxyAddress">代理地址</param>
    /// <returns></returns>
    ProxyStatistics? GetProxyStatistics(string proxyAddress);

    /// <summary>
    /// 获取所有代理统计信息
    /// </summary>
    /// <returns></returns>
    IEnumerable<ProxyStatistics> GetAllStatistics();

    /// <summary>
    /// 记录代理使用结果
    /// </summary>
    /// <param name="proxyAddress">代理地址</param>
    /// <param name="success">是否成功</param>
    /// <param name="responseTime">响应时间</param>
    void RecordProxyResult(string proxyAddress, bool success, long responseTime);

    /// <summary>
    /// 标记代理为不可用
    /// </summary>
    /// <param name="proxyAddress">代理地址</param>
    void MarkProxyUnavailable(string proxyAddress);

    /// <summary>
    /// 标记代理为可用
    /// </summary>
    /// <param name="proxyAddress">代理地址</param>
    void MarkProxyAvailable(string proxyAddress);

    /// <summary>
    /// 启动健康检查
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task StartHealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止健康检查
    /// </summary>
    void StopHealthCheck();

    /// <summary>
    /// 获取代理池大小
    /// </summary>
    /// <returns></returns>
    int GetPoolSize();

    /// <summary>
    /// 获取可用代理数量
    /// </summary>
    /// <returns></returns>
    int GetAvailableCount();
}
