// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Runtime;

/// <summary>
/// 节点实例状态
/// </summary>
public enum WorkflowNodeInstanceStatus
{
    /// <summary>
    /// 执行中
    /// </summary>
    Running = 1,

    /// <summary>
    /// 已挂起（等待书签恢复）
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 3,

    /// <summary>
    /// 已取消
    /// </summary>
    Canceled = 4,

    /// <summary>
    /// 已故障（若存在重试书签则等待重试）
    /// </summary>
    Faulted = 5,

    /// <summary>
    /// 已补偿
    /// </summary>
    Compensated = 6
}
