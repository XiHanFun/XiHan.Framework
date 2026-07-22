// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 曦寒应用接口
/// </summary>
public interface IXiHanApplication : IModuleContainer, IApplicationInfoAccessor, IDisposable
{
    /// <summary>
    /// 应用程序启动(入口)模块的类型
    /// </summary>
    Type StartupModuleType { get; }

    /// <summary>
    /// 所有服务注册的列表
    /// 应用程序初始化后，不能向这个集合添加新的服务
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// 应用程序根服务提供器
    /// 在初始化应用程序之前不能使用
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 调用模块的 Pre/Configure/Post的ServicesAsync 方法
    /// 在使用这个方法之前，必须设置 <see cref="XiHanApplicationCreationOptions.SkipConfigureServices"/> 选项为 true
    /// </summary>
    Task ConfigureServicesAsync();

    /// <summary>
    /// 用于优雅地关闭应用程序和所有模块
    /// </summary>
    Task ShutdownAsync();

    /// <summary>
    /// 用于优雅地关闭应用程序和所有模块
    /// </summary>
    void Shutdown();
}
