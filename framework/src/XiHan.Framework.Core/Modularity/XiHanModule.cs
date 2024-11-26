#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanModule
// Guid:c4f81f64-ba21-48e1-b1a7-0c64b5954491
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:43:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 曦寒模块化服务配置基类
/// </summary>
public abstract class XiHanModule : IPreConfigureServices, IXiHanModule, IPostConfigureServices,
    IOnPreApplicationInitialization, IOnApplicationInitialization, IOnPostApplicationInitialization, IOnApplicationShutdown
{
    private ServiceConfigurationContext? _serviceConfigurationContext;

    /// <summary>
    /// 服务配置上下文
    /// </summary>
    protected internal ServiceConfigurationContext ServiceConfigurationContext
    {
        get => _serviceConfigurationContext ??
            throw new XiHanException($"{nameof(ServiceConfigurationContext)}只能在{nameof(ConfigureServices)}、{nameof(PreConfigureServices)}和{nameof(PostConfigureServices)}方法中使用。");
        internal set => _serviceConfigurationContext = value;
    }

    /// <summary>
    /// 是否跳过自动服务注册
    /// </summary>
    protected internal bool SkipAutoServiceRegistration { get; protected set; }

    #region 服务配置

    /// <summary>
    /// 服务配置前，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual Task PreConfigureServicesAsync(ServiceConfigurationContext context)
    {
        PreConfigureServices(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 服务配置前
    /// </summary>
    /// <param name="context"></param>
    public virtual void PreConfigureServices(ServiceConfigurationContext context)
    {
    }

    /// <summary>
    /// 服务配置，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual Task ConfigureServicesAsync(ServiceConfigurationContext context)
    {
        ConfigureServices(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public virtual void ConfigureServices(ServiceConfigurationContext context)
    {
    }

    /// <summary>
    /// 服务配置后，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual Task PostConfigureServicesAsync(ServiceConfigurationContext context)
    {
        PostConfigureServices(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 服务配置后
    /// </summary>
    /// <param name="context"></param>
    public virtual void PostConfigureServices(ServiceConfigurationContext context)
    {
    }

    #endregion

    #region 程序相关

    /// <summary>
    /// 程序初始化前，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual Task OnPreApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        OnPreApplicationInitialization(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 程序初始化前
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="NotImplementedException"></exception>
    public virtual void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
    }

    /// <summary>
    /// 程序初始化，异步
    /// 通常由启动模块用于构建 ASP.NET Core 应用程序的中间件管道
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public virtual Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        OnApplicationInitialization(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 程序初始化
    /// 通常由启动模块用于构建 ASP.NET Core 应用程序的中间件管道
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="NotImplementedException"></exception>
    public virtual void OnApplicationInitialization(ApplicationInitializationContext context)
    {
    }

    /// <summary>
    /// 程序初始化后，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        OnPostApplicationInitialization(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 程序初始化后
    /// </summary>
    /// <param name="context"></param>
    public virtual void OnPostApplicationInitialization(ApplicationInitializationContext context)
    {
    }

    /// <summary>
    /// 程序关闭时，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
    {
        OnApplicationShutdown(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 程序关闭时
    /// </summary>
    /// <param name="context"></param>
    public virtual void OnApplicationShutdown(ApplicationShutdownContext context)
    {
    }

    #endregion

    #region 配置选项

    /// <summary>
    /// 配置选项
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="configureOptions"></param>
    protected void Configure<TOptions>(Action<TOptions> configureOptions)
        where TOptions : class
    {
        _ = ServiceConfigurationContext.Services.Configure(configureOptions);
    }

    /// <summary>
    /// 配置选项
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="name"></param>
    /// <param name="configureOptions"></param>
    protected void Configure<TOptions>(string name, Action<TOptions> configureOptions)
        where TOptions : class
    {
        _ = ServiceConfigurationContext.Services.Configure(name, configureOptions);
    }

    /// <summary>
    /// 配置选项
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="configuration"></param>
    protected void Configure<TOptions>(IConfiguration configuration)
        where TOptions : class
    {
        _ = ServiceConfigurationContext.Services.Configure<TOptions>(configuration);
    }

    /// <summary>
    /// 配置选项
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="configuration"></param>
    /// <param name="configureBinder"></param>
    protected void Configure<TOptions>(IConfiguration configuration, Action<BinderOptions> configureBinder)
        where TOptions : class
    {
        _ = ServiceConfigurationContext.Services.Configure<TOptions>(configuration, configureBinder);
    }

    /// <summary>
    /// 配置选项
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="name"></param>
    /// <param name="configuration"></param>
    protected void Configure<TOptions>(string name, IConfiguration configuration)
        where TOptions : class
    {
        _ = ServiceConfigurationContext.Services.Configure<TOptions>(name, configuration);
    }

    /// <summary>
    /// 配置前选项
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="configureOptions"></param>
    protected void PreConfigure<TOptions>(Action<TOptions> configureOptions)
        where TOptions : class
    {
        _ = ServiceConfigurationContext.Services.PreConfigure(configureOptions);
    }

    /// <summary>
    /// 配置后选项
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="configureOptions"></param>
    protected void PostConfigure<TOptions>(Action<TOptions> configureOptions)
        where TOptions : class
    {
        _ = ServiceConfigurationContext.Services.PostConfigure(configureOptions);
    }

    /// <summary>
    /// 配置前所有选项
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <param name="configureOptions"></param>
    protected void PostConfigureAll<TOptions>(Action<TOptions> configureOptions)
        where TOptions : class
    {
        _ = ServiceConfigurationContext.Services.PostConfigureAll(configureOptions);
    }

    #endregion
}