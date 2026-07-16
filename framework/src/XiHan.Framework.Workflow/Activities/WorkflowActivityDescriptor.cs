#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowActivityDescriptor
// Guid:4b81f26d-e053-4c97-ba28-70d5e19c46f3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:48:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
