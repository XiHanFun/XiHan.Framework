// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
