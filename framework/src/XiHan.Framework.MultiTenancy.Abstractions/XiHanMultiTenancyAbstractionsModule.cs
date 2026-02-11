#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanMultiTenancyAbstractionsModule
// Guid:5b8e4f7a-3c2d-4e9a-b1f5-9a7c6d4e3f2b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/11 05:04:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 曦寒框架多租户抽象模块
/// </summary>
public class XiHanMultiTenancyAbstractionsModule : XiHanModule
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
