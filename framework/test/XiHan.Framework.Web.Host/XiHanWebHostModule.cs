// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api;
using XiHan.Framework.Web.Docs;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Host;

/// <summary>
/// 曦寒示例 Web 宿主
/// </summary>
[DependsOn(
    typeof(XiHanWebApiModule),
    typeof(XiHanWebDocsModule)
)]
public class XiHanWebHostModule : XiHanModule
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

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
    }
}
