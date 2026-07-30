// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Core.Extensions.DependencyInjection;

/// <summary>
/// WebApplicationBuilder 扩展
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// 添加应用程序
    /// </summary>
    /// <typeparam name="TStartupModule"></typeparam>
    /// <param name="builder"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static async Task<IXiHanApplicationWithExternalServiceProvider> AddApplicationAsync<TStartupModule>(
        this WebApplicationBuilder builder,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        return await builder.Services.AddApplicationAsync<TStartupModule>(options =>
        {
            options.Services.ReplaceConfiguration(builder.Configuration);
            optionsAction?.Invoke(options);
            if (options.Environment.IsNullOrWhiteSpace())
            {
                options.Environment = builder.Environment.EnvironmentName;
            }
        });
    }

    /// <summary>
    /// 添加应用程序
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="startupModuleType"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static async Task<IXiHanApplicationWithExternalServiceProvider> AddApplicationAsync(
        this WebApplicationBuilder builder,
        Type startupModuleType,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        return await builder.Services.AddApplicationAsync(startupModuleType, options =>
        {
            options.Services.ReplaceConfiguration(builder.Configuration);
            optionsAction?.Invoke(options);
            if (options.Environment.IsNullOrWhiteSpace())
            {
                options.Environment = builder.Environment.EnvironmentName;
            }
        });
    }
}
