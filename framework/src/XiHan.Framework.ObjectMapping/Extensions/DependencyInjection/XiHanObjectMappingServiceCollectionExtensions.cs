// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.ObjectMapping.Extensions.DependencyInjection;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class XiHanObjectMappingServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒框架对象映射服务
    /// </summary>
    /// <param name="services"></param>
    public static IServiceCollection AddXiHanMapster(this IServiceCollection services)
    {
        services.AddTransient<IMapper, Mapper>();

        return services;
    }
}
