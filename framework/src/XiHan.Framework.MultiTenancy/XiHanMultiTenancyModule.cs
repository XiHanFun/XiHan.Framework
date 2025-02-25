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

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Security;
using XiHan.Framework.Settings;

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 曦寒多租户模块
/// </summary>
[DependsOn(
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
    }
}
