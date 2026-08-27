// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Policies;

namespace XiHan.Framework.Authorization.Tests.Infrastructure;

/// <summary>
/// 带查询计数的策略存储
/// </summary>
/// <remarks>
/// 多策略评估的短路行为无法从返回值观察，只能靠“后面的策略有没有被查过”来判定，因此在读路径上加计数。
/// </remarks>
public sealed class CountingPolicyStore : IPolicyStore
{
    private readonly DefaultPolicyStore _inner = new();

    /// <summary>
    /// 被查询过的策略名称，按调用顺序记录
    /// </summary>
    public List<string> QueriedPolicyNames { get; } = [];

    /// <summary>
    /// 获取所有策略
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略定义列表</returns>
    public Task<List<PolicyDefinition>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
    {
        return _inner.GetAllPoliciesAsync(cancellationToken);
    }

    /// <summary>
    /// 按名称获取策略
    /// </summary>
    /// <param name="policyName">策略名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略定义</returns>
    public Task<PolicyDefinition?> GetPolicyByNameAsync(string policyName, CancellationToken cancellationToken = default)
    {
        QueriedPolicyNames.Add(policyName);
        return _inner.GetPolicyByNameAsync(policyName, cancellationToken);
    }

    /// <summary>
    /// 创建策略
    /// </summary>
    /// <param name="policy">策略定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task CreatePolicyAsync(PolicyDefinition policy, CancellationToken cancellationToken = default)
    {
        return _inner.CreatePolicyAsync(policy, cancellationToken);
    }

    /// <summary>
    /// 更新策略
    /// </summary>
    /// <param name="policy">策略定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task UpdatePolicyAsync(PolicyDefinition policy, CancellationToken cancellationToken = default)
    {
        return _inner.UpdatePolicyAsync(policy, cancellationToken);
    }

    /// <summary>
    /// 删除策略
    /// </summary>
    /// <param name="policyName">策略名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task DeletePolicyAsync(string policyName, CancellationToken cancellationToken = default)
    {
        return _inner.DeletePolicyAsync(policyName, cancellationToken);
    }
}
