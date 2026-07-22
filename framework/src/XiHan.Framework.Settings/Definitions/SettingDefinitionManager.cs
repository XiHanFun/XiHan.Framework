// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Settings.Options;

namespace XiHan.Framework.Settings.Definitions;

/// <summary>
/// 设置定义管理器实现
/// </summary>
/// <remarks>
/// 单例。首次访问时懒加载：实例化 <see cref="XiHanSettingOptions.DefinitionProviders"/> 收集到的每个
/// <see cref="ISettingDefinitionProvider"/>，依次调用其 <c>Define</c> 把定义汇总进一张只读表并缓存。
/// 这样只写一个 <see cref="ISettingDefinitionProvider"/> 就能被自动收纳，不必再手动调用
/// <c>SettingManager.AddDefinition</c>。
/// </remarks>
public class SettingDefinitionManager : ISettingDefinitionManager, ISingletonDependency
{
    private readonly IServiceProvider _serviceProvider;
    private readonly XiHanSettingOptions _options;
    private readonly Lazy<IReadOnlyDictionary<string, SettingDefinition>> _definitions;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="options">设置选项（含定义提供者类型列表）</param>
    public SettingDefinitionManager(IServiceProvider serviceProvider, IOptions<XiHanSettingOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _definitions = new Lazy<IReadOnlyDictionary<string, SettingDefinition>>(BuildDefinitions, isThreadSafe: true);
    }

    /// <inheritdoc />
    public SettingDefinition? GetOrNull(string name)
    {
        return _definitions.Value.GetValueOrDefault(name);
    }

    /// <inheritdoc />
    public IReadOnlyList<SettingDefinition> GetAll()
    {
        return [.. _definitions.Value.Values];
    }

    private IReadOnlyDictionary<string, SettingDefinition> BuildDefinitions()
    {
        var context = new SettingDefinitionContext();

        // 定义提供者可能带 scoped 依赖，开一个作用域来实例化
        using var scope = _serviceProvider.CreateScope();

        foreach (var providerType in _options.DefinitionProviders)
        {
            var provider = (ISettingDefinitionProvider)ActivatorUtilities.CreateInstance(scope.ServiceProvider, providerType);
            provider.Define(context);
        }

        return context.GetAll();
    }
}
