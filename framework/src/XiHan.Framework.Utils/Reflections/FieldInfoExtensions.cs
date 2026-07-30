// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace XiHan.Framework.Utils.Reflections;

/// <summary>
/// 字段信息扩展方法
/// </summary>
public static class FieldInfoExtensions
{
    /// <summary>
    /// 获取字段描述特性的值
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    public static string GetDescriptionValue(this FieldInfo field)
    {
        var descValue = field.Name;
        if (field.GetCustomAttribute<DescriptionAttribute>(false) is DescriptionAttribute description)
        {
            descValue = description.Description;
        }
        return descValue;
    }

    /// <summary>
    /// 获取字段描述特性的值
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    public static string GetDisplayValue(this FieldInfo field)
    {
        var displayValue = field.Name;
        if (field.GetCustomAttribute<DisplayAttribute>(false) is DisplayAttribute display)
        {
            displayValue = display.Description ?? displayValue;
        }
        return displayValue;
    }
}
