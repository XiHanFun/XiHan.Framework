#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ObjectExtensionPropertyInfoExtensions
// Guid:ec90bb06-7c75-4b3b-ae7e-00ed4d0e247a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 7:11:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel.DataAnnotations;

namespace XiHan.Framework.ObjectExtending.Extensions;

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
