#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiControllerFeatureProvider
// Guid:i9j0k1l2-m3n4-4o5p-6q7r-8s9t0u1v2w3x
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using XiHan.Framework.Application.Services;

namespace XiHan.Framework.Web.Api.DynamicApi.Controllers;

/// <summary>
/// 动态 API 控制器特性提供者
/// </summary>
public class DynamicApiControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    /// <summary>
    /// 填充特性
    /// </summary>
    /// <param name="parts">应用程序部件</param>
    /// <param name="feature">控制器特性</param>
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        // 获取所有应用服务类型
        var serviceTypes = GetApplicationServiceTypes(parts);

        foreach (var serviceType in serviceTypes)
        {
            // 创建动态控制器类型
            var controllerType = DynamicApiControllerFactory.CreateControllerType(serviceType.AsType());
            if (controllerType != null)
            {
                var controllerTypeInfo = controllerType.GetTypeInfo();
                if (!feature.Controllers.Contains(controllerTypeInfo))
                {
                    feature.Controllers.Add(controllerTypeInfo);
                }
            }
        }
    }

    /// <summary>
    /// 获取应用服务类型
    /// </summary>
    private static IEnumerable<TypeInfo> GetApplicationServiceTypes(IEnumerable<ApplicationPart> parts)
    {
        var serviceTypes = new List<TypeInfo>();

        foreach (var part in parts.OfType<IApplicationPartTypeProvider>())
        {
            foreach (var type in part.Types)
            {
                if (IsApplicationService(type))
                {
                    serviceTypes.Add(type);
                }
            }
        }

        return serviceTypes;
    }

    /// <summary>
    /// 判断是否是应用服务
    /// </summary>
    private static bool IsApplicationService(TypeInfo typeInfo)
    {
        return typeInfo.IsClass &&
               !typeInfo.IsAbstract &&
               typeInfo.IsPublic &&
               typeof(IApplicationService).IsAssignableFrom(typeInfo);
    }
}
