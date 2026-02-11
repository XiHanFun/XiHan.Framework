#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TenantFeatureChecker
// Guid:8055ac68-a79e-49e0-a380-2bb646180887
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Settings.Stores;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.MultiTenancy.Features;

/// <summary>
/// 租户功能检查器
/// </summary>
public class TenantFeatureChecker(
    ICurrentTenant currentTenant,
    ISettingStore settingStore) : ITenantFeatureChecker, ITransientDependency
{
    /// <summary>
    /// 功能设置键前缀
    /// </summary>
    public const string FeatureKeyPrefix = "Feature:";

    /// <summary>
    /// 判断功能是否启用
    /// </summary>
    /// <param name="featureName"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public async Task<bool> IsEnabledAsync(string featureName, bool defaultValue = false)
    {
        var value = await GetValueOrNullAsync(featureName);
        if (value.IsNullOrWhiteSpace())
        {
            return defaultValue;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取功能原始值
    /// </summary>
    /// <param name="featureName"></param>
    /// <returns></returns>
    public async Task<string?> GetValueOrNullAsync(string featureName)
    {
        if (featureName.IsNullOrWhiteSpace())
        {
            return null;
        }

        var tenantProviderKey = !currentTenant.Name.IsNullOrWhiteSpace()
            ? currentTenant.Name
            : currentTenant.Id?.ToString();
        if (tenantProviderKey.IsNullOrWhiteSpace())
        {
            return null;
        }

        return await settingStore.GetOrNullAsync(
            FeatureKeyPrefix + featureName.Trim(),
            TenantSettingValueProvider.ProviderName,
            tenantProviderKey);
    }
}
