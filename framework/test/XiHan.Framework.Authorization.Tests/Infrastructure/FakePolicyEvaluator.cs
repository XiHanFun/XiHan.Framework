// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Policies;

namespace XiHan.Framework.Authorization.Tests.Infrastructure;

/// <summary>
/// 策略评估器替身
/// </summary>
/// <remarks>
/// 授权服务只负责把策略评估结果翻译成授权结果，这里把评估结果固定住，专测那层翻译。
/// </remarks>
public sealed class FakePolicyEvaluator : IPolicyEvaluator
{
    /// <summary>
    /// 固定返回的评估结果
    /// </summary>
    public PolicyEvaluationResult Result { get; set; } = PolicyEvaluationResult.Success();

    /// <summary>
    /// 最近一次收到的策略名称
    /// </summary>
    public string? LastPolicyName { get; private set; }

    /// <summary>
    /// 最近一次收到的资源对象
    /// </summary>
    public object? LastResource { get; private set; }

    /// <summary>
    /// 评估单个策略
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="policyName">策略名称</param>
    /// <param name="resource">资源对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    public Task<PolicyEvaluationResult> EvaluateAsync(string userId, string policyName, object? resource = null, CancellationToken cancellationToken = default)
    {
        LastPolicyName = policyName;
        LastResource = resource;
        return Task.FromResult(Result);
    }

    /// <summary>
    /// 评估多个策略（全部通过）
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="policyNames">策略名称列表</param>
    /// <param name="resource">资源对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    public Task<PolicyEvaluationResult> EvaluateAllAsync(string userId, List<string> policyNames, object? resource = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result);
    }

    /// <summary>
    /// 评估多个策略（任意通过）
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="policyNames">策略名称列表</param>
    /// <param name="resource">资源对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    public Task<PolicyEvaluationResult> EvaluateAnyAsync(string userId, List<string> policyNames, object? resource = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result);
    }
}
