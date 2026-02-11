#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanApplicationModule
// Guid:d8e2aab0-7d8f-4a66-bfbc-fe461151dc3b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 03:33:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Application.Contracts;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Domain;
using XiHan.Framework.Logging;
using XiHan.Framework.ObjectMapping;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Application;

/// <summary>
/// 曦寒框架领域驱动应用模块
/// </summary>
[DependsOn(
    typeof(XiHanLoggingModule),
    typeof(XiHanApplicationContractsModule),
    typeof(XiHanDomainModule),
    typeof(XiHanDistributedIdsModule),
    typeof(XiHanObjectMappingModule)
    )]
public class XiHanApplicationModule : XiHanModule
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
