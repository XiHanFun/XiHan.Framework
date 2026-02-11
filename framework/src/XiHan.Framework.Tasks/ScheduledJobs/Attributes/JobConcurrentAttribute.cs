#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobConcurrentAttribute
// Guid:65644c80-a18a-414f-8799-0b98078f9ff3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 14:35:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
