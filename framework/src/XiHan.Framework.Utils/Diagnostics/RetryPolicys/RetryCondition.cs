#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RetryCondition
// Guid:4ff1e8d7-a7ee-4c44-be81-ae228fa27178
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/5 7:30:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Diagnostics.RetryPolicys;

/// <summary>
/// 默认重试条件（对所有异常都重试）
/// </summary>
public class DefaultRetryCondition : IRetryCondition
{
    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>总是返回true</returns>
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return true;
    }
}

/// <summary>
/// 基于异常类型的重试条件
/// </summary>
/// <typeparam name="TException">要重试的异常类型</typeparam>
public class ExceptionTypeRetryCondition<TException> : IRetryCondition
    where TException : Exception
{
    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>异常是指定类型时返回true</returns>
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return exception is TException;
    }
}

/// <summary>
/// 自定义重试条件
/// </summary>
public class CustomRetryCondition : IRetryCondition
{
    private readonly Func<Exception, int, bool> _condition;

    /// <summary>
    /// 初始化自定义重试条件
    /// </summary>
    /// <param name="condition">重试条件函数</param>
    public CustomRetryCondition(Func<Exception, int, bool> condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>自定义条件的判断结果</returns>
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return _condition(exception, attemptNumber);
    }
}

/// <summary>
/// 组合重试条件（AND 逻辑）
/// </summary>
public class AndRetryCondition : IRetryCondition
{
    private readonly IRetryCondition[] _conditions;

    /// <summary>
    /// 初始化组合重试条件
    /// </summary>
    /// <param name="conditions">要组合的条件</param>
    public AndRetryCondition(params IRetryCondition[] conditions)
    {
        _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
    }

    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>所有条件都满足时返回true</returns>
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return _conditions.All(condition => condition.ShouldRetry(exception, attemptNumber));
    }
}

/// <summary>
/// 组合重试条件（OR 逻辑）
/// </summary>
public class OrRetryCondition : IRetryCondition
{
    private readonly IRetryCondition[] _conditions;

    /// <summary>
    /// 初始化组合重试条件
    /// </summary>
    /// <param name="conditions">要组合的条件</param>
    public OrRetryCondition(params IRetryCondition[] conditions)
    {
        _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
    }

    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>任一条件满足时返回true</returns>
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return _conditions.Any(condition => condition.ShouldRetry(exception, attemptNumber));
    }
}
