#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FieldInfoExtensions
// Guid:5ccaac6d-e8fc-4239-875d-790c8de00882
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/03/17 19:05:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
