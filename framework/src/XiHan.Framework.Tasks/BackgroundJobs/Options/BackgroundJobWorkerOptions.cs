// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundJobs.Options;

/// <summary>
/// 后台作业 Worker 调优选项
/// </summary>
public class BackgroundJobWorkerOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:BackgroundJobs";

    /// <summary>
    /// 是否启用后台作业执行（数据迁移等场景可关闭；关闭后入队仍可用，只是不执行）
    /// </summary>
    public bool IsJobExecutionEnabled { get; set; } = true;

    /// <summary>
    /// 应用名称（多实例隔离；为空表示不区分）
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// 首次轮询前的等待（毫秒）
    /// </summary>
    public int FirstWaitDurationMilliseconds { get; set; } = 5000;

    /// <summary>
    /// 轮询间隔（毫秒）
    /// </summary>
    public int JobPollPeriodMilliseconds { get; set; } = 5000;

    /// <summary>
    /// 每轮最多领取的作业数量
    /// </summary>
    public int MaxJobFetchCount { get; set; } = 1000;

    /// <summary>
    /// 首次失败后的等待秒数（退避基数）
    /// </summary>
    public int DefaultFirstWaitDurationSeconds { get; set; } = 60;

    /// <summary>
    /// 退避倍率
    /// </summary>
    public double DefaultWaitFactor { get; set; } = 2.0;

    /// <summary>
    /// 放弃阈值（秒）：自创建起累计重试时间超过此值即放弃，默认 2 天。为唯一的失败上限（无固定次数）。
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 172800;

    /// <summary>
    /// 分布式锁名称（保证多实例下单活 Worker，避免重复执行）
    /// </summary>
    public string DistributedLockName { get; set; } = "XiHanBackgroundJobWorker";

    /// <summary>
    /// 分布式锁 TTL（秒）：崩溃安全网，应大于单轮处理耗时
    /// </summary>
    public int DistributedLockExpirySeconds { get; set; } = 300;
}
