#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRateLimitPolicy
// Guid:0d1e2f3a-4b5c-6d7e-8f9a-0b1c2d3e4f5a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/1/22 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Traffic.RateLimiting.Abstractions;

/// <summary>
/// 限流策略接口
/// </summary>
public interface IRateLimitPolicy
{
    /// <summary>
    /// 策略名称
    /// </summary>
    string PolicyName { get; }

    /// <summary>
    /// 是否允许请求通过
    /// </summary>
    /// <param name="key">限流键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否允许</returns>
    Task<bool> IsAllowedAsync(string key, CancellationToken cancellationToken = default);
}
