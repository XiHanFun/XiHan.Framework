// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
