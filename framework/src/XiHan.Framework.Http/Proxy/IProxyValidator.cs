#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IProxyValidator
// Guid:c8h0j2e4-df5g-9h1i-ej7k-6f8g9h0i1j2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Models;

namespace XiHan.Framework.Http.Proxy;

/// <summary>
/// 代理验证器接口
/// </summary>
public interface IProxyValidator
{
    /// <summary>
    /// 验证代理是否可用
    /// </summary>
    /// <param name="proxy">代理配置</param>
    /// <param name="testUrl">测试URL</param>
    /// <param name="timeoutSeconds">超时时间(秒)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<ProxyValidationResult> ValidateAsync(ProxyConfiguration proxy, string testUrl, int timeoutSeconds = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量验证代理
    /// </summary>
    /// <param name="proxies">代理配置列表</param>
    /// <param name="testUrl">测试URL</param>
    /// <param name="timeoutSeconds">超时时间(秒)</param>
    /// <param name="maxConcurrency">最大并发数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<IEnumerable<ProxyValidationResult>> ValidateBatchAsync(IEnumerable<ProxyConfiguration> proxies, string testUrl, int timeoutSeconds = 10, int maxConcurrency = 10, CancellationToken cancellationToken = default);
}
