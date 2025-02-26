#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLocalizationModule
// Guid:3e2d1f0a-9b8c-7d6e-5f4a-3e2d-1f0a-9b8c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 13:40:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Localization.Extensions;
using XiHan.Framework.Settings;
using XiHan.Framework.Threading;
using XiHan.Framework.VirtualFileSystem;

namespace XiHan.Framework.Localization;

/// <summary>
/// 曦寒框架本地化模块
/// </summary>
[DependsOn(
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

        // 注册本地化服务
        _ = services.AddXiHanLocalization();
    }
}
