// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Diagnostics.RetryPolicys;

/// <summary>
/// 重试条件接口
/// </summary>
public interface IRetryCondition
{
    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>是否应该重试</returns>
    bool ShouldRetry(Exception exception, int attemptNumber);
}
