// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.ScheduledJobs.Attributes;

/// <summary>
/// 任务描述特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class JobDescriptionAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="description">任务描述</param>
    public JobDescriptionAttribute(string description)
    {
        Description = description;
    }

    /// <summary>
    /// 任务描述
    /// </summary>
    public string Description { get; }
}
