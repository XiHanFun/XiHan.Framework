// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Diagnostics.RetryPolicys;

/// <summary>
/// 重试策略接口
/// </summary>
public interface IRetryStrategy
{
    /// <summary>
    /// 获取指定重试次数的延迟时间
    /// </summary>
    /// <param name="retryCount">重试次数</param>
    /// <returns>延迟时间</returns>
    TimeSpan GetDelay(int retryCount);
}
