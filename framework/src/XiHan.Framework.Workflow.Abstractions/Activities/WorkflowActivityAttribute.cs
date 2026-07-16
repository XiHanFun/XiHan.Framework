#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowActivityAttribute
// Guid:15c9f2e8-7ad4-4b06-93c1-e58d20b6f7a9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:21:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 工作流活动元数据特性（声明活动类型编码与流转行为）
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class WorkflowActivityAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="activityType">活动类型编码（流程定义节点通过该编码引用活动）</param>
    public WorkflowActivityAttribute(string activityType)
    {
        ActivityType = activityType;
    }

    /// <summary>
    /// 活动类型编码
    /// </summary>
    public string ActivityType { get; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 分类
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 出边流转行为
    /// </summary>
    public ActivityOutgoingBehavior OutgoingBehavior { get; set; } = ActivityOutgoingBehavior.AllMatched;
}
