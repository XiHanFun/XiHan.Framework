#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowDefinitionStatus
// Guid:7a90c3e5-1b48-4f2d-96a7-d05e83f1c26b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 流程定义状态
/// </summary>
public enum WorkflowDefinitionStatus
{
    /// <summary>
    /// 草稿
    /// </summary>
    Draft = 0,

    /// <summary>
    /// 已发布（可启动实例）
    /// </summary>
    Published = 1,

    /// <summary>
    /// 已停用（不可启动新实例，存量实例继续运行）
    /// </summary>
    Disabled = 2,

    /// <summary>
    /// 已归档
    /// </summary>
    Archived = 3
}
