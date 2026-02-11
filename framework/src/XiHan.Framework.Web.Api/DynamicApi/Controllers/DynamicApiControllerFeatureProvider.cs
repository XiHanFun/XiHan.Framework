#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiControllerFeatureProvider
// Guid:5d33eae4-1145-4d01-acf7-8f46657910dd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using XiHan.Framework.Web.Api.DynamicApi.Configuration;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Helpers;

namespace XiHan.Framework.Web.Api.DynamicApi.Controllers;

/// <summary>
/// 动态 API 控制器特性提供者
/// </summary>
public class DynamicApiControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiControllerFeatureProvider()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    public DynamicApiControllerFeatureProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 填充特性
    /// </summary>
    /// <param name="parts">应用程序部件</param>
    /// <param name="feature">控制器特性</param>
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        // 尝试获取配置和约定
        IDynamicApiConvention? convention = null;
        DynamicApiOptions? options = null;
        ILogger? logger = null;

        if (_serviceProvider != null)
        {
            convention = _serviceProvider.GetService<IDynamicApiConvention>();
            options = _serviceProvider.GetService<DynamicApiOptions>();
            logger = _serviceProvider.GetService<ILogger<DynamicApiControllerFeatureProvider>>();
        }

        // 检查是否启用动态 API
        if (options?.IsEnabled == false)
        {
            logger?.LogInformation("动态 API 功能已禁用");
            return;
        }

        logger?.LogInformation("开始生成动态 API 控制器...");

        // 获取所有应用服务类型
        var serviceTypes = GetApplicationServiceTypes(parts);
        var totalServices = serviceTypes.Count();

        logger?.LogInformation("找到 {TotalServices} 个应用服务待处理", totalServices);

        var successCount = 0;
        var failCount = 0;

        foreach (var serviceType in serviceTypes)
        {
            try
            {
                // 创建动态控制器类型
                var controllerType = DynamicApiControllerFactory.CreateControllerType(
                    serviceType.AsType(),
                    convention,
                    options,
                    logger);

                if (controllerType != null)
                {
                    var controllerTypeInfo = controllerType.GetTypeInfo();
                    if (!feature.Controllers.Contains(controllerTypeInfo))
                    {
                        feature.Controllers.Add(controllerTypeInfo);
                        successCount++;
                    }
                }
                else
                {
                    failCount++;
                }
            }
            catch (Exception ex)
            {
                failCount++;
                logger?.LogError(ex, "为服务 '{ServiceName}' 创建动态控制器失败", serviceType.Name);
            }
        }

        logger?.LogInformation("动态 API 生成完成: {SuccessCount} 个成功, {FailCount} 个失败",
            successCount, failCount);
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
        return TypeHelper.IsApplicationService(typeInfo);
    }
}
