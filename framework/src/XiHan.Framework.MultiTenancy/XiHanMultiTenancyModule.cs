#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanMultiTenancyModule
// Guid:2d25167f-53fa-4771-97b9-fef8f1e84505
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 6:17:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.ConfigurationStore;
using XiHan.Framework.Security;
using XiHan.Framework.Settings;
using XiHan.Framework.Settings.Options;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 曦寒多租户模块
/// </summary>
[DependsOn(
    typeof(XiHanDataModule),
    typeof(XiHanSettingsModule),
    typeof(XiHanSecurityModule)
)]
public class XiHanMultiTenancyModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ICurrentTenantAccessor>(AsyncLocalCurrentTenantAccessor.Instance);

        var configuration = context.Services.GetConfiguration();
        Configure<XiHanDefaultTenantStoreOptions>(configuration);

        Configure<XiHanSettingOptions>(options =>
        {
            options.ValueProviders.InsertAfter(t => t == typeof(GlobalSettingValueProvider), typeof(TenantSettingValueProvider));
        });

        Configure<XiHanTenantResolveOptions>(options =>
        {
            options.TenantResolvers.Insert(0, new CurrentUserTenantResolveContributor());
        });
    }
}
