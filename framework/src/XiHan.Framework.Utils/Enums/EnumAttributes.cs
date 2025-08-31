#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumAttributes
// Guid:7a6b5c4d-3e2f-4g1h-8i9j-0k1l2m3n4o5p
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;

namespace XiHan.Framework.Utils.Enums;

/// <summary>
/// 枚举主题特性
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class EnumThemeAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="theme">主题名称</param>
    public EnumThemeAttribute(string theme)
    {
        Theme = theme;
    }

    /// <summary>
    /// 主题名称
    /// </summary>
    public string Theme { get; }
}

/// <summary>
/// 枚举显示特性（包含描述和主题）
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class EnumDisplayAttribute : DescriptionAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="description">描述</param>
    public EnumDisplayAttribute(string description) : base(description)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="description">描述</param>
    /// <param name="theme">主题</param>
    public EnumDisplayAttribute(string description, string theme) : base(description)
    {
        Theme = theme;
    }

    /// <summary>
    /// 主题名称
    /// </summary>
    public string? Theme { get; set; }

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 是否隐藏
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// 扩展属性
    /// </summary>
    public Dictionary<string, object>? Extra { get; set; }
}

/// <summary>
/// 枚举排序特性
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class EnumOrderAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="order">排序顺序</param>
    public EnumOrderAttribute(int order)
    {
        Order = order;
    }

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int Order { get; }
}

/// <summary>
/// 枚举隐藏特性
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class EnumHiddenAttribute : Attribute
{
}

/// <summary>
/// 枚举扩展特性
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class EnumExtraAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    public EnumExtraAttribute(string key, object value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// 键
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 值
    /// </summary>
    public object Value { get; }
}