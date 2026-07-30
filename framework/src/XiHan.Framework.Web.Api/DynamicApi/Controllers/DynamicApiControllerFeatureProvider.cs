// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Helpers;
using XiHan.Framework.Web.Api.DynamicApi.Options;

namespace XiHan.Framework.Web.Api.DynamicApi.Controllers;

/// <summary>
/// 动态 API 控制器特性提供者
/// </summary>
public class DynamicApiControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private readonly IDynamicApiConvention? _convention;
    private readonly DynamicApiOptions _options;
    private readonly ILogger<DynamicApiControllerFeatureProvider> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="convention">动态 API 约定</param>
    /// <param name="options">动态 API 选项</param>
    /// <param name="logger">日志记录器</param>
    public DynamicApiControllerFeatureProvider(
        IDynamicApiConvention? convention,
        DynamicApiOptions? options,
        ILogger<DynamicApiControllerFeatureProvider>? logger = null)
    {
        _convention = convention;
        _options = options ?? new DynamicApiOptions();
        _logger = logger ?? NullLogger<DynamicApiControllerFeatureProvider>.Instance;
    }

    /// <summary>
    /// 填充特性
    /// </summary>
    /// <param name="parts">应用程序部件</param>
    /// <param name="feature">控制器特性</param>
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        // 检查是否启用动态 API
        if (!_options.IsEnabled)
        {
            _logger.LogInformation("动态 API 功能已禁用");
            return;
        }

        _logger.LogInformation("开始生成动态 API 控制器...");

        // 获取所有应用服务类型
        var serviceTypes = GetApplicationServiceTypes(parts);
        var totalServices = serviceTypes.Count();

        _logger.LogInformation("找到 {TotalServices} 个应用服务待处理", totalServices);

        var successCount = 0;
        var failCount = 0;

        foreach (var serviceType in serviceTypes)
        {
            try
            {
                // 创建动态控制器类型
                var controllerType = DynamicApiControllerFactory.CreateControllerType(
                    serviceType.AsType(),
                    _convention,
                    _options,
                    _logger);

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
                _logger.LogError(ex, "为服务 '{ServiceName}' 创建动态控制器失败", serviceType.Name);
            }
        }

        _logger.LogInformation("动态 API 生成完成: {SuccessCount} 个成功, {FailCount} 个失败",
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
