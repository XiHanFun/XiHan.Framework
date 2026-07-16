#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowUserTaskOutcomes
// Guid:e2c50f81-9a4d-4736-bd2e-61f8a3b7c904
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:04:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 人工任务标准结果常量
/// </summary>
/// <remarks>
/// 结果值会作为 <c>outcome</c> 变量参与出边条件求值（如 <c>outcome == 'approved'</c>），
/// 除标准值外也允许业务自定义任意结果字符串。
/// </remarks>
public static class WorkflowUserTaskOutcomes
{
    /// <summary>
    /// 同意
    /// </summary>
    public const string Approved = "approved";

    /// <summary>
    /// 拒绝
    /// </summary>
    public const string Rejected = "rejected";

    /// <summary>
    /// 超时
    /// </summary>
    public const string Timeout = "timeout";
}
