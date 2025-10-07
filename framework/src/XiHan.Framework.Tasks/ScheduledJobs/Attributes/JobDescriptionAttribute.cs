#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobDescriptionAttribute.cs
// Guid:a58e5729-0709-4441-b891-4f124f1c7098
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 17:33:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
