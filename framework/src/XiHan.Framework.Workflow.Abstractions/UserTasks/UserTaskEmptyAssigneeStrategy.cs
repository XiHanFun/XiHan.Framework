// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.UserTasks;

/// <summary>
/// 人工任务受理人为空时的处理策略
/// </summary>
/// <remarks>
/// 动态规则（角色、部门主管等）在运行时可能一个人都解析不到，策略决定此时节点如何收场；
/// 自动通过/自动驳回会在审批轨迹中留下无办理人的记录。
/// </remarks>
public enum UserTaskEmptyAssigneeStrategy
{
    /// <summary>
    /// 节点故障（默认；实例进入故障态，修正组织数据后可重试）
    /// </summary>
    Fault = 1,

    /// <summary>
    /// 自动通过（按 approved 结果流转）
    /// </summary>
    AutoApprove = 2,

    /// <summary>
    /// 自动驳回（按 rejected 结果流转）
    /// </summary>
    AutoReject = 3,

    /// <summary>
    /// 转交兜底受理人（按 FallbackAssigneeRules 再解析一次，仍为空则节点故障）
    /// </summary>
    Fallback = 4
}
