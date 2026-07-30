// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Policies;

/// <summary>
/// 策略评估器接口
/// </summary>
public interface IPolicyEvaluator
{
    /// <summary>
    /// 评估策略
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="policyName">策略名称</param>
    /// <param name="resource">资源对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    Task<PolicyEvaluationResult> EvaluateAsync(string userId, string policyName, object? resource = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 评估多个策略（所有策略都必须通过）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="policyNames">策略名称列表</param>
    /// <param name="resource">资源对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    Task<PolicyEvaluationResult> EvaluateAllAsync(string userId, List<string> policyNames, object? resource = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 评估多个策略（任意一个策略通过即可）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="policyNames">策略名称列表</param>
    /// <param name="resource">资源对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    Task<PolicyEvaluationResult> EvaluateAnyAsync(string userId, List<string> policyNames, object? resource = null, CancellationToken cancellationToken = default);
}
