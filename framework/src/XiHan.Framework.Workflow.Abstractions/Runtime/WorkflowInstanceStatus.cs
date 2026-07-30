// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Runtime;

/// <summary>
/// 流程实例状态
/// </summary>
public enum WorkflowInstanceStatus
{
    /// <summary>
    /// 运行中（含等待书签恢复的空闲状态）
    /// </summary>
    Running = 1,

    /// <summary>
    /// 已挂起（人工暂停，书签保留但拒绝恢复，直至实例恢复运行）
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 3,

    /// <summary>
    /// 已取消（可触发补偿）
    /// </summary>
    Canceled = 4,

    /// <summary>
    /// 已故障（可通过重试恢复运行）
    /// </summary>
    Faulted = 5,

    /// <summary>
    /// 已终止（强制结束，不补偿、不可恢复）
    /// </summary>
    Terminated = 6
}
