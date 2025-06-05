#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExtensionPropertyPolicyChecker
// Guid:395eb299-3ddf-4be2-a0c8-2fa6a3d6fe6c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 7:39:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.ObjectExtending.Modularity;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.ObjectExtending;

/// <summary>
/// 扩展属性策略检查器
/// 负责检查扩展属性的访问策略，包括全局功能、功能开关和权限验证
/// 实现瞬时依赖注入生命周期
/// </summary>
public class ExtensionPropertyPolicyChecker : ITransientDependency
{
    /// <summary>
    /// 异步检查扩展属性策略是否满足要求
    /// 依次检查全局功能、功能开关和权限，所有检查都必须通过
    /// </summary>
    /// <param name="policy">扩展属性策略配置</param>
    /// <returns>如果策略检查通过返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当 policy 为 null 时</exception>
    public virtual async Task<bool> CheckPolicyAsync([NotNull] ExtensionPropertyPolicyConfiguration policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!await CheckAsync(policy.GlobalFeatures.Features, policy.GlobalFeatures.RequiresAll, CheckGlobalFeaturesAsync))
        {
            return false;
        }

        if (!await CheckAsync(policy.Features.Features, policy.Features.RequiresAll, CheckFeaturesAsync))
        {
            return false;
        }

        return await CheckAsync(policy.Permissions.PermissionNames, policy.Permissions.RequiresAll, CheckPermissionsAsync);
    }

    /// <summary>
    /// 通用的异步检查方法，用于检查名称数组是否满足策略要求
    /// 支持"要求全部"和"要求任一"两种检查模式
    /// </summary>
    /// <param name="names">待检查的名称数组</param>
    /// <param name="requiresAll">是否要求全部名称都通过检查</param>
    /// <param name="checkFunc">具体的检查函数</param>
    /// <returns>如果检查通过返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当 checkFunc 为 null 时</exception>
    protected virtual async Task<bool> CheckAsync(string[] names, bool requiresAll, Func<string, Task<bool>> checkFunc)
    {
        ArgumentNullException.ThrowIfNull(checkFunc);

        if (names.IsNullOrEmpty())
        {
            return true;
        }

        var hasAny = false;
        foreach (var name in names)
        {
            if (!await checkFunc(name))
            {
                if (requiresAll)
                {
                    return false;
                }
            }
            else
            {
                hasAny = true;
                if (!requiresAll)
                {
                    break;
                }
            }
        }

        return hasAny;
    }

    /// <summary>
    /// 检查全局功能是否启用
    /// 默认实现总是返回 true，子类可重写此方法实现具体的全局功能检查逻辑
    /// </summary>
    /// <param name="featureName">全局功能名称</param>
    /// <returns>如果全局功能启用返回 true，否则返回 false</returns>
    protected virtual Task<bool> CheckGlobalFeaturesAsync(string featureName)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// 检查功能开关是否启用
    /// 默认实现总是返回 true，子类可重写此方法实现具体的功能开关检查逻辑
    /// </summary>
    /// <param name="featureName">功能名称</param>
    /// <returns>如果功能启用返回 true，否则返回 false</returns>
    protected virtual Task<bool> CheckFeaturesAsync(string featureName)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// 检查用户是否具有指定权限
    /// 默认实现总是返回 true，子类可重写此方法实现具体的权限检查逻辑
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <returns>如果用户具有权限返回 true，否则返回 false</returns>
    protected virtual Task<bool> CheckPermissionsAsync(string permissionName)
    {
        return Task.FromResult(true);
    }
}
