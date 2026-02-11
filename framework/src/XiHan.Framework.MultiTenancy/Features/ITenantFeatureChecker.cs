#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITenantFeatureChecker
// Guid:f4b49787-8e06-42ec-a16c-eb89fd9d6a13
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
