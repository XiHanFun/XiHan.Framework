// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动执行结果种类
/// </summary>
public enum ActivityExecutionResultKind
{
    /// <summary>
    /// 已完成（继续流转）
    /// </summary>
    Completed = 1,

    /// <summary>
    /// 已挂起（等待书签恢复）
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// 已故障（按重试策略处理）
    /// </summary>
    Faulted = 3
}
