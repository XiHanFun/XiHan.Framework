// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Policies;

/// <summary>
/// 策略存储接口
/// </summary>
/// <remarks>
/// 用户需要实现此接口来提供策略数据的存储和检索功能
/// </remarks>
public interface IPolicyStore
{
    /// <summary>
    /// 获取所有策略
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略列表</returns>
    Task<List<PolicyDefinition>> GetAllPoliciesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据名称获取策略
    /// </summary>
    /// <param name="policyName">策略名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略定义</returns>
    Task<PolicyDefinition?> GetPolicyByNameAsync(string policyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建策略
    /// </summary>
    /// <param name="policy">策略定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CreatePolicyAsync(PolicyDefinition policy, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新策略
    /// </summary>
    /// <param name="policy">策略定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdatePolicyAsync(PolicyDefinition policy, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除策略
    /// </summary>
    /// <param name="policyName">策略名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeletePolicyAsync(string policyName, CancellationToken cancellationToken = default);
}
