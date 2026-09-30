// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Mapping;

/// <summary>
/// 后台作业契约与实体的双向映射
/// </summary>
public static class BackgroundJobMapper
{
    /// <summary>
    /// 把后台作业信息转换为实体，领取令牌与领取时刻置空
    /// </summary>
    /// <param name="info">后台作业信息</param>
    /// <returns>后台作业实体</returns>
    public static SysBackgroundJob ToEntity(BackgroundJobInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return new SysBackgroundJob(info.Id)
        {
            ApplicationName = ToApplicationKey(info.ApplicationName),
            TenantId = info.TenantId,
            JobName = info.JobName,
            JobArgs = info.JobArgs,
            TryCount = info.TryCount,
            CreationTime = info.CreationTime,
            NextTryTime = info.NextTryTime,
            LastTryTime = info.LastTryTime,
            IsAbandoned = info.IsAbandoned,
            Priority = (int)info.Priority,
            ClaimToken = null,
            ClaimTime = null
        };
    }

    /// <summary>
    /// 把实体转换为后台作业信息，空字符串的应用名还原为空
    /// </summary>
    /// <param name="entity">后台作业实体</param>
    /// <returns>后台作业信息</returns>
    public static BackgroundJobInfo ToJobInfo(SysBackgroundJob entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new BackgroundJobInfo
        {
            Id = entity.BasicId,
            ApplicationName = entity.ApplicationName.Length == 0 ? null : entity.ApplicationName,
            TenantId = entity.TenantId,
            JobName = entity.JobName,
            JobArgs = entity.JobArgs,
            TryCount = entity.TryCount,
            CreationTime = entity.CreationTime,
            NextTryTime = entity.NextTryTime,
            LastTryTime = entity.LastTryTime,
            IsAbandoned = entity.IsAbandoned,
            Priority = (BackgroundJobPriority)entity.Priority
        };
    }

    /// <summary>
    /// 把应用名转换为存储键，空值转换为空字符串
    /// </summary>
    /// <param name="applicationName">应用名</param>
    /// <returns>存储键</returns>
    public static string ToApplicationKey(string? applicationName)
    {
        return applicationName ?? string.Empty;
    }
}
