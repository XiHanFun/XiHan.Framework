// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.MultiTenancy.Features;

/// <summary>
/// 租户功能检查器
/// </summary>
public interface ITenantFeatureChecker
{
    /// <summary>
    /// 判断功能是否启用
    /// </summary>
    /// <param name="featureName"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    Task<bool> IsEnabledAsync(string featureName, bool defaultValue = false);

    /// <summary>
    /// 获取功能原始值
    /// </summary>
    /// <param name="featureName"></param>
    /// <returns></returns>
    Task<string?> GetValueOrNullAsync(string featureName);
}
