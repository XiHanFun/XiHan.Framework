#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BackgroundJobOptions
// Guid:ada3c42b-83a1-42a6-8a44-1dd908653a48
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.BackgroundJobs.Options;

/// <summary>
/// 后台作业注册表选项（作业处理器 ↔ 参数类型 ↔ 作业名 的映射，启动时由自动发现填充）
/// </summary>
public class BackgroundJobOptions
{
    private readonly Dictionary<Type, BackgroundJobConfiguration> _jobConfigurationsByArgsType = [];
    private readonly Dictionary<string, BackgroundJobConfiguration> _jobConfigurationsByName = [];

    /// <summary>
    /// 添加作业（按处理器类型）
    /// </summary>
    /// <typeparam name="TJob">作业处理器类型</typeparam>
    public void AddJob<TJob>()
        where TJob : IBackgroundJob
    {
        AddJob(typeof(TJob));
    }

    /// <summary>
    /// 添加作业（按处理器类型）
    /// </summary>
    /// <param name="jobType">作业处理器类型</param>
    public void AddJob(Type jobType)
    {
        AddJob(new BackgroundJobConfiguration(jobType));
    }

    /// <summary>
    /// 添加作业配置
    /// </summary>
    /// <param name="configuration">作业配置</param>
    public void AddJob(BackgroundJobConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _jobConfigurationsByArgsType[configuration.ArgsType] = configuration;
        _jobConfigurationsByName[configuration.JobName] = configuration;
    }

    /// <summary>
    /// 按作业名获取配置，不存在返回 null
    /// </summary>
    /// <param name="jobName">作业名</param>
    /// <returns>作业配置</returns>
    public BackgroundJobConfiguration? GetJobOrNull(string jobName)
    {
        return _jobConfigurationsByName.GetValueOrDefault(jobName);
    }

    /// <summary>
    /// 按参数类型获取配置，不存在返回 null
    /// </summary>
    /// <param name="argsType">作业参数类型</param>
    /// <returns>作业配置</returns>
    public BackgroundJobConfiguration? GetJobByArgsOrNull(Type argsType)
    {
        return _jobConfigurationsByArgsType.GetValueOrDefault(argsType);
    }

    /// <summary>
    /// 获取全部已注册作业配置
    /// </summary>
    /// <returns>作业配置列表</returns>
    public IReadOnlyList<BackgroundJobConfiguration> GetJobs()
    {
        return [.. _jobConfigurationsByArgsType.Values];
    }
}
