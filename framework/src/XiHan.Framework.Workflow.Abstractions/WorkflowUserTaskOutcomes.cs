// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
