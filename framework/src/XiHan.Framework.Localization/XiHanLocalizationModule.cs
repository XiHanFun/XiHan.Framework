// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Localization.Extensions.DependencyInjection;
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

        services.AddXiHanLocalization(config);
    }
}
