// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
