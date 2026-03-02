#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiSwaggerGenOptionsSetup
// Guid:7a1ad55d-8b76-4d5b-9e73-0b0c8fbbe2f7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace XiHan.Framework.Web.Docs.Swagger;

/// <summary>
/// 动态 API Swagger 分组配置
/// </summary>
internal sealed class DynamicApiSwaggerGenOptionsSetup : IConfigureOptions<SwaggerGenOptions>
{
    /// <summary>
    /// 配置 SwaggerGen
    /// </summary>
    public void Configure(SwaggerGenOptions options)
    {
        EnsureSwaggerDoc(options, DynamicApiSwaggerGroupHelper.DefaultDocName, DynamicApiSwaggerGroupHelper.DefaultDocTitle);

        var groupNames = DynamicApiSwaggerGroupHelper.GetGroupNamesFromAttributes();
        foreach (var groupName in groupNames)
        {
            if (string.Equals(groupName, DynamicApiSwaggerGroupHelper.DefaultDocName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            EnsureSwaggerDoc(options, groupName, groupName);
        }

        options.DocInclusionPredicate((docName, apiDesc) =>
        {
            var groupName = apiDesc.GroupName;

            if (string.IsNullOrWhiteSpace(groupName))
            {
                return string.Equals(docName, DynamicApiSwaggerGroupHelper.DefaultDocName, StringComparison.OrdinalIgnoreCase);
            }

            if (options.SwaggerGeneratorOptions.SwaggerDocs.ContainsKey(groupName))
            {
                return string.Equals(docName, groupName, StringComparison.OrdinalIgnoreCase);
            }

            // 兜底：如果分组文档未注册，回退到默认文档，避免接口丢失
            return string.Equals(docName, DynamicApiSwaggerGroupHelper.DefaultDocName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static void EnsureSwaggerDoc(SwaggerGenOptions options, string docName, string title)
    {
        if (options.SwaggerGeneratorOptions.SwaggerDocs.ContainsKey(docName))
        {
            return;
        }

        options.SwaggerDoc(docName, new OpenApiInfo
        {
            Title = title,
            Version = docName
        });
    }
}
