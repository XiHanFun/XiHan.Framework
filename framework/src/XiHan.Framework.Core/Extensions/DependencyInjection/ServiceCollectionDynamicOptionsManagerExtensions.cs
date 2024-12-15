#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionDynamicOptionsManagerExtensions
// Guid:60ea44f1-b78e-449c-890f-38241d5694d7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 5:27:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Options;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 服务集合动态选项管理器扩展方法
/// </summary>
public static class ServiceCollectionDynamicOptionsManagerExtensions
{
    /// <summary>
    /// 添加动态选项
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <typeparam name="TManager"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanDynamicOptions<TOptions, TManager>(this IServiceCollection services)
        where TOptions : class
        where TManager : XiHanDynamicOptionsManager<TOptions>
    {
        _ = services.Replace(ServiceDescriptor.Scoped<IOptions<TOptions>, TManager>());
        _ = services.Replace(ServiceDescriptor.Scoped<IOptionsSnapshot<TOptions>, TManager>());

        return services;
    }
}
