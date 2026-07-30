// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.ScheduledJobs.Attributes;

/// <summary>
/// 任务超时特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class JobTimeoutAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="timeoutMilliseconds">超时时间（毫秒）</param>
    public JobTimeoutAttribute(int timeoutMilliseconds)
    {
        TimeoutMilliseconds = timeoutMilliseconds;
    }

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int TimeoutMilliseconds { get; }
}
