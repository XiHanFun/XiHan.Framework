#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionPreConfigureExtensions
// Guid:8276465a-ab72-47ae-99a4-cdf7e76aa23c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 20:46:22
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Options;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 服务容器预配置扩展方法
/// </summary>
public static class ServiceCollectionPreConfigureExtensions
{
    /// <summary>
    /// 预配置
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="services"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static IServiceCollection PreConfigure<TOptions>(this IServiceCollection services, Action<TOptions> optionsAction)
    {
        services.GetPreConfigureActions<TOptions>().Add(optionsAction);
        return services;
    }

    /// <summary>
    /// 执行预配置委托
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static TOptions ExecutePreConfiguredActions<TOptions>(this IServiceCollection services)
        where TOptions : new()
    {
        return services.ExecutePreConfiguredActions(new TOptions());
    }

    /// <summary>
    /// 执行预配置委托
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="services"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static TOptions ExecutePreConfiguredActions<TOptions>(this IServiceCollection services, TOptions options)
    {
        services.GetPreConfigureActions<TOptions>().Configure(options);
        return options;
    }

    /// <summary>
    /// 获取预配置委托
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static PreConfigureActionList<TOptions> GetPreConfigureActions<TOptions>(this IServiceCollection services)
    {
        var actionList = services.GetSingletonInstanceOrNull<IObjectAccessor<PreConfigureActionList<TOptions>>>()?.Value;
        if (actionList is not null)
        {
            return actionList;
        }

        actionList = [];
        _ = services.AddObjectAccessor(actionList);

        return actionList;
    }
}
