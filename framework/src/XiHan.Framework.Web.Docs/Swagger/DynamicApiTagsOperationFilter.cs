#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiTagsOperationFilter
// Guid:3e5d8d7a-1c5e-4e5d-8d7a-1c5e3b1c9f3b
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2025-01-26 下午 05:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;

namespace XiHan.Framework.Web.Docs.Swagger;

/// <summary>
/// 动态 API 标签操作过滤器
/// 用于从 DynamicApiAttribute.GroupName 读取并设置 Swagger Tags
/// </summary>
public class DynamicApiTagsOperationFilter : IOperationFilter
{
    /// <summary>
    /// 应用过滤器
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // 检查是否有 OriginalMethodAttribute
        var originalMethodAttr = context.MethodInfo.GetCustomAttribute<OriginalMethodAttribute>();
        if (originalMethodAttr == null)
        {
            return;
        }

        try
        {
            // 获取原始方法
            var originalMethod = originalMethodAttr.ServiceType.GetMethod(
                originalMethodAttr.MethodName,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                originalMethodAttr.ParameterTypes,
                null);

            if (originalMethod == null)
            {
                return;
            }

            // 获取方法级别的 DynamicApiAttribute
            var methodAttr = originalMethod.GetCustomAttribute<DynamicApiAttribute>();

            // 获取类级别的 DynamicApiAttribute
            var classAttr = originalMethodAttr.ServiceType.GetCustomAttribute<DynamicApiAttribute>();

            // 确定使用哪个 GroupName（方法级别优先）
            string? groupName = null;
            if (methodAttr != null && !string.IsNullOrEmpty(methodAttr.GroupName))
            {
                groupName = methodAttr.GroupName;
            }
            else if (classAttr != null && !string.IsNullOrEmpty(classAttr.GroupName))
            {
                groupName = classAttr.GroupName;
            }

            // 如果有 GroupName，设置为 Swagger Tag
            if (!string.IsNullOrEmpty(groupName))
            {
                // 清空默认的 Tags（通常是控制器名称）
                operation.Tags?.Clear();

                // 添加自定义 Tag
                operation.Tags ??= new HashSet<OpenApiTagReference>();
                operation.Tags.Add(new OpenApiTagReference(groupName, null, null));
            }
        }
        catch
        {
            // 忽略错误，不影响 Swagger 生成
        }
    }
}
