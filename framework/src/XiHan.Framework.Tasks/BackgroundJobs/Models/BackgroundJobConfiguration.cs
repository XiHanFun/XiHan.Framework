// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
