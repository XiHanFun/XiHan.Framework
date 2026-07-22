// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
