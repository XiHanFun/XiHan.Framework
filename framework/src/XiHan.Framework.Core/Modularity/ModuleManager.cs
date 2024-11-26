﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ModuleManager
// Guid:7d41bac7-c563-45b4-a979-e6be82310166
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2024-04-24 下午 02:43:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块管理器
/// </summary>
public class ModuleManager : IModuleManager, ISingletonDependency
{
    private readonly IModuleContainer _moduleContainer;
    private readonly IEnumerable<IModuleLifecycleContributor> _lifecycleContributors;
    private readonly ILogger<ModuleManager> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="moduleContainer"></param>
    /// <param name="logger"></param>
    /// <param name="options"></param>
    /// <param name="serviceProvider"></param>
    public ModuleManager(IModuleContainer moduleContainer, ILogger<ModuleManager> logger,
        IOptions<XiHanModuleLifecycleOptions> options, IServiceProvider serviceProvider)
    {
        _moduleContainer = moduleContainer;
        _logger = logger;

        _lifecycleContributors = options.Value.Contributors
            .Select(serviceProvider.GetRequiredService)
            .Cast<IModuleLifecycleContributor>()
            .ToArray();
    }

    /// <summary>
    /// 初始化模块，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual async Task InitializeModulesAsync([NotNull] ApplicationInitializationContext context)
    {
        foreach (var contributor in _lifecycleContributors)
        {
            foreach (var module in _moduleContainer.Modules)
            {
                try
                {
                    await contributor.InitializeAsync(context, module.Instance);
                }
                catch (Exception ex)
                {
                    throw new InitializationException($"在模块 {module.Type.AssemblyQualifiedName} 的初始化 {contributor.GetType().FullName} 阶段发生错误：{ex.Message}。查看集成异常以获取详细信息。", ex);
                }
            }
        }

        _logger.LogInformation("已初始化所有模块。");
    }

    /// <summary>
    /// 初始化模块
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public void InitializeModules([NotNull] ApplicationInitializationContext context)
    {
        foreach (var contributor in _lifecycleContributors)
        {
            foreach (var module in _moduleContainer.Modules)
            {
                try
                {
                    _ = contributor.InitializeAsync(context, module.Instance);
                }
                catch (Exception ex)
                {
                    throw new InitializationException($"在模块 {module.Type.AssemblyQualifiedName} 的初始化 {contributor.GetType().FullName} 阶段发生错误：{ex.Message}。查看集成异常以获取详细信息。", ex);
                }
            }
        }

        _logger.LogInformation("已初始化所有模块。");
    }

    /// <summary>
    /// 关闭模块，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual async Task ShutdownModulesAsync([NotNull] ApplicationShutdownContext context)
    {
        var modules = _moduleContainer.Modules.Reverse().ToList();

        foreach (var contributor in _lifecycleContributors)
        {
            foreach (var module in modules)
            {
                try
                {
                    await contributor.ShutdownAsync(context, module.Instance);
                }
                catch (Exception ex)
                {
                    throw new ShutdownException($"在模块 {module.Type.AssemblyQualifiedName} 的关闭 {contributor.GetType().FullName} 阶段发生错误：{ex.Message}。查看集成异常以获取详细信息。", ex);
                }
            }
        }
    }

    /// <summary>
    /// 关闭模块
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public void ShutdownModules([NotNull] ApplicationShutdownContext context)
    {
        var modules = _moduleContainer.Modules.Reverse().ToList();

        foreach (var contributor in _lifecycleContributors)
        {
            foreach (var module in modules)
            {
                try
                {
                    _ = contributor.ShutdownAsync(context, module.Instance);
                }
                catch (Exception ex)
                {
                    throw new ShutdownException($"在模块 {module.Type.AssemblyQualifiedName} 的关闭 {contributor.GetType().FullName} 阶段发生错误：{ex.Message}。查看集成异常以获取详细信息。", ex);
                }
            }
        }
    }
}