#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDomainModule
// Guid:639ae3b3-41a1-49a6-a958-695e4e2bdf0a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 03:33:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Domain.Shared;

namespace XiHan.Framework.Domain;

/// <summary>
/// 曦寒框架领域驱动设计模块
/// </summary>
[DependsOn(
    typeof(XiHanDomainSharedModule)
    )]
public class XiHanDomainModule : XiHanModule
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
