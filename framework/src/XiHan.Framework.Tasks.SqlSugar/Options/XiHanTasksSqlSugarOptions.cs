// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.SqlSugar.Options;

/// <summary>
/// 任务 SqlSugar 存储配置
/// </summary>
public class XiHanTasksSqlSugarOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Tasks:SqlSugar";

    /// <summary>
    /// 后台作业租约时长，领取后超过该时长仍未删除或更新的作业可被重新领取
    /// </summary>
    public TimeSpan BackgroundJobLeaseTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 运行中任务实例的宽限期，开始时间加任务超时再加宽限期之后，实例不再视为运行中
    /// </summary>
    public TimeSpan RunningInstanceGracePeriod { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 单次领取的作业数量上限，实际领取数量取调用方请求数量与该值的较小者，必须大于零
    /// </summary>
    public int MaxClaimBatchSize { get; set; } = 50;
}
