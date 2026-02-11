#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBasicObjectExtensionPropertyInfo
// Guid:2e6aaf42-aea7-40aa-b702-2fd2c2ca5342
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 06:31:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Localization.Abstractions;

namespace XiHan.Framework.ObjectMapping;

/// <summary>
/// 基础对象扩展属性信息接口
/// 定义对象扩展属性的基本信息和行为契约，包括属性名称、类型、特性、验证器等
/// </summary>
public interface IBasicObjectExtensionPropertyInfo
{
    /// <summary>
    /// 获取属性名称
    /// </summary>
    /// <value>属性的唯一标识名称</value>
    public string Name { get; }

    /// <summary>
    /// 获取属性类型
    /// </summary>
    /// <value>属性的 .NET 类型信息</value>
    public Type Type { get; }

    /// <summary>
    /// 获取属性的特性集合
    /// 包含验证特性、显示特性等用于描述属性行为的元数据
    /// </summary>
    /// <value>特性对象的可变列表</value>
    public List<Attribute> Attributes { get; }

    /// <summary>
    /// 获取属性的验证器集合
    /// 包含自定义验证逻辑的委托方法列表
    /// </summary>
    /// <value>验证器委托的可变列表</value>
    public List<Action<ObjectExtensionPropertyValidationContext>> Validators { get; }

    /// <summary>
    /// 获取或设置属性的可本地化显示名称
    /// 用于在用户界面中显示的友好名称，支持多语言
    /// </summary>
    /// <value>可本地化字符串对象，如果未设置则为 null</value>
    public ILocalizableString? DisplayName { get; }

    /// <summary>
    /// 获取或设置属性的默认值
    /// 当 <see cref="DefaultValueFactory"/> 未设置时使用此值作为默认值
    /// </summary>
    /// <value>属性的默认值，可以为 null</value>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 获取或设置默认值工厂方法
    /// 具有最高优先级，用于动态创建属性的默认值
    /// 如果未设置，则使用 <see cref="DefaultValue"/> 作为默认值
    /// </summary>
    /// <value>返回默认值的工厂方法，如果未设置则为 null</value>
    public Func<object>? DefaultValueFactory { get; set; }
}
