#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowNodeInstanceStatus
// Guid:f7c04b58-2ea1-4d93-8b67-c30d9512ae84
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:12:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
