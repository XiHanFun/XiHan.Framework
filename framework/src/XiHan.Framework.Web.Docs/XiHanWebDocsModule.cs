#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebDocsModule
// Guid:8d8f4d0c-4b66-4d52-b9b7-ef10c658842a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 02:39:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Scalar.AspNetCore;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api;
using XiHan.Framework.Web.Core.Extensions;
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

        // 默认文档（v1）启用动态 API XML 注释增强
        services.AddOpenApi(options =>
        {
            options.AddOperationTransformer<DynamicApiXmlCommentsOperationTransformer>();
        });

        var groupDefinitions = DynamicApiSwaggerGroupHelper.GetGroupDefinitionsFromAttributes();
        foreach (var groupDefinition in groupDefinitions)
        {
            if (string.Equals(groupDefinition.Group, DynamicApiSwaggerGroupHelper.DefaultDocName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 以官方 OpenAPI 文档为基准：为每个动态分组注册独立文档
            services.AddOpenApi(groupDefinition.Group, options =>
            {
                options.ShouldInclude = apiDescription =>
                    string.Equals(apiDescription.GroupName, groupDefinition.Group, StringComparison.OrdinalIgnoreCase);
                options.AddOperationTransformer<DynamicApiXmlCommentsOperationTransformer>();
            });
        }
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        // 启用 Swagger UI（读取官方 OpenAPI 文档）
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

        // 启用 Scalar（读取官方 OpenAPI 文档）
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapScalarApiReference((options, httpContext) =>
            {
                var groupDefinitions = DynamicApiSwaggerGroupHelper.GetGroupDefinitionsFromAttributes();

                var extraGroupDefinitions = groupDefinitions
                    .Where(definition =>
                        !string.Equals(definition.Group, DynamicApiSwaggerGroupHelper.DefaultDocName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Scalar 会自动读取官方 OpenAPI JSON
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
            });
        });
    }
}
