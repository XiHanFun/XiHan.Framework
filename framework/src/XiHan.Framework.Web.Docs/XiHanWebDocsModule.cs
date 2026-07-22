// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Scalar.AspNetCore;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api;
using XiHan.Framework.Web.Core.Extensions;
using XiHan.Framework.Web.Docs.Extensions.DependencyInjection;
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

        services.AddXiHanWebDocs();
    }

    /// <summary>
    /// 应用初始化前：注册 Swagger UI 中间件，确保在认证/授权中间件之前处理文档请求
    /// </summary>
    /// <param name="context"></param>
    public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseSwaggerUI(options =>
        {
            var groupDefinitions = DynamicApiSwaggerGroupHelper.GetGroupDefinitionsFromAttributes();

            var extraGroupDefinitions = groupDefinitions
                .Where(definition =>
                    !string.Equals(definition.Group, DynamicApiSwaggerGroupHelper.DefaultDocName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            options.SwaggerEndpoint(
                $"/openapi/{DynamicApiSwaggerGroupHelper.DefaultDocName}.json",
                DynamicApiSwaggerGroupHelper.DefaultDocTitle);

            foreach (var groupDefinition in extraGroupDefinitions)
            {
                options.SwaggerEndpoint($"/openapi/{groupDefinition.Group}.json", groupDefinition.DisplayName);
            }
        });
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapScalarApiReference((options, httpContext) =>
            {
                var groupDefinitions = DynamicApiSwaggerGroupHelper.GetGroupDefinitionsFromAttributes();

                var extraGroupDefinitions = groupDefinitions
                    .Where(definition =>
                        !string.Equals(definition.Group, DynamicApiSwaggerGroupHelper.DefaultDocName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                options.WithTitle("XiHan Framework API")
                       .WithTheme(ScalarTheme.Purple)
                       .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                       .WithOpenApiRoutePattern("/openapi/{documentName}.json")
                       .AddDocument(
                           DynamicApiSwaggerGroupHelper.DefaultDocName,
                           DynamicApiSwaggerGroupHelper.DefaultDocTitle,
                           isDefault: true);

                foreach (var groupDefinition in extraGroupDefinitions)
                {
                    options.AddDocument(groupDefinition.Group, groupDefinition.DisplayName);
                }
            }).AllowAnonymous();
        });
    }
}
