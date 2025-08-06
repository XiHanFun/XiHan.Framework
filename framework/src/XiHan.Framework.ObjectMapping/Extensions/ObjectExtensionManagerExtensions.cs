#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ObjectExtensionManagerExtensions
// Guid:a3cd4c52-1913-4dde-a112-4b435c35c6b0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 7:36:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using XiHan.Framework.ObjectMapping;
using XiHan.Framework.ObjectMapping.Data;

namespace XiHan.Framework.ObjectMapping.Extensions;

/// <summary>
/// 对象扩展管理器扩展方法类
/// 提供对 ObjectExtensionManager 的便捷扩展方法，简化对象扩展属性的管理和操作
/// </summary>
public static class ObjectExtensionManagerExtensions
{
    /// <summary>
    /// 空属性列表的不可变集合，用于优化性能避免重复创建
    /// </summary>
    private static readonly ImmutableList<ObjectExtensionPropertyInfo> EmptyPropertyList = new List<ObjectExtensionPropertyInfo>().ToImmutableList();

    /// <summary>
    /// 为多个对象类型添加或更新指定类型的属性
    /// </summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="objectExtensionManager">对象扩展管理器实例</param>
    /// <param name="objectTypes">目标对象类型数组</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="configureAction">属性配置委托，可选</param>
    /// <returns>对象扩展管理器实例，支持链式调用</returns>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    public static ObjectExtensionManager AddOrUpdateProperty<TProperty>(this ObjectExtensionManager objectExtensionManager,
        Type[] objectTypes, string propertyName, Action<ObjectExtensionPropertyInfo>? configureAction = null)
    {
        return objectExtensionManager.AddOrUpdateProperty(
            objectTypes,
            typeof(TProperty),
            propertyName, configureAction
        );
    }

    /// <summary>
    /// 为指定的对象类型添加或更新指定类型的属性
    /// </summary>
    /// <typeparam name="TObject">实现 IHasExtraProperties 接口的对象类型</typeparam>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="objectExtensionManager">对象扩展管理器实例</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="configureAction">属性配置委托，可选</param>
    /// <returns>对象扩展管理器实例，支持链式调用</returns>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    public static ObjectExtensionManager AddOrUpdateProperty<TObject, TProperty>(this ObjectExtensionManager objectExtensionManager,
        string propertyName, Action<ObjectExtensionPropertyInfo>? configureAction = null)
        where TObject : IHasExtraProperties
    {
        return objectExtensionManager.AddOrUpdateProperty(
            typeof(TObject),
            typeof(TProperty),
            propertyName,
            configureAction
        );
    }

    /// <summary>
    /// 为多个对象类型添加或更新指定类型和名称的属性
    /// </summary>
    /// <param name="objectExtensionManager">对象扩展管理器实例</param>
    /// <param name="objectTypes">目标对象类型数组</param>
    /// <param name="propertyType">属性类型</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="configureAction">属性配置委托，可选</param>
    /// <returns>对象扩展管理器实例，支持链式调用</returns>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    public static ObjectExtensionManager AddOrUpdateProperty(this ObjectExtensionManager objectExtensionManager,
        Type[] objectTypes, Type propertyType,
        string propertyName, Action<ObjectExtensionPropertyInfo>? configureAction = null)
    {
        ArgumentNullException.ThrowIfNull(objectTypes);

        foreach (var objectType in objectTypes)
        {
            objectExtensionManager.AddOrUpdateProperty(
                objectType,
                propertyType,
                propertyName,
                configureAction
            );
        }

        return objectExtensionManager;
    }

    /// <summary>
    /// 为指定对象类型添加或更新指定类型和名称的属性
    /// </summary>
    /// <param name="objectExtensionManager">对象扩展管理器实例</param>
    /// <param name="objectType">目标对象类型</param>
    /// <param name="propertyType">属性类型</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="configureAction">属性配置委托，可选</param>
    /// <returns>对象扩展管理器实例，支持链式调用</returns>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    public static ObjectExtensionManager AddOrUpdateProperty(this ObjectExtensionManager objectExtensionManager,
        Type objectType, Type propertyType, string propertyName,
        Action<ObjectExtensionPropertyInfo>? configureAction = null)
    {
        ArgumentNullException.ThrowIfNull(objectExtensionManager);

        return objectExtensionManager.AddOrUpdate(
            objectType,
            options =>
            {
                options.AddOrUpdateProperty(
                    propertyType,
                    propertyName,
                    configureAction
                );
            });
    }

    /// <summary>
    /// 获取指定对象类型的扩展属性信息，如果不存在则返回 null
    /// </summary>
    /// <typeparam name="TObject">对象类型</typeparam>
    /// <param name="objectExtensionManager">对象扩展管理器实例</param>
    /// <param name="propertyName">属性名称</param>
    /// <returns>对象扩展属性信息，如果不存在则为 null</returns>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    public static ObjectExtensionPropertyInfo? GetPropertyOrNull<TObject>(this ObjectExtensionManager objectExtensionManager,
        string propertyName)
    {
        return objectExtensionManager.GetPropertyOrNull(
            typeof(TObject),
            propertyName
        );
    }

    /// <summary>
    /// 获取指定对象类型的扩展属性信息，如果不存在则返回 null
    /// </summary>
    /// <param name="objectExtensionManager">对象扩展管理器实例</param>
    /// <param name="objectType">对象类型</param>
    /// <param name="propertyName">属性名称</param>
    /// <returns>对象扩展属性信息，如果不存在则为 null</returns>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    public static ObjectExtensionPropertyInfo? GetPropertyOrNull(
        this ObjectExtensionManager objectExtensionManager,
        Type objectType, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(objectExtensionManager);
        ArgumentNullException.ThrowIfNull(objectType);
        ArgumentNullException.ThrowIfNull(propertyName);

        return objectExtensionManager
            .GetOrNull(objectType)?
            .GetPropertyOrNull(propertyName);
    }

    /// <summary>
    /// 获取指定对象类型的所有扩展属性信息列表
    /// </summary>
    /// <typeparam name="TObject">对象类型</typeparam>
    /// <param name="objectExtensionManager">对象扩展管理器实例</param>
    /// <returns>扩展属性信息的不可变列表</returns>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    public static ImmutableList<ObjectExtensionPropertyInfo> GetProperties<TObject>(
        this ObjectExtensionManager objectExtensionManager)
    {
        return objectExtensionManager.GetProperties(typeof(TObject));
    }

    /// <summary>
    /// 获取指定对象类型的所有扩展属性信息列表
    /// </summary>
    /// <param name="objectExtensionManager">对象扩展管理器实例</param>
    /// <param name="objectType">对象类型</param>
    /// <returns>扩展属性信息的不可变列表，如果没有扩展信息则返回空列表</returns>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    public static ImmutableList<ObjectExtensionPropertyInfo> GetProperties(
        this ObjectExtensionManager objectExtensionManager, Type objectType)
    {
        ArgumentNullException.ThrowIfNull(objectExtensionManager);
        ArgumentNullException.ThrowIfNull(objectType);

        var extensionInfo = objectExtensionManager.GetOrNull(objectType);
        return extensionInfo == null ? EmptyPropertyList : extensionInfo.GetProperties();
    }

    /// <summary>
    /// 异步获取指定对象类型的扩展属性并检查策略权限
    /// </summary>
    /// <typeparam name="TObject">对象类型</typeparam>
    /// <param name="objectExtensionManager">对象扩展管理器实例</param>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <returns>通过策略检查的扩展属性信息不可变列表</returns>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    public static Task<ImmutableList<ObjectExtensionPropertyInfo>> GetPropertiesAndCheckPolicyAsync<TObject>(
        this ObjectExtensionManager objectExtensionManager,
        IServiceProvider serviceProvider)
    {
        return objectExtensionManager.GetPropertiesAndCheckPolicyAsync(typeof(TObject), serviceProvider);
    }

    /// <summary>
    /// 异步获取指定对象类型的扩展属性并检查策略权限
    /// 只返回通过策略检查的属性信息
    /// </summary>
    /// <param name="objectExtensionManager">对象扩展管理器实例</param>
    /// <param name="objectType">对象类型</param>
    /// <param name="serviceProvider">服务提供程序，用于获取策略检查器</param>
    /// <returns>通过策略检查的扩展属性信息不可变列表</returns>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    public static async Task<ImmutableList<ObjectExtensionPropertyInfo>> GetPropertiesAndCheckPolicyAsync(
        this ObjectExtensionManager objectExtensionManager,
        Type objectType,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(objectExtensionManager);
        ArgumentNullException.ThrowIfNull(objectType);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var extensionPropertyPolicyConfigurationChecker = serviceProvider.GetRequiredService<ExtensionPropertyPolicyChecker>();
        var properties = new List<ObjectExtensionPropertyInfo>();
        foreach (var propertyInfo in objectExtensionManager.GetProperties(objectType))
        {
            if (await extensionPropertyPolicyConfigurationChecker.CheckPolicyAsync(propertyInfo.Policy))
            {
                properties.Add(propertyInfo);
            }
        }

        return properties.ToImmutableList();
    }
}
