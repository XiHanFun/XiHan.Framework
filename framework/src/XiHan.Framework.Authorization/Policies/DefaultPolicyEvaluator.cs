#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultPolicyEvaluator
// Guid:a2b3c4d5-e6f7-8901-2345-123456789040
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/09 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Roles;

namespace XiHan.Framework.Authorization.Policies;

/// <summary>
/// 默认策略评估器实现
/// </summary>
public class DefaultPolicyEvaluator : IPolicyEvaluator
{
    private readonly IPolicyStore _policyStore;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IRoleStore _roleStore;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="policyStore">策略存储</param>
    /// <param name="permissionChecker">权限检查器</param>
    /// <param name="roleStore">角色存储</param>
    public DefaultPolicyEvaluator(
        IPolicyStore policyStore,
        IPermissionChecker permissionChecker,
        IRoleStore roleStore)
    {
        _policyStore = policyStore;
        _permissionChecker = permissionChecker;
        _roleStore = roleStore;
    }

    /// <summary>
    /// 评估策略
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="policyName">策略名称</param>
    /// <param name="resource">资源对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    public async Task<PolicyEvaluationResult> EvaluateAsync(
        string userId,
        string policyName,
        object? resource = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return PolicyEvaluationResult.Failure("用户ID不能为空");
        }

        if (string.IsNullOrEmpty(policyName))
        {
            return PolicyEvaluationResult.Failure("策略名称不能为空");
        }

        // 获取策略定义
        var policy = await _policyStore.GetPolicyByNameAsync(policyName, cancellationToken);
        if (policy == null)
        {
            return PolicyEvaluationResult.Failure($"策略 '{policyName}' 不存在");
        }

        if (!policy.IsEnabled)
        {
            return PolicyEvaluationResult.Failure($"策略 '{policyName}' 已禁用");
        }

        var failedRequirements = new List<string>();

        // 检查角色要求（任意一个角色即可）
        if (policy.RequiredRoles.Count > 0)
        {
            var hasAnyRole = false;
            foreach (var roleName in policy.RequiredRoles)
            {
                if (await _roleStore.IsInRoleAsync(userId, roleName, cancellationToken))
                {
                    hasAnyRole = true;
                    break;
                }
            }

            if (!hasAnyRole)
            {
                failedRequirements.Add($"需要以下角色之一: {string.Join(", ", policy.RequiredRoles)}");
            }
        }

        // 检查权限要求（所有权限都必须有）
        if (policy.RequiredPermissions.Count > 0)
        {
            foreach (var permissionName in policy.RequiredPermissions)
            {
                if (!await _permissionChecker.IsGrantedAsync(userId, permissionName, cancellationToken))
                {
                    failedRequirements.Add($"缺少权限: {permissionName}");
                }
            }
        }

        // 检查声明要求
        if (policy.RequiredClaims.Count > 0)
        {
            // 注意：这里需要从某处获取用户的声明信息
            // 暂时标记为未通过，实际使用时需要扩展此功能
            foreach (var claim in policy.RequiredClaims)
            {
                failedRequirements.Add($"缺少声明: {claim.Key} = {claim.Value}");
            }
        }

        // 评估自定义要求
        if (policy.CustomRequirements.Count > 0)
        {
            var context = new AuthorizationContext
            {
                UserId = userId,
                PolicyName = policyName,
                Resource = resource,
                UserRoles = [.. (await _roleStore.GetUserRolesAsync(userId, cancellationToken)).Select(r => r.Name)],
                UserPermissions = [.. (await _permissionChecker.GetGrantedPermissionsAsync(userId, cancellationToken))]
            };

            foreach (var requirement in policy.CustomRequirements)
            {
                try
                {
                    if (!await requirement.EvaluateAsync(context))
                    {
                        failedRequirements.Add($"自定义要求未通过: {requirement.Name}");
                    }
                }
                catch (Exception ex)
                {
                    failedRequirements.Add($"自定义要求评估异常: {requirement.Name} - {ex.Message}");
                }
            }
        }

        // 返回评估结果
        if (failedRequirements.Count > 0)
        {
            return PolicyEvaluationResult.Failure(
                $"策略 '{policyName}' 评估失败",
                failedRequirements);
        }

        return PolicyEvaluationResult.Success();
    }

    /// <summary>
    /// 评估多个策略（所有策略都必须通过）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="policyNames">策略名称列表</param>
    /// <param name="resource">资源对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    public async Task<PolicyEvaluationResult> EvaluateAllAsync(
        string userId,
        List<string> policyNames,
        object? resource = null,
        CancellationToken cancellationToken = default)
    {
        var names = policyNames.ToList();
        if (names.Count == 0)
        {
            return PolicyEvaluationResult.Failure("策略名称列表不能为空");
        }

        var allFailedRequirements = new List<string>();

        foreach (var policyName in names)
        {
            var result = await EvaluateAsync(userId, policyName, resource, cancellationToken);
            if (!result.Succeeded)
            {
                allFailedRequirements.AddRange(result.FailedRequirements);
            }
        }

        if (allFailedRequirements.Count > 0)
        {
            return PolicyEvaluationResult.Failure(
                "部分策略评估失败",
                allFailedRequirements);
        }

        return PolicyEvaluationResult.Success();
    }

    /// <summary>
    /// 评估多个策略（任意一个策略通过即可）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="policyNames">策略名称列表</param>
    /// <param name="resource">资源对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    public async Task<PolicyEvaluationResult> EvaluateAnyAsync(
        string userId,
        List<string> policyNames,
        object? resource = null,
        CancellationToken cancellationToken = default)
    {
        var names = policyNames.ToList();
        if (names.Count == 0)
        {
            return PolicyEvaluationResult.Failure("策略名称列表不能为空");
        }

        var allFailedRequirements = new List<string>();

        foreach (var policyName in names)
        {
            var result = await EvaluateAsync(userId, policyName, resource, cancellationToken);
            if (result.Succeeded)
            {
                return PolicyEvaluationResult.Success();
            }

            allFailedRequirements.AddRange(result.FailedRequirements);
        }

        return PolicyEvaluationResult.Failure(
            "所有策略评估都失败",
            allFailedRequirements);
    }
}
