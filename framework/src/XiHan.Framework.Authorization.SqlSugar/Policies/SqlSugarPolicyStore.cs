// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.SqlSugar.Entities;
using XiHan.Framework.Authorization.SqlSugar.Mapping;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;

namespace XiHan.Framework.Authorization.SqlSugar.Policies;

/// <summary>
/// 策略存储的 SqlSugar 实现
/// </summary>
/// <remarks>
/// 不支持持久化 <see cref="PolicyDefinition.CustomRequirements"/>，含自定义要求的策略写入时抛出 <see cref="NotSupportedException"/>。
/// </remarks>
public class SqlSugarPolicyStore : IPolicyStore
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly IDistributedIdGenerator<long> _idGenerator;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="idGenerator">主键生成器</param>
    public SqlSugarPolicyStore(
        ISqlSugarClientResolver clientResolver,
        IDistributedIdGenerator<long> idGenerator)
    {
        _clientResolver = clientResolver;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 当前租户主库的客户端
    /// </summary>
    private ISqlSugarClient Client => _clientResolver.GetCurrentClient();

    /// <summary>
    /// 获取所有策略
    /// </summary>
    /// <remarks>
    /// 按名称序数升序返回，不过滤启用状态。
    /// </remarks>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略列表</returns>
    public async Task<List<PolicyDefinition>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Client.Queryable<SysAuthzPolicy>().ToListAsync(cancellationToken);

        return
        [
            .. entities
                .OrderBy(entity => entity.PolicyName, StringComparer.Ordinal)
                .Select(PolicyMapper.ToDefinition)
        ];
    }

    /// <summary>
    /// 根据名称获取策略
    /// </summary>
    /// <param name="policyName">策略名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略定义，不存在时返回空</returns>
    public async Task<PolicyDefinition?> GetPolicyByNameAsync(string policyName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(policyName))
        {
            return null;
        }

        var entity = await Client.Queryable<SysAuthzPolicy>()
            .FirstAsync(item => item.PolicyName == policyName, cancellationToken);

        return entity is null ? null : PolicyMapper.ToDefinition(entity);
    }

    /// <summary>
    /// 创建策略
    /// </summary>
    /// <param name="policy">策略定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentException">策略或策略名称为空，或任一要求集合为空引用</exception>
    /// <exception cref="NotSupportedException">策略含自定义要求</exception>
    /// <exception cref="InvalidOperationException">同名策略已存在</exception>
    public async Task CreatePolicyAsync(PolicyDefinition policy, CancellationToken cancellationToken = default)
    {
        EnsurePersistable(policy);

        var client = Client;

        if (await client.Queryable<SysAuthzPolicy>().AnyAsync(item => item.PolicyName == policy.Name, cancellationToken))
        {
            throw new InvalidOperationException($"策略 '{policy.Name}' 已存在");
        }

        await client.Insertable(PolicyMapper.ToEntity(policy, _idGenerator.NextId())).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 更新策略
    /// </summary>
    /// <remarks>
    /// 按名称匹配，改写名称以外的全部字段。
    /// </remarks>
    /// <param name="policy">策略定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentException">策略或策略名称为空，或任一要求集合为空引用</exception>
    /// <exception cref="NotSupportedException">策略含自定义要求</exception>
    /// <exception cref="InvalidOperationException">策略不存在</exception>
    public async Task UpdatePolicyAsync(PolicyDefinition policy, CancellationToken cancellationToken = default)
    {
        EnsurePersistable(policy);

        var client = Client;
        var existing = await client.Queryable<SysAuthzPolicy>()
            .FirstAsync(item => item.PolicyName == policy.Name, cancellationToken)
            ?? throw new InvalidOperationException($"策略 '{policy.Name}' 不存在");

        var updated = PolicyMapper.ToEntity(policy, existing.BasicId);

        await client.Updateable<SysAuthzPolicy>()
            .SetColumns(item => new SysAuthzPolicy
            {
                DisplayName = updated.DisplayName,
                Description = updated.Description,
                RequiredRoles = updated.RequiredRoles,
                RequiredPermissions = updated.RequiredPermissions,
                RequiredClaims = updated.RequiredClaims,
                IsEnabled = updated.IsEnabled,
                Properties = updated.Properties
            })
            .Where(item => item.BasicId == existing.BasicId)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 删除策略
    /// </summary>
    /// <param name="policyName">策略名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task DeletePolicyAsync(string policyName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(policyName))
        {
            return;
        }

        await Client.Deleteable<SysAuthzPolicy>()
            .Where(item => item.PolicyName == policyName)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 校验策略可以写入
    /// </summary>
    /// <param name="policy">策略定义</param>
    /// <exception cref="ArgumentException">策略或策略名称为空，或任一要求集合为空引用</exception>
    /// <exception cref="NotSupportedException">策略含自定义要求</exception>
    private static void EnsurePersistable(PolicyDefinition policy)
    {
        if (policy is null || string.IsNullOrEmpty(policy.Name))
        {
            throw new ArgumentException("策略或策略名称不能为空", nameof(policy));
        }

        if (policy.RequiredRoles is null ||
            policy.RequiredPermissions is null ||
            policy.RequiredClaims is null ||
            policy.CustomRequirements is null)
        {
            throw new ArgumentException($"策略 '{policy.Name}' 的要求集合不能为空引用，没有要求时请传空集合", nameof(policy));
        }

        if (policy.CustomRequirements.Count > 0)
        {
            throw new NotSupportedException(
                $"策略 '{policy.Name}' 含自定义要求，SqlSugar 策略存储无法持久化自定义要求。" +
                "含自定义要求的策略请由应用自行实现的 IPolicyStore 提供。");
        }
    }
}
