// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

        // 只按租户标识定键（与租户级设置同一口径），平台（0 号租户）没有租户级特性值
        if (currentTenant.Id is not { } tenantId || tenantId <= 0)
        {
            return null;
        }

        var tenantProviderKey = tenantId.ToString();

        return await settingStore.GetOrNullAsync(
            FeatureKeyPrefix + featureName.Trim(),
            TenantSettingValueProvider.ProviderName,
            tenantProviderKey);
    }
}
