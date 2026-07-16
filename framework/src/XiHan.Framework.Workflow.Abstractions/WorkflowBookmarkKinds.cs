#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowBookmarkKinds
// Guid:6d2f84a9-31c7-4b5e-8f60-92ade1c47b05
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:02:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 书签种类常量
/// </summary>
/// <remarks>
/// 书签是流程实例挂起后可被外界恢复的等待点，种类决定恢复来源与 <c>Key</c> 的语义：
/// <list type="bullet">
/// <item><see cref="UserTask"/>：Key = 受理人标识</item>
/// <item><see cref="Timer"/>：DueTime = 到期时间，由定时器 Worker 轮询恢复</item>
/// <item><see cref="Signal"/>：Key = 信号名称</item>
/// <item><see cref="SubWorkflow"/>：Key = 父节点实例标识，由子流程终态回调恢复</item>
/// <item><see cref="Retry"/>：DueTime = 下次重试时间，由定时器 Worker 轮询恢复</item>
/// <item><see cref="NodeTimeout"/>：DueTime = 节点超时时间，由定时器 Worker 轮询恢复</item>
/// </list>
/// </remarks>
public static class WorkflowBookmarkKinds
{
    /// <summary>
    /// 人工任务
    /// </summary>
    public const string UserTask = "UserTask";

    /// <summary>
    /// 定时器
    /// </summary>
    public const string Timer = "Timer";

    /// <summary>
    /// 信号
    /// </summary>
    public const string Signal = "Signal";

    /// <summary>
    /// 子流程
    /// </summary>
    public const string SubWorkflow = "SubWorkflow";

    /// <summary>
    /// 节点重试
    /// </summary>
    public const string Retry = "Retry";

    /// <summary>
    /// 节点超时
    /// </summary>
    public const string NodeTimeout = "NodeTimeout";
}
