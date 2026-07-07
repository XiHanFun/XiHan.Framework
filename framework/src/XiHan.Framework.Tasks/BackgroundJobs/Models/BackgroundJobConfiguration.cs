#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BackgroundJobConfiguration
// Guid:4151c5f4-8c23-438a-812f-4866a10a49c5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.BackgroundJobs.Attributes;

namespace XiHan.Framework.Tasks.BackgroundJobs.Models;

/// <summary>
/// 后台作业配置（作业名 ↔ 处理器类型 ↔ 参数类型 的映射）
/// </summary>
public class BackgroundJobConfiguration
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="jobType">作业处理器类型（实现 IAsyncBackgroundJob&lt;TArgs&gt;）</param>
    public BackgroundJobConfiguration(Type jobType)
    {
        ArgumentNullException.ThrowIfNull(jobType);

        JobType = jobType;
        ArgsType = BackgroundJobArgsHelper.GetJobArgsType(jobType);
        JobName = BackgroundJobNameAttribute.GetName(ArgsType);
    }

    /// <summary>
    /// 作业处理器类型
    /// </summary>
    public Type JobType { get; }

    /// <summary>
    /// 作业参数类型
    /// </summary>
    public Type ArgsType { get; }

    /// <summary>
    /// 作业名称
    /// </summary>
    public string JobName { get; }
}
