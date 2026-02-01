#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObjectMappingServiceCollectionExtensions
// Guid:8eb2d44d-4e2e-49e9-887e-b59e940c2b19
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/06 04:57:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
