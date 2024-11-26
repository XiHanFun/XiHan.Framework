#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanApplicationWithExternalServiceProvider
// Guid:09e23e43-36dd-4863-ade2-d62705cc322c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 4:14:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 具有外部服务的曦寒应用提供器
/// </summary>
internal class XiHanApplicationWithExternalServiceProvider : XiHanApplicationBase, IXiHanApplicationWithExternalServiceProvider
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="startupModuleType"></param>
    /// <param name="services"></param>
    /// <param name="optionsAction"></param>
    public XiHanApplicationWithExternalServiceProvider(
        [NotNull] Type startupModuleType,
        [NotNull] IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction) : base(startupModuleType, services, optionsAction)
    {
        _ = services.AddSingleton<IXiHanApplicationWithExternalServiceProvider>(this);
    }

    /// <summary>
    /// 设置服务提供器，但不初始化模块
    /// </summary>
    void IXiHanApplicationWithExternalServiceProvider.SetServiceProvider([NotNull] IServiceProvider serviceProvider)
    {
        _ = CheckHelper.NotNull(serviceProvider, nameof(serviceProvider));

        if (ServiceProvider != null)
        {
            if (ServiceProvider != serviceProvider)
            {
                throw new Exception("服务提供器之前已设置为另一个服务提供器实例！");
            }

            return;
        }

        SetServiceProvider(serviceProvider);
    }

    /// <summary>
    /// 设置服务提供器并初始化所有模块，异步
    /// 如果之前调用过 <see cref="IXiHanApplicationWithExternalServiceProvider.SetServiceProvider"/>，则应将相同的 <paramref name="serviceProvider"/> 实例传递给此方法
    /// </summary>
    public async Task InitializeAsync([NotNull] IServiceProvider serviceProvider)
    {
        _ = CheckHelper.NotNull(serviceProvider, nameof(serviceProvider));

        SetServiceProvider(serviceProvider);

        await InitializeModulesAsync();
    }

    /// <summary>
    /// 设置服务提供器并初始化所有模块，异步
    /// 如果之前调用过 <see cref="IXiHanApplicationWithExternalServiceProvider.SetServiceProvider"/>，则应将相同的 <paramref name="serviceProvider"/> 实例传递给此方法
    /// </summary>
    public void Initialize([NotNull] IServiceProvider serviceProvider)
    {
        _ = CheckHelper.NotNull(serviceProvider, nameof(serviceProvider));

        SetServiceProvider(serviceProvider);

        InitializeModules();
    }

    /// <summary>
    /// 释放
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();

        if (ServiceProvider is IDisposable disposableServiceProvider)
        {
            disposableServiceProvider.Dispose();
        }
    }
}