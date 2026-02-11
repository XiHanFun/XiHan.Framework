#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICircuitBreakerPolicy
// Guid:1e2f3a4b-5c6d-7e8f-9a0b-1c2d3e4f5a6b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Traffic.CircuitBreaker.Abstractions;

/// <summary>
/// 熔断策略接口
/// </summary>
public interface ICircuitBreakerPolicy
{
    /// <summary>
    /// 策略名称
    /// </summary>
    string PolicyName { get; }

    /// <summary>
    /// 判断熔断器是否开启
    /// </summary>
    /// <param name="key">熔断键</param>
    /// <returns>是否熔断</returns>
    bool IsOpen(string key);

    /// <summary>
    /// 记录成功调用
    /// </summary>
    /// <param name="key">熔断键</param>
    void RecordSuccess(string key);

    /// <summary>
    /// 记录失败调用
    /// </summary>
    /// <param name="key">熔断键</param>
    void RecordFailure(string key);
}
