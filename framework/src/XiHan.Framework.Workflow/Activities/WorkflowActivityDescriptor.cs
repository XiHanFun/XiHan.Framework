// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Activities;

/// <summary>
/// 活动描述符（活动类型编码到 CLR 类型的映射与元数据）
/// </summary>
public class WorkflowActivityDescriptor
{
    /// <summary>
    /// 活动类型编码
    /// </summary>
    public required string ActivityType { get; init; }

    /// <summary>
    /// 活动 CLR 类型
    /// </summary>
    public required Type ClrType { get; init; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 分类
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// 出边流转行为
    /// </summary>
    public ActivityOutgoingBehavior OutgoingBehavior { get; init; }
}
