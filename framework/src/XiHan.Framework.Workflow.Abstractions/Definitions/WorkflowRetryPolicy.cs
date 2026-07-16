#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowRetryPolicy
// Guid:0c6e2f91-84b5-4da3-b7e6-52a90d13f8c4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:06:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 节点重试策略（指数退避）
/// </summary>
public class WorkflowRetryPolicy
{
    /// <summary>
    /// 最大尝试次数（含首次执行，1 表示不重试）
    /// </summary>
    public int MaxAttempts { get; set; } = 1;

    /// <summary>
    /// 首次重试等待秒数
    /// </summary>
    public int FirstDelaySeconds { get; set; } = 10;

    /// <summary>
    /// 退避倍率（第 N 次重试等待 = 首次等待 × 倍率^(N-1)）
    /// </summary>
    public double BackoffFactor { get; set; } = 2.0;
}
