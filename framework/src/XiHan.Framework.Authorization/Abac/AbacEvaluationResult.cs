#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AbacEvaluationResult
// Guid:dc59fda5-bcab-4a3a-869c-5ee0a8fd2784
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization.Abac;

/// <summary>
/// ABAC 评估结果
/// </summary>
public sealed class AbacEvaluationResult
{
    /// <summary>
    /// 是否允许
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// 评估说明
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 允许
    /// </summary>
    /// <param name="reason">原因</param>
    /// <returns></returns>
    public static AbacEvaluationResult Allow(string? reason = null)
    {
        return new AbacEvaluationResult
        {
            IsAllowed = true,
            Reason = reason
        };
    }

    /// <summary>
    /// 拒绝
    /// </summary>
    /// <param name="reason">原因</param>
    /// <returns></returns>
    public static AbacEvaluationResult Deny(string? reason = null)
    {
        return new AbacEvaluationResult
        {
            IsAllowed = false,
            Reason = reason
        };
    }
}
