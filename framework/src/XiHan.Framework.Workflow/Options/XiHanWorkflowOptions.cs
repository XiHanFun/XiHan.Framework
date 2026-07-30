// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Collections;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Options;

/// <summary>
/// 曦寒框架工作流选项
/// </summary>
public class XiHanWorkflowOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Workflow";

    /// <summary>
    /// 已注册的活动类型列表（内置活动由框架注册，自定义活动经注册扩展方法加入）
    /// </summary>
    public ITypeList<IWorkflowActivity> Activities { get; } = new TypeList<IWorkflowActivity>();

    /// <summary>
    /// 单次执行批次的最大节点执行数（防止定义中的环路失控空转）
    /// </summary>
    public int MaxNodeExecutionsPerBurst { get; set; } = 1000;

    /// <summary>
    /// 实例锁过期秒数
    /// </summary>
    public int InstanceLockExpirySeconds { get; set; } = 120;

    /// <summary>
    /// 实例锁获取超时秒数（超时未获取到锁视为并发冲突抛出异常）
    /// </summary>
    public int InstanceLockAcquireTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// 实例锁获取重试间隔毫秒数
    /// </summary>
    public int InstanceLockRetryIntervalMilliseconds { get; set; } = 200;

    /// <summary>
    /// 实例不可恢复（挂起/故障）时定时类书签的到期回退秒数（避免同一到期书签每轮轮询空转占满取回配额）
    /// </summary>
    public int NotResumableTimerBackoffSeconds { get; set; } = 300;

    /// <summary>
    /// 子流程最大嵌套深度（超过后子实例拒绝启动并回调父节点故障，阻断递归定义失控）
    /// </summary>
    public int MaxSubWorkflowDepth { get; set; } = 16;
}
