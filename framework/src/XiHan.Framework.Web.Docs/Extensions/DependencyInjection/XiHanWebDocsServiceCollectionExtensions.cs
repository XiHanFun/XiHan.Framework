#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebDocsServiceCollectionExtensions
// Guid:d0e1f2a3-4567-890a-bcde-f12345678901
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/06 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Web.Docs.Swagger;

namespace XiHan.Framework.Web.Docs.Extensions.DependencyInjection;

/// <summary>
/// 曦寒框架 Web 文档服务集合扩展
/// </summary>
public static class XiHanWebDocsServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒 Web 文档服务（OpenApi + 动态 API 分组）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanWebDocs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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

        return services;
    }
}
