// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace XiHan.Framework.ObjectMapping.Extensions;

/// <summary>
/// 对象扩展属性信息扩展方法
/// </summary>
public static class ObjectExtensionPropertyInfoExtensions
{
    /// <summary>
    /// 获取对象扩展属性信息的验证特性
    /// </summary>
    /// <param name="propertyInfo"></param>
    /// <returns></returns>
    public static ValidationAttribute[] GetValidationAttributes(this ObjectExtensionPropertyInfo propertyInfo)
    {
        return [.. propertyInfo.Attributes.OfType<ValidationAttribute>()];
    }
}
