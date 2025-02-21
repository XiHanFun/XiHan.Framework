#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanConsoleTestModule
// Guid:c9bf348b-8c2f-4e2a-9f36-cc2edafe551e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/10 5:34:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AspNetCore.Authentication.JwtBearer;
using XiHan.Framework.AspNetCore.Authentication.OAuth;
using XiHan.Framework.AspNetCore.Scalar;
using XiHan.Framework.AspNetCore.Serilog;
using XiHan.Framework.AspNetCore.SignalR;
using XiHan.Framework.AspNetCore.Swagger;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Ddd.Application;

namespace XiHan.Framework.Console.Test;

/// <summary>
/// 曦寒测试应用 Web 主机
/// </summary>
[DependsOn(
    typeof(XiHanDddApplicationModule),
    typeof(XiHanAspNetCoreSerilogModule),
    typeof(XiHanAspNetCoreSignalRModule),
    typeof(XiHanAspNetCoreAuthenticationJwtBearerModule),
    typeof(XiHanAspNetCoreAuthenticationOAuthModule),
    typeof(XiHanAspNetCoreScalarModule),
    typeof(XiHanAspNetCoreSwaggerModule)
)]
public class XiHanConsoleTestModule : XiHanModule
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

    /// <summary>
    /// 应用关闭
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
    }
}
