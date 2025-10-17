#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ObjectExtensionManager
// Guid:49f4143a-bae4-4424-85f1-6bbcbee1da2a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 6:13:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Collections.Immutable;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.ObjectMapping;

/// <summary>
/// 对象扩展管理器
/// 用于管理对象的动态扩展属性和配置信息的单例管理器
/// </summary>
public class ObjectExtensionManager
{
    /// <summary>
    /// 初始化对象扩展管理器的新实例
    /// </summary>
    protected internal ObjectExtensionManager()
    {
        ObjectsExtensions = new ConcurrentDictionary<Type, ObjectExtensionInfo>();
        Configuration = new ConcurrentDictionary<object, object>();
    }

    /// <summary>
    /// 获取对象扩展管理器的单例实例
    /// </summary>
    public static ObjectExtensionManager Instance { get; protected set; } = new ObjectExtensionManager();

    /// <summary>
    /// 配置字典
    /// 用于存储各种配置信息的线程安全字典
    /// </summary>
    public ConcurrentDictionary<object, object> Configuration { get; }

    /// <summary>
    /// 对象扩展信息字典
    /// 存储各种类型的扩展信息的线程安全字典
    /// </summary>
    protected ConcurrentDictionary<Type, ObjectExtensionInfo> ObjectsExtensions { get; }

    /// <summary>
    /// 为指定的泛型类型添加或更新扩展信息
    /// </summary>
    /// <typeparam name="TObject">要扩展的对象类型</typeparam>
    /// <param name="configureAction">配置扩展信息的操作，可选</param>
    /// <returns>当前管理器实例，支持链式调用</returns>
    public virtual ObjectExtensionManager AddOrUpdate<TObject>(Action<ObjectExtensionInfo>? configureAction = null)
    {
        return AddOrUpdate(typeof(TObject), configureAction);
    }

    /// <summary>
    /// 为指定的多个类型批量添加或更新扩展信息
    /// </summary>
    /// <param name="types">要扩展的类型数组</param>
    /// <param name="configureAction">配置扩展信息的操作，可选</param>
    /// <returns>当前管理器实例，支持链式调用</returns>
    /// <exception cref="ArgumentNullException">当 types 为 null 时</exception>
    public virtual ObjectExtensionManager AddOrUpdate(Type[] types, Action<ObjectExtensionInfo>? configureAction = null)
    {
        ArgumentNullException.ThrowIfNull(types);

        foreach (var type in types)
        {
            AddOrUpdate(type, configureAction);
        }

        return this;
    }

    /// <summary>
    /// 为指定类型添加或更新扩展信息
    /// </summary>
    /// <param name="type">要扩展的类型</param>
    /// <param name="configureAction">配置扩展信息的操作，可选</param>
    /// <returns>当前管理器实例，支持链式调用</returns>
    /// <exception cref="ArgumentNullException">当 type 为 null 时</exception>
    public virtual ObjectExtensionManager AddOrUpdate(Type type, Action<ObjectExtensionInfo>? configureAction = null)
    {
        ArgumentNullException.ThrowIfNull(type);

        var extensionInfo = ObjectsExtensions.GetOrAdd(type, _ => new ObjectExtensionInfo(type));

        configureAction?.Invoke(extensionInfo);

        return this;
    }

    /// <summary>
    /// 获取指定泛型类型的扩展信息
    /// </summary>
    /// <typeparam name="TObject">要获取扩展信息的对象类型</typeparam>
    /// <returns>扩展信息对象，如果不存在则返回 null</returns>
    public virtual ObjectExtensionInfo? GetOrNull<TObject>()
    {
        return GetOrNull(typeof(TObject));
    }

    /// <summary>
    /// 获取指定类型的扩展信息
    /// </summary>
    /// <param name="type">要获取扩展信息的类型</param>
    /// <returns>扩展信息对象，如果不存在则返回 null</returns>
    /// <exception cref="ArgumentNullException">当 type 为 null 时</exception>
    public virtual ObjectExtensionInfo? GetOrNull(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return ObjectsExtensions.GetOrDefault(type);
    }

    /// <summary>
    /// 获取所有已扩展对象的信息集合
    /// </summary>
    /// <returns>包含所有扩展信息的不可变列表</returns>
    public virtual ImmutableList<ObjectExtensionInfo> GetExtendedObjects()
    {
        return [.. ObjectsExtensions.Values];
    }
}
