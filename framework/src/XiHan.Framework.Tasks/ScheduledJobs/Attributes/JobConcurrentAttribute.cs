// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.ScheduledJobs.Attributes;

/// <summary>
/// 任务并发特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class JobConcurrentAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="allowConcurrent">是否允许并发执行</param>
    public JobConcurrentAttribute(bool allowConcurrent = true)
    {
        AllowConcurrent = allowConcurrent;
    }

    /// <summary>
    /// 是否允许并发执行
    /// </summary>
    public bool AllowConcurrent { get; }
}
