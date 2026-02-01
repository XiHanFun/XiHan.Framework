#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLocalizationAbstractionsModule
// Guid:7f9c2e4b-1a3d-4c8f-9b2e-8d6e5f4c3a2b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/11 05:12:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 曦寒框架本地化抽象模块
/// </summary>
public class XiHanLocalizationAbstractionsModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();
    }
}
