#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebTestModule
// Guid:c9bf348b-8c2f-4e2a-9f36-cc2edafe551e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/10 5:34:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AspNetCore;
using XiHan.Framework.AspNetCore.Mvc;
using XiHan.Framework.AspNetCore.Serilog;
using XiHan.Framework.AspNetCore.Swagger;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Web.Test;

/// <summary>
/// 曦寒测试应用 Web 主机
/// </summary>
[DependsOn(
    typeof(XiHanAspNetCoreModule),
    typeof(XiHanAspNetCoreMvcModule),
    typeof(XiHanAspNetCoreSerilogModule),
    typeof(XiHanAspNetCoreSwaggerModule)
)]
public class XiHanWebTestModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
    }
}
