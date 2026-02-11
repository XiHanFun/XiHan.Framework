#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PolicyEvaluationResult
// Guid:d8e9f0a1-b2c3-4567-1234-123456789037
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization.Policies;

/// <summary>
/// 策略评估结果
/// </summary>
public class PolicyEvaluationResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// 失败原因
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 失败的要求列表
    /// </summary>
    public List<string> FailedRequirements { get; set; } = [];

    /// <summary>
    /// 额外数据
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static PolicyEvaluationResult Success()
    {
        return new PolicyEvaluationResult
        {
            Succeeded = true
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static PolicyEvaluationResult Failure(string failureReason, List<string>? failedRequirements = null)
    {
        return new PolicyEvaluationResult
        {
            Succeeded = false,
            FailureReason = failureReason,
            FailedRequirements = failedRequirements ?? []
        };
    }
}
