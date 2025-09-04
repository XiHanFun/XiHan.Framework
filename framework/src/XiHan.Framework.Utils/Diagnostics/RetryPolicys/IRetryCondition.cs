#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRetryCondition
// Guid:bfd0dfde-e8cd-4f97-a6d7-9e6044adc0a3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/5 7:30:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
