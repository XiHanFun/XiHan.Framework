#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumAttributes
// Guid:2ec8584d-9477-4dea-8762-a6a553b1c179
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
/// 枚举禁用特性
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class EnumDisabledAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="disabled">是否禁用</param>
    public EnumDisabledAttribute(bool disabled = true)
    {
        Disabled = disabled;
    }

    /// <summary>
    /// 是否禁用
    /// </summary>
    public bool Disabled { get; }
}

/// <summary>
/// 枚举图标特性
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class EnumIconAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="icon">图标</param>
    public EnumIconAttribute(string icon)
    {
        Icon = icon;
    }

    /// <summary>
    /// 图标
    /// </summary>
    public string Icon { get; }
}

/// <summary>
/// 枚举本地化资源特性
/// 可标注在枚举类型上作为默认资源，也可标注在字段上作为字段级覆盖
/// </summary>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Field, AllowMultiple = false)]
public sealed class EnumLocalizationResourceAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="resourceName">本地化资源名</param>
    public EnumLocalizationResourceAttribute(string resourceName)
    {
        ResourceName = resourceName;
    }

    /// <summary>
    /// 本地化资源名
    /// </summary>
    public string ResourceName { get; }

    /// <summary>
    /// 本地化键前缀
    /// </summary>
    public string? KeyPrefix { get; set; }
}

/// <summary>
/// 枚举本地化键特性
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class EnumLocalizationKeyAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="localizationKey">本地化键</param>
    public EnumLocalizationKeyAttribute(string localizationKey)
    {
        LocalizationKey = localizationKey;
    }

    /// <summary>
    /// 本地化键
    /// </summary>
    public string LocalizationKey { get; }
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
    /// 是否禁用
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// 图标
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 本地化键
    /// </summary>
    public string? LocalizationKey { get; set; }

    /// <summary>
    /// 本地化资源名
    /// </summary>
    public string? ResourceName { get; set; }

    /// <summary>
    /// 扩展属性
    /// </summary>
    public Dictionary<string, object>? Extra { get; set; }
}
