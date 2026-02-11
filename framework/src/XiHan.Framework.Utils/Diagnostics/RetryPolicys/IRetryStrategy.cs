#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRetryStrategy
// Guid:8f7802bc-ba45-44a1-b5f2-e3797558023b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/05 07:32:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
