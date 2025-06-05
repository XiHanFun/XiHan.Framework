#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ObjectExtensionPropertyInfo
// Guid:7fe236ed-e403-48e8-9b46-13457c495761
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 6:54:27
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Localization;
using XiHan.Framework.ObjectExtending.Modularity;
using XiHan.Framework.Utils.Reflections;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.ObjectExtending;

/// <summary>
/// 对象扩展属性信息
/// </summary>
public class ObjectExtensionPropertyInfo : IHasNameWithLocalizableDisplayName, IBasicObjectExtensionPropertyInfo
{
    /// <summary>
    /// 初始化对象扩展属性信息
    /// </summary>
    /// <param name="objectExtension">对象扩展信息</param>
    /// <param name="type">属性类型</param>
    /// <param name="name">属性名称</param>
    public ObjectExtensionPropertyInfo([NotNull] ObjectExtensionInfo objectExtension, [NotNull] Type type, [NotNull] string name)
    {
        ObjectExtension = Guard.NotNull(objectExtension, nameof(objectExtension));
        Type = Guard.NotNull(type, nameof(type));
        Name = Guard.NotNull(name, nameof(name));

        Configuration = [];
        Attributes = [];
        Validators = [];

        Attributes.AddRange(ExtensionPropertyHelper.GetDefaultAttributes(Type));
        DefaultValue = TypeHelper.GetDefaultValue(Type);
        Lookup = new ExtensionPropertyLookupConfiguration();
        UI = new ExtensionPropertyUI();
        Policy = new ExtensionPropertyPolicyConfiguration();
    }

    /// <summary>
    /// 对象扩展信息
    /// </summary>
    [NotNull]
    public ObjectExtensionInfo ObjectExtension { get; }

    /// <summary>
    /// 属性名称
    /// </summary>
    [NotNull]
    public string Name { get; }

    /// <summary>
    /// 属性类型
    /// </summary>
    [NotNull]
    public Type Type { get; }

    /// <summary>
    /// 属性特性集合
    /// </summary>
    [NotNull]
    public List<Attribute> Attributes { get; }

    /// <summary>
    /// 验证器集合
    /// </summary>
    [NotNull]
    public List<Action<ObjectExtensionPropertyValidationContext>> Validators { get; }

    /// <summary>
    /// 显示名称（可本地化）
    /// </summary>
    public ILocalizableString? DisplayName { get; set; }

    /// <summary>
    /// 指示在对象映射时是否检查另一方是否明确定义了该属性
    /// 此属性用于：
    ///
    /// * .MapExtraPropertiesTo() 扩展方法
    /// * AutoMapper 的 .MapExtraProperties() 配置
    ///
    /// 如果为 true，这些方法会检查映射对象是否使用 <see cref="ObjectExtensionManager"/> 定义了该属性
    ///
    /// 默认值: null（未指定，使用默认逻辑）
    /// </summary>
    public bool? CheckPairDefinitionOnMapping { get; set; }

    /// <summary>
    /// 配置字典
    /// </summary>
    [NotNull]
    public Dictionary<object, object> Configuration { get; }

    /// <summary>
    /// 如果未设置 <see cref="DefaultValueFactory"/>，则用作默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 优先用于创建属性默认值的工厂方法
    /// 如果未设置，则使用 <see cref="DefaultValue"/>
    /// </summary>
    public Func<object>? DefaultValueFactory { get; set; }

    /// <summary>
    /// 查找配置
    /// </summary>
    [NotNull]
    public ExtensionPropertyLookupConfiguration Lookup { get; set; }

    /// <summary>
    /// 用户界面配置
    /// </summary>
    public ExtensionPropertyUI UI { get; set; }

    /// <summary>
    /// 策略配置
    /// </summary>
    public ExtensionPropertyPolicyConfiguration Policy { get; set; }

    /// <summary>
    /// 获取默认值
    /// </summary>
    /// <returns>属性的默认值</returns>
    public object? GetDefaultValue()
    {
        return ExtensionPropertyHelper.GetDefaultValue(Type, DefaultValueFactory, DefaultValue);
    }

    /// <summary>
    /// 扩展属性用户界面配置
    /// </summary>
    public class ExtensionPropertyUI
    {
        /// <summary>
        /// 初始化扩展属性用户界面配置
        /// </summary>
        public ExtensionPropertyUI()
        {
            EditModal = new ExtensionPropertyUIEditModal();
        }

        /// <summary>
        /// 显示顺序
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// 编辑模态框配置
        /// </summary>
        public ExtensionPropertyUIEditModal EditModal { get; set; }
    }

    /// <summary>
    /// 扩展属性用户界面编辑模态框配置
    /// </summary>
    public class ExtensionPropertyUIEditModal
    {
        /// <summary>
        /// 是否只读
        /// </summary>
        public bool IsReadOnly { get; set; }
    }
}
