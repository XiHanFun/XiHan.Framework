#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebApiModule
// Guid:7b69fc24-fbf3-4e1b-8175-eed3f45a7c76
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/12 0:38:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Scalar.AspNetCore;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Serialization;
using XiHan.Framework.Web.Core;
using XiHan.Framework.Web.Core.Extensions;

namespace XiHan.Framework.Web.Api;

/// <summary>
/// 曦寒框架 Web 核心文档模块
/// </summary>
[DependsOn(
    typeof(XiHanWebCoreModule),
    typeof(XiHanSerializationModule)
)]
public class XiHanWebApiModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        _ = app.UseEndpoints(endpoints =>
        {
            _ = endpoints.MapScalarApiReference();
        });
    }
}
