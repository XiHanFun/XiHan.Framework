#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ObjectExtensionInfo
// Guid:19675b1c-fd73-494c-b3b7-e94994125a65
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 6:14:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Collections.Immutable;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.ObjectMapping;

/// <summary>
/// 对象扩展信息类
/// 管理特定类型对象的扩展属性、配置和验证器信息
/// 提供线程安全的属性管理和查询功能
/// </summary>
public class ObjectExtensionInfo
{
    /// <summary>
    /// 初始化 ObjectExtensionInfo 类的新实例
    /// </summary>
    /// <param name="type">对象类型</param>
    /// <exception cref="ArgumentNullException">当 type 为 null 时</exception>
    public ObjectExtensionInfo(Type type)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Properties = new ConcurrentDictionary<string, ObjectExtensionPropertyInfo>();
        Configuration = new ConcurrentDictionary<object, object>();
        Validators = [];
    }

    /// <summary>
    /// 获取对象类型
    /// </summary>
    /// <value>此扩展信息所属的对象类型</value>
    public Type Type { get; }

    /// <summary>
    /// 获取配置字典
    /// 用于存储与此对象类型相关的任意配置信息
    /// </summary>
    /// <value>线程安全的配置字典</value>
    public ConcurrentDictionary<object, object> Configuration { get; }

    /// <summary>
    /// 获取对象级验证器集合
    /// 包含对整个对象进行验证的委托方法列表
    /// </summary>
    /// <value>对象验证器委托的可变列表</value>
    public List<Action<ObjectExtensionValidationContext>> Validators { get; }

    /// <summary>
    /// 获取属性字典
    /// 存储此对象类型的所有扩展属性信息
    /// </summary>
    /// <value>线程安全的属性字典，键为属性名称，值为属性信息</value>
    protected ConcurrentDictionary<string, ObjectExtensionPropertyInfo> Properties { get; }

    /// <summary>
    /// 检查是否存在指定名称的扩展属性
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    /// <returns>如果属性存在返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当 propertyName 为 null 时</exception>
    public virtual bool HasProperty(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        return Properties.ContainsKey(propertyName);
    }

    /// <summary>
    /// 添加或更新指定类型的扩展属性
    /// </summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="propertyName">属性名称</param>
    /// <param name="configureAction">属性配置委托，可选</param>
    /// <returns>当前 ObjectExtensionInfo 实例，支持链式调用</returns>
    /// <exception cref="ArgumentNullException">当 propertyName 为 null 时</exception>
    public virtual ObjectExtensionInfo AddOrUpdateProperty<TProperty>(
        string propertyName,
        Action<ObjectExtensionPropertyInfo>? configureAction = null)
    {
        return AddOrUpdateProperty(
            typeof(TProperty),
            propertyName,
            configureAction
        );
    }

    /// <summary>
    /// 添加或更新指定类型和名称的扩展属性
    /// </summary>
    /// <param name="propertyType">属性类型</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="configureAction">属性配置委托，可选</param>
    /// <returns>当前 ObjectExtensionInfo 实例，支持链式调用</returns>
    /// <exception cref="ArgumentNullException">当 propertyType 或 propertyName 为 null 时</exception>
    public virtual ObjectExtensionInfo AddOrUpdateProperty(
        Type propertyType,
        string propertyName,
        Action<ObjectExtensionPropertyInfo>? configureAction = null)
    {
        ArgumentNullException.ThrowIfNull(propertyType);
        ArgumentNullException.ThrowIfNull(propertyName);

        var propertyInfo = Properties.GetOrAdd(
            propertyName,
            _ => new ObjectExtensionPropertyInfo(this, propertyType, propertyName)
        );

        configureAction?.Invoke(propertyInfo);

        return this;
    }

    /// <summary>
    /// 获取所有扩展属性信息的不可变列表
    /// 按照 UI 显示顺序排序
    /// </summary>
    /// <returns>扩展属性信息的不可变列表</returns>
    public virtual ImmutableList<ObjectExtensionPropertyInfo> GetProperties()
    {
        return Properties.OrderBy(t => t.Value.Ui.Order).Select(t => t.Value)
                        .ToImmutableList();
    }

    /// <summary>
    /// 获取指定名称的扩展属性信息，如果不存在则返回 null
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    /// <returns>扩展属性信息，如果不存在则为 null</returns>
    /// <exception cref="ArgumentException">当 propertyName 为 null 或空字符串时</exception>
    public virtual ObjectExtensionPropertyInfo? GetPropertyOrNull(string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);

        return Properties.GetOrDefault(propertyName);
    }
}
