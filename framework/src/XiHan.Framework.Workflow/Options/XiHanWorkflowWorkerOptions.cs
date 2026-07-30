// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Options;

/// <summary>
/// 曦寒框架工作流定时器 Worker 选项
/// </summary>
public class XiHanWorkflowWorkerOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Workflow:Worker";

    /// <summary>
    /// 是否启用定时器轮询（关闭后延时/重试/超时书签不会被自动恢复）
    /// </summary>
    public bool IsTimerEnabled { get; set; } = true;

    /// <summary>
    /// 启动后首次轮询前的等待毫秒数
    /// </summary>
    public int FirstWaitDurationMilliseconds { get; set; } = 5000;

    /// <summary>
    /// 轮询周期毫秒数
    /// </summary>
    public int PollPeriodMilliseconds { get; set; } = 5000;

    /// <summary>
    /// 单轮最大取回书签数
    /// </summary>
    public int MaxBookmarkFetchCount { get; set; } = 100;

    /// <summary>
    /// 分布式锁资源名（集群内单活轮询）
    /// </summary>
    public string DistributedLockName { get; set; } = "XiHanWorkflowTimerWorker";

    /// <summary>
    /// 分布式锁过期秒数
    /// </summary>
    public int DistributedLockExpirySeconds { get; set; } = 300;
}
