#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTestsWebModule
// Guid:a08e94e6-05da-4241-b12a-32b2c278e8a6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/10 05:34:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api;
using XiHan.Framework.Web.Docs;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Tests;

/// <summary>
/// 曦寒测试应用 Web 主机
/// </summary>
[DependsOn(
    typeof(XiHanWebApiModule),
    typeof(XiHanWebDocsModule)
)]
public class XiHanTestsWebModule : XiHanModule
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
