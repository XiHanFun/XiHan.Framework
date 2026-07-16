#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowInstanceStatus
// Guid:5e13d7a2-98c4-4f6b-b0d5-27a8e64c19f3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:11:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
