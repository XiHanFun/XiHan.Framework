#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebDocsModule
// Guid:8d8f4d0c-4b66-4d52-b9b7-ef10c658842a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 2:39:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Scalar.AspNetCore;
using XiHan.Framework.AspNetCore.Extensions;
using XiHan.Framework.AspNetCore.Scalar;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Web.Docs;

/// <summary>
/// 曦寒框架 Web 核心 Swagger 文档模块
/// </summary>
[DependsOn(
    typeof(XiHanWebApiModule)
)]
public class XiHanWebDocsModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        _ = services.AddSwaggerGen();
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        _ = app.UseSwagger();
        _ = app.UseSwaggerUI();
        _ = app.UseEndpoints(endpoints =>
        {
            _ = endpoints.MapScalarApiReference();
        });
    }
}
