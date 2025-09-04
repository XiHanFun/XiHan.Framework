#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RetryResult
// Guid:39385b00-daca-4ddd-b2ae-5ac872b1f7b9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/5 7:29:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Diagnostics.RetryPolicys;

/// <summary>
/// 重试结果基类
/// </summary>
public class RetryResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 最后一次的异常
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 总耗时
    /// </summary>
    public TimeSpan TotalElapsedTime { get; set; }

    /// <summary>
    /// 所有尝试的记录
    /// </summary>
    public List<RetryAttempt> Attempts { get; set; } = [];

    /// <summary>
    /// 总尝试次数
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// 是否超时
    /// </summary>
    public bool IsTimeout => !IsSuccess && Attempts.Count != 0;

    /// <summary>
    /// 平均每次尝试的耗时
    /// </summary>
    public TimeSpan AverageAttemptTime => TotalAttempts > 0
        ? TimeSpan.FromTicks(TotalElapsedTime.Ticks / TotalAttempts)
        : TimeSpan.Zero;
}

/// <summary>
/// 带返回值的重试结果
/// </summary>
/// <typeparam name="T">返回值类型</typeparam>
public class RetryResult<T> : RetryResult
{
    /// <summary>
    /// 执行结果
    /// </summary>
    public T? Result { get; set; }
}

/// <summary>
/// 单次重试尝试的记录
/// </summary>
public class RetryAttempt
{
    /// <summary>
    /// 尝试次数
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 本次尝试的耗时
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// 本次尝试的异常
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 尝试时间
    /// </summary>
    public DateTime AttemptTime { get; set; } = DateTime.Now;
}
