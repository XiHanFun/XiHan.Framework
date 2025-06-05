#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ObjectExtensionPropertyValidationContext
// Guid:07a6e88c-6106-4908-9a4b-aba9a9926fad
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 7:12:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.ObjectExtending;

/// <summary>
/// 对象扩展属性验证上下文类
/// 提供单个对象扩展属性验证过程中所需的上下文信息、属性值和验证结果收集功能
/// </summary>
public class ObjectExtensionPropertyValidationContext
{
    /// <summary>
    /// 初始化对象扩展属性验证上下文的新实例
    /// </summary>
    /// <param name="objectExtensionPropertyInfo">对象扩展属性信息</param>
    /// <param name="validatingObject">正在验证的对象</param>
    /// <param name="validationErrors">验证错误集合</param>
    /// <param name="validationContext">验证上下文</param>
    /// <param name="value">要验证的属性值</param>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    public ObjectExtensionPropertyValidationContext(
        [NotNull] ObjectExtensionPropertyInfo objectExtensionPropertyInfo, [NotNull] IHasExtraProperties validatingObject,
        [NotNull] List<ValidationResult> validationErrors, [NotNull] ValidationContext validationContext, object? value)
    {
        ExtensionPropertyInfo = Guard.NotNull(objectExtensionPropertyInfo, nameof(objectExtensionPropertyInfo));
        ValidatingObject = Guard.NotNull(validatingObject, nameof(validatingObject));
        ValidationErrors = Guard.NotNull(validationErrors, nameof(validationErrors));
        ValidationContext = Guard.NotNull(validationContext, nameof(validationContext));
        Value = value;
    }

    /// <summary>
    /// 获取相关的扩展属性信息
    /// 包含属性的定义、验证规则和配置信息
    /// </summary>
    [NotNull]
    public ObjectExtensionPropertyInfo ExtensionPropertyInfo { get; }

    /// <summary>
    /// 获取正在验证的对象引用
    /// 该对象实现了 IHasExtraProperties 接口，具有额外属性
    /// </summary>
    [NotNull]
    public IHasExtraProperties ValidatingObject { get; }

    /// <summary>
    /// 获取验证错误集合
    /// 验证过程中发现的错误将添加到此列表中
    /// </summary>
    [NotNull]
    public List<ValidationResult> ValidationErrors { get; }

    /// <summary>
    /// 获取验证上下文
    /// 来自 IValidatableObject.Validate 方法的验证上下文信息
    /// </summary>
    [NotNull]
    public ValidationContext ValidationContext { get; }

    /// <summary>
    /// 获取正在验证的属性值
    /// 这是 ValidatingObject 中正在验证的属性的当前值
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// 获取服务提供者
    /// 可用于从依赖注入容器中解析服务实例
    /// 当在对象上使用 SetProperty 方法时，此值可能为 null
    /// </summary>
    public IServiceProvider? ServiceProvider => ValidationContext;
}
