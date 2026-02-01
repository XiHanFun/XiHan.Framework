#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLocalizationModule
// Guid:638c874e-7656-4255-865f-ab6d97320828
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/27 13:40:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Settings;
using XiHan.Framework.Threading;
using XiHan.Framework.VirtualFileSystem;

namespace XiHan.Framework.Localization;

/// <summary>
/// 曦寒框架本地化模块
/// </summary>
[DependsOn(
    typeof(XiHanLocalizationAbstractionsModule),
    typeof(XiHanVirtualFileSystemModule),
    typeof(XiHanThreadingModule),
    typeof(XiHanSettingsModule)
)]
public class XiHanLocalizationModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();
    }
}
