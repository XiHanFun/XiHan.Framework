#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanApplicationContractsModule
// Guid:fab6d8c3-4e22-4b89-a5f5-8c6f3a9e1c2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/11 04:52:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Application.Contracts;

/// <summary>
/// 曦寒框架应用层契约模块
/// </summary>
public class XiHanApplicationContractsModule : XiHanModule
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
