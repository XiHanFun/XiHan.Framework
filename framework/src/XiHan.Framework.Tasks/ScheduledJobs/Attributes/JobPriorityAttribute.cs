// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Attributes;

/// <summary>
/// 任务优先级特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class JobPriorityAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="priority">优先级</param>
    public JobPriorityAttribute(JobPriority priority)
    {
        Priority = priority;
    }

    /// <summary>
    /// 优先级
    /// </summary>
    public JobPriority Priority { get; }
}
