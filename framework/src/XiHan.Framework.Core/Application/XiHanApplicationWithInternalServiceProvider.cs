#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanApplicationWithInternalServiceProvider
// Guid:e1b0b1a1-6873-4ff3-83ea-fa6526918aed
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 4:10:53
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 具有集成服务的曦寒应用提供器
/// </summary>
public class XiHanApplicationWithInternalServiceProvider : XiHanApplicationBase, IXiHanApplicationWithInternalServiceProvider
{
    /// <summary>
    /// 作用域服务
    /// </summary>
    public IServiceScope? ServiceScope { get; private set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="startupModuleType"></param>
    /// <param name="optionsAction"></param>
    public XiHanApplicationWithInternalServiceProvider([NotNull] Type startupModuleType, Action<XiHanApplicationCreationOptions>? optionsAction)
        : this(startupModuleType, new ServiceCollection(), optionsAction)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="startupModuleType"></param>
    /// <param name="services"></param>
    /// <param name="optionsAction"></param>
    private XiHanApplicationWithInternalServiceProvider(
        [NotNull] Type startupModuleType,
        [NotNull] IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction) : base(startupModuleType, services, optionsAction)
    {
        Services.AddSingleton<IXiHanApplicationWithInternalServiceProvider>(this);
    }

    /// <summary>
    /// 创建服务提供器，但不初始化模块。
    /// 多次调用将返回相同的服务提供器，而不会再次创建
    /// </summary>
    public IServiceProvider CreateServiceProvider()
    {
        if (ServiceProvider != null)
        {
            return ServiceProvider;
        }

        ServiceScope = Services.BuildServiceProviderFromFactory().CreateScope();
        SetServiceProvider(ServiceScope.ServiceProvider);

        return ServiceProvider!;
    }

    /// <summary>
    /// 创建服务提供商并初始化所有模块，异步
    /// 如果之前调用过 <see cref="CreateServiceProvider"/> 方法，它不会重新创建，而是使用之前的那个
    /// </summary>
    public async Task InitializeAsync()
    {
        CreateServiceProvider();
        await InitializeModulesAsync();
    }

    /// <summary>
    /// 创建服务提供商并初始化所有模块，异步
    /// 如果之前调用过 <see cref="CreateServiceProvider"/> 方法，它不会重新创建，而是使用之前的那个
    /// </summary>
    public void Initialize()
    {
        CreateServiceProvider();
        InitializeModules();
    }

    /// <summary>
    /// 释放
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        ServiceScope?.Dispose();
    }
}