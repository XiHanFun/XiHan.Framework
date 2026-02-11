#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExtensibleObjectValidator
// Guid:9b0b2991-c3ca-47a4-8603-e7944e4a28b2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 06:41:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Core.DynamicProxy;
using XiHan.Framework.ObjectMapping.Extensions;
using XiHan.Framework.ObjectMapping.Extensions.Data;
using XiHan.Framework.Validation.Abstractions;

namespace XiHan.Framework.ObjectMapping;

/// <summary>
/// 可扩展对象验证器
/// 提供对实现了 IHasExtraProperties 接口的对象及其扩展属性进行验证的静态方法集合
/// </summary>
public static class ExtensibleObjectValidator
{
    /// <summary>
    /// 验证并保护指定属性的值，如果验证失败则抛出异常
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="value">属性值</param>
    /// <exception cref="ArgumentNullException">当 extensibleObject 或 propertyName 为 null 时</exception>
    /// <exception cref="XiHanValidationException">当验证失败时</exception>
    public static void GuardValue(
        IHasExtraProperties extensibleObject,
        string propertyName,
        object? value)
    {
        var validationErrors = GetValidationErrors(extensibleObject, propertyName, value);

        if (validationErrors.Count != 0)
        {
            throw new XiHanValidationException(validationErrors);
        }
    }

    /// <summary>
    /// 检查可扩展对象是否有效（通过验证）
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="objectValidationContext">对象验证上下文，可选</param>
    /// <returns>如果对象有效则返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当 extensibleObject 为 null 时</exception>
    public static bool IsValid(IHasExtraProperties extensibleObject, ValidationContext? objectValidationContext = null)
    {
        return GetValidationErrors(
            extensibleObject,
            objectValidationContext
        ).Count == 0;
    }

    /// <summary>
    /// 检查可扩展对象的指定属性值是否有效（通过验证）
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="value">属性值</param>
    /// <param name="objectValidationContext">对象验证上下文，可选</param>
    /// <returns>如果属性值有效则返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当 extensibleObject 或 propertyName 为 null 时</exception>
    public static bool IsValid(IHasExtraProperties extensibleObject, string propertyName,
        object? value, ValidationContext? objectValidationContext = null)
    {
        return GetValidationErrors(
            extensibleObject,
            propertyName,
            value,
            objectValidationContext
        ).Count == 0;
    }

    /// <summary>
    /// 获取可扩展对象的所有验证错误
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="objectValidationContext">对象验证上下文，可选</param>
    /// <returns>验证错误列表</returns>
    /// <exception cref="ArgumentNullException">当 extensibleObject 为 null 时</exception>
    public static List<ValidationResult> GetValidationErrors(IHasExtraProperties extensibleObject, ValidationContext? objectValidationContext = null)
    {
        var validationErrors = new List<ValidationResult>();

        AddValidationErrors(
            extensibleObject,
            validationErrors,
            objectValidationContext
        );

        return validationErrors;
    }

    /// <summary>
    /// 获取可扩展对象指定属性的验证错误
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="value">属性值</param>
    /// <param name="objectValidationContext">对象验证上下文，可选</param>
    /// <returns>验证错误列表</returns>
    /// <exception cref="ArgumentNullException">当 extensibleObject 或 propertyName 为 null 时</exception>
    public static List<ValidationResult> GetValidationErrors(IHasExtraProperties extensibleObject, string propertyName,
        object? value, ValidationContext? objectValidationContext = null)
    {
        var validationErrors = new List<ValidationResult>();

        AddValidationErrors(
            extensibleObject,
            validationErrors,
            propertyName,
            value,
            objectValidationContext
        );

        return validationErrors;
    }

    /// <summary>
    /// 将可扩展对象的验证错误添加到指定的错误集合中
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="validationErrors">验证错误集合</param>
    /// <param name="objectValidationContext">对象验证上下文，可选</param>
    /// <exception cref="ArgumentNullException">当 extensibleObject 或 validationErrors 为 null 时</exception>
    public static void AddValidationErrors(IHasExtraProperties extensibleObject, List<ValidationResult> validationErrors,
        ValidationContext? objectValidationContext = null)
    {
        ArgumentNullException.ThrowIfNull(extensibleObject);
        ArgumentNullException.ThrowIfNull(validationErrors);

        objectValidationContext ??= new ValidationContext(
                extensibleObject,
                null,
                new Dictionary<object, object?>()
            );

        var objectType = ProxyHelper.UnProxy(extensibleObject).GetType();

        var objectExtensionInfo = ObjectExtensionManager.Instance
            .GetOrNull(objectType);

        if (objectExtensionInfo == null)
        {
            return;
        }

        AddPropertyValidationErrors(
            extensibleObject,
            validationErrors,
            objectValidationContext,
            objectExtensionInfo
        );

        ExecuteCustomObjectValidationActions(
            extensibleObject,
            validationErrors,
            objectValidationContext,
            objectExtensionInfo
        );
    }

    /// <summary>
    /// 将可扩展对象指定属性的验证错误添加到指定的错误集合中
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="validationErrors">验证错误集合</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="value">属性值</param>
    /// <param name="objectValidationContext">对象验证上下文，可选</param>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时</exception>
    /// <exception cref="ArgumentException">当 propertyName 为空或仅包含空白字符时</exception>
    public static void AddValidationErrors(IHasExtraProperties extensibleObject, List<ValidationResult> validationErrors,
        string propertyName, object? value, ValidationContext? objectValidationContext = null)
    {
        ArgumentNullException.ThrowIfNull(extensibleObject);
        ArgumentNullException.ThrowIfNull(validationErrors);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        objectValidationContext ??= new ValidationContext(extensibleObject, null, new Dictionary<object, object?>());

        var objectType = ProxyHelper.UnProxy(extensibleObject).GetType();

        var objectExtensionInfo = ObjectExtensionManager.Instance
            .GetOrNull(objectType);

        if (objectExtensionInfo == null)
        {
            return;
        }

        var property = objectExtensionInfo.GetPropertyOrNull(propertyName);
        if (property == null)
        {
            return;
        }

        AddPropertyValidationErrors(
            extensibleObject,
            validationErrors,
            objectValidationContext,
            property,
            value
        );
    }

    /// <summary>
    /// 验证并检查指定属性的值，这是 GuardValue 方法的别名
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="value">属性值</param>
    /// <exception cref="ArgumentNullException">当 extensibleObject 或 propertyName 为 null 时</exception>
    /// <exception cref="XiHanValidationException">当验证失败时</exception>
    public static void CheckValue(IHasExtraProperties extensibleObject, string propertyName, object? value)
    {
        GuardValue(extensibleObject, propertyName, value);
    }

    /// <summary>
    /// 为对象扩展信息中的所有属性添加验证错误
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="validationErrors">验证错误集合</param>
    /// <param name="objectValidationContext">对象验证上下文</param>
    /// <param name="objectExtensionInfo">对象扩展信息</param>
    private static void AddPropertyValidationErrors(IHasExtraProperties extensibleObject, List<ValidationResult> validationErrors,
        ValidationContext objectValidationContext, ObjectExtensionInfo objectExtensionInfo)
    {
        var properties = objectExtensionInfo.GetProperties();
        if (!properties.Any())
        {
            return;
        }

        foreach (var property in properties)
        {
            AddPropertyValidationErrors(
                extensibleObject,
                validationErrors,
                objectValidationContext,
                property,
                extensibleObject.GetProperty(property.Name)
            );
        }
    }

    /// <summary>
    /// 为指定的扩展属性添加验证错误
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="validationErrors">验证错误集合</param>
    /// <param name="objectValidationContext">对象验证上下文</param>
    /// <param name="property">扩展属性信息</param>
    /// <param name="value">属性值</param>
    private static void AddPropertyValidationErrors(IHasExtraProperties extensibleObject, List<ValidationResult> validationErrors,
        ValidationContext objectValidationContext, ObjectExtensionPropertyInfo property, object? value)
    {
        AddPropertyValidationAttributeErrors(
            extensibleObject,
            validationErrors,
            objectValidationContext,
            property,
            value
        );

        ExecuteCustomPropertyValidationActions(
            extensibleObject,
            validationErrors,
            objectValidationContext,
            property,
            value
        );
    }

    /// <summary>
    /// 使用验证特性为属性添加验证错误
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="validationErrors">验证错误集合</param>
    /// <param name="objectValidationContext">对象验证上下文</param>
    /// <param name="property">扩展属性信息</param>
    /// <param name="value">属性值</param>
    private static void AddPropertyValidationAttributeErrors(IHasExtraProperties extensibleObject, List<ValidationResult> validationErrors,
        ValidationContext objectValidationContext, ObjectExtensionPropertyInfo property, object? value)
    {
        var validationAttributes = property.GetValidationAttributes();

        if (validationAttributes.Length == 0)
        {
            return;
        }

        var propertyValidationContext = new ValidationContext(extensibleObject, objectValidationContext, null)
        {
            DisplayName = property.Name,
            MemberName = property.Name
        };

        foreach (var attribute in validationAttributes)
        {
            var result = attribute.GetValidationResult(
                value,
                propertyValidationContext
            );

            if (result != null)
            {
                validationErrors.Add(result);
            }
        }
    }

    /// <summary>
    /// 执行自定义属性验证操作
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="validationErrors">验证错误集合</param>
    /// <param name="objectValidationContext">对象验证上下文</param>
    /// <param name="property">扩展属性信息</param>
    /// <param name="value">属性值</param>
    private static void ExecuteCustomPropertyValidationActions(IHasExtraProperties extensibleObject, List<ValidationResult> validationErrors,
        ValidationContext objectValidationContext, ObjectExtensionPropertyInfo property, object? value)
    {
        if (property.Validators.Count == 0)
        {
            return;
        }

        var context = new ObjectExtensionPropertyValidationContext(
            property,
            extensibleObject,
            validationErrors,
            objectValidationContext,
            value
        );

        foreach (var validator in property.Validators)
        {
            validator(context);
        }
    }

    /// <summary>
    /// 执行自定义对象验证操作
    /// </summary>
    /// <param name="extensibleObject">可扩展对象实例</param>
    /// <param name="validationErrors">验证错误集合</param>
    /// <param name="objectValidationContext">对象验证上下文</param>
    /// <param name="objectExtensionInfo">对象扩展信息</param>
    private static void ExecuteCustomObjectValidationActions(IHasExtraProperties extensibleObject, List<ValidationResult> validationErrors,
        ValidationContext objectValidationContext, ObjectExtensionInfo objectExtensionInfo)
    {
        if (objectExtensionInfo.Validators.Count == 0)
        {
            return;
        }

        var context = new ObjectExtensionValidationContext(
            objectExtensionInfo,
            extensibleObject,
            validationErrors,
            objectValidationContext
        );

        foreach (var validator in objectExtensionInfo.Validators)
        {
            validator(context);
        }
    }
}
