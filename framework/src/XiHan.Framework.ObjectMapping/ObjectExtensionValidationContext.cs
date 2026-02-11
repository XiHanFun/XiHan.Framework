#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ObjectExtensionValidationContext
// Guid:e31dcf0f-d466-4706-a805-9abb476e6eef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 06:14:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.ObjectMapping.Extensions.Data;
using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.ObjectMapping;

/// <summary>
/// 对象扩展验证上下文类
/// 提供对象扩展属性验证过程中所需的上下文信息和验证结果收集功能
/// </summary>
public class ObjectExtensionValidationContext
{
    /// <summary>
    /// 初始化对象扩展验证上下文的新实例
    /// </summary>
    /// <param name="objectExtensionInfo">对象扩展信息</param>
    /// <param name="validatingObject">正在验证的对象</param>
    /// <param name="validationErrors">验证错误集合</param>
    /// <param name="validationContext">验证上下文</param>
    /// <exception cref="ArgumentNullException">当任一参数为 null 时</exception>
    public ObjectExtensionValidationContext(ObjectExtensionInfo objectExtensionInfo, IHasExtraProperties validatingObject,
        List<ValidationResult> validationErrors, ValidationContext validationContext)
    {
        ObjectExtensionInfo = Guard.NotNull(objectExtensionInfo, nameof(objectExtensionInfo));
        ValidatingObject = Guard.NotNull(validatingObject, nameof(validatingObject));
        ValidationErrors = Guard.NotNull(validationErrors, nameof(validationErrors));
        ValidationContext = Guard.NotNull(validationContext, nameof(validationContext));
    }

    /// <summary>
    /// 获取相关的对象扩展信息
    /// 包含对象类型的扩展属性定义和配置信息
    /// </summary>
    public ObjectExtensionInfo ObjectExtensionInfo { get; }

    /// <summary>
    /// 获取正在验证的对象引用
    /// 该对象实现了 IHasExtraProperties 接口，具有额外属性
    /// </summary>
    public IHasExtraProperties ValidatingObject { get; }

    /// <summary>
    /// 获取验证错误集合
    /// 验证过程中发现的错误将添加到此列表中
    /// </summary>
    public List<ValidationResult> ValidationErrors { get; }

    /// <summary>
    /// 获取验证上下文
    /// 来自 IValidatableObject.Validate 方法的验证上下文信息
    /// </summary>
    public ValidationContext ValidationContext { get; }

    /// <summary>
    /// 获取服务提供者
    /// 可用于从依赖注入容器中解析服务实例
    /// </summary>
    public IServiceProvider? ServiceProvider => ValidationContext;
}
