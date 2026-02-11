#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultPolicyStore
// Guid:f0a1b2c3-d4e5-6789-0123-123456789039
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/09 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Authorization.Policies;

/// <summary>
/// 默认策略存储实现
/// </summary>
/// <remarks>
/// 基于内存的策略存储实现,适用于开发、测试环境或作为参考实现。
/// 生产环境建议实现基于数据库的存储。
/// </remarks>
public class DefaultPolicyStore : IPolicyStore
{
    /// <summary>
    /// 策略定义字典（策略名称 -> 策略定义）
    /// </summary>
    private readonly ConcurrentDictionary<string, PolicyDefinition> _policies = new();

    /// <summary>
    /// 用于线程安全操作的锁对象
    /// </summary>
    private readonly Lock _lockObject = new();

    /// <summary>
    /// 获取所有策略
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略列表</returns>
    public Task<List<PolicyDefinition>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_policies.Values.ToList());
    }

    /// <summary>
    /// 根据名称获取策略
    /// </summary>
    /// <param name="policyName">策略名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略定义</returns>
    public Task<PolicyDefinition?> GetPolicyByNameAsync(string policyName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(policyName))
        {
            return Task.FromResult<PolicyDefinition?>(null);
        }

        _policies.TryGetValue(policyName, out var policy);
        return Task.FromResult(policy);
    }

    /// <summary>
    /// 创建策略
    /// </summary>
    /// <param name="policy">策略定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task CreatePolicyAsync(PolicyDefinition policy, CancellationToken cancellationToken = default)
    {
        if (policy == null || string.IsNullOrEmpty(policy.Name))
        {
            throw new ArgumentException("策略或策略名称不能为空", nameof(policy));
        }

        lock (_lockObject)
        {
            if (_policies.ContainsKey(policy.Name))
            {
                throw new InvalidOperationException($"策略 '{policy.Name}' 已存在");
            }

            _policies.TryAdd(policy.Name, policy);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新策略
    /// </summary>
    /// <param name="policy">策略定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task UpdatePolicyAsync(PolicyDefinition policy, CancellationToken cancellationToken = default)
    {
        if (policy == null || string.IsNullOrEmpty(policy.Name))
        {
            throw new ArgumentException("策略或策略名称不能为空", nameof(policy));
        }

        lock (_lockObject)
        {
            if (!_policies.ContainsKey(policy.Name))
            {
                throw new InvalidOperationException($"策略 '{policy.Name}' 不存在");
            }

            _policies[policy.Name] = policy;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 删除策略
    /// </summary>
    /// <param name="policyName">策略名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task DeletePolicyAsync(string policyName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(policyName))
        {
            return Task.CompletedTask;
        }

        _policies.TryRemove(policyName, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 批量添加策略
    /// </summary>
    /// <param name="policies">策略定义列表</param>
    public Task AddPoliciesAsync(List<PolicyDefinition> policies)
    {
        if (policies == null)
        {
            return Task.CompletedTask;
        }

        foreach (var policy in policies.Where(p => p != null && !string.IsNullOrEmpty(p.Name)))
        {
            _policies.TryAdd(policy.Name, policy);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 清空所有策略
    /// </summary>
    public Task ClearAsync()
    {
        _policies.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 检查策略是否存在
    /// </summary>
    /// <param name="policyName">策略名称</param>
    /// <returns>是否存在</returns>
    public Task<bool> PolicyExistsAsync(string policyName)
    {
        if (string.IsNullOrEmpty(policyName))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_policies.ContainsKey(policyName));
    }
}
