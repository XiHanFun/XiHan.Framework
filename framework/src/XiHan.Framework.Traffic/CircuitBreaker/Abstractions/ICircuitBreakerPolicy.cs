// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
