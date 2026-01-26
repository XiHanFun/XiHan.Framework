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
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api;
using XiHan.Framework.Web.Core.Extensions;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Docs.Swagger;

namespace XiHan.Framework.Web.Docs;

/// <summary>
/// 曦寒框架 Web 核心文档模块
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
        var config = services.GetConfiguration();

        services.AddSwaggerGen(options =>
        {
            // 读取所有程序集的 XML 文档
            var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
            foreach (var xmlFile in xmlFiles)
            {
                options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);
            }

            // 添加动态 API 标签过滤器（用于设置 Swagger Tags）
            options.OperationFilter<DynamicApiTagsOperationFilter>();

            // 添加动态 API XML 注释过滤器（用于读取原始服务方法的 XML 注释）
            options.OperationFilter<DynamicApiXmlCommentsOperationFilter>();
        });
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        // 启用 Swagger 中间件（生成 OpenAPI JSON）
        app.UseSwagger();
        
        // 启用 Swagger UI
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
        });

        // 启用 Scalar（使用 Swagger 生成的 OpenAPI JSON）
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapScalarApiReference(options =>
            {
                // Scalar 会自动读取 Swagger 生成的 OpenAPI JSON
                options.WithTitle("XiHan Framework API")
                       .WithTheme(ScalarTheme.Purple)
                       .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
        });
    }
}
