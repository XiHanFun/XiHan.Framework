// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Web.Core.Tests.Infrastructure;

/// <summary>
/// 手写的曦寒应用替身，只记录初始化/关闭/释放的调用次序，不真正装配模块
/// </summary>
/// <remarks>
/// InitializeApplication 系列扩展的契约就是"把 IApplicationBuilder 交给应用并登记生命周期回调"，
/// 用替身才能在不启动真实模块系统的前提下观察这三件事。
/// </remarks>
public sealed class FakeXiHanApplication : IXiHanApplicationWithExternalServiceProvider
{
    /// <summary>
    /// 模块列表，替身固定为空
    /// </summary>
    public IReadOnlyList<IModuleDescriptor> Modules { get; } = [];

    /// <summary>
    /// 应用名称
    /// </summary>
    public string? ApplicationName => "XiHan.Framework.Web.Core.Tests";

    /// <summary>
    /// 应用实例标识
    /// </summary>
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 启动模块类型
    /// </summary>
    public Type StartupModuleType => typeof(XiHanWebCoreModule);

    /// <summary>
    /// 服务集合
    /// </summary>
    public IServiceCollection Services { get; } = new ServiceCollection();

    /// <summary>
    /// 根服务提供器
    /// </summary>
    public IServiceProvider ServiceProvider { get; private set; } = default!;

    /// <summary>
    /// 初始化时收到的服务提供器
    /// </summary>
    public IServiceProvider? InitializedServiceProvider { get; private set; }

    /// <summary>
    /// 同步初始化调用次数
    /// </summary>
    public int InitializeCallCount { get; private set; }

    /// <summary>
    /// 异步初始化调用次数
    /// </summary>
    public int InitializeAsyncCallCount { get; private set; }

    /// <summary>
    /// 同步关闭调用次数
    /// </summary>
    public int ShutdownCallCount { get; private set; }

    /// <summary>
    /// 异步关闭调用次数
    /// </summary>
    public int ShutdownAsyncCallCount { get; private set; }

    /// <summary>
    /// 释放调用次数
    /// </summary>
    public int DisposeCallCount { get; private set; }

    /// <summary>
    /// 设置服务提供器
    /// </summary>
    /// <param name="serviceProvider">服务提供器</param>
    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// 同步初始化
    /// </summary>
    /// <param name="serviceProvider">服务提供器</param>
    public void Initialize(IServiceProvider serviceProvider)
    {
        InitializeCallCount++;
        InitializedServiceProvider = serviceProvider;
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// 异步初始化
    /// </summary>
    /// <param name="serviceProvider">服务提供器</param>
    /// <returns>已完成的任务</returns>
    public Task InitializeAsync(IServiceProvider serviceProvider)
    {
        InitializeAsyncCallCount++;
        InitializedServiceProvider = serviceProvider;
        ServiceProvider = serviceProvider;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    /// <returns>已完成的任务</returns>
    public Task ConfigureServicesAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 同步关闭
    /// </summary>
    public void Shutdown()
    {
        ShutdownCallCount++;
    }

    /// <summary>
    /// 异步关闭
    /// </summary>
    /// <returns>已完成的任务</returns>
    public Task ShutdownAsync()
    {
        ShutdownAsyncCallCount++;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        DisposeCallCount++;
    }
}
