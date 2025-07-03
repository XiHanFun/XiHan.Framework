#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumInfo
// Guid:eb3bf937-4be6-405e-9885-4292db553217
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/17 3:30:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Themes;

namespace XiHan.Framework.Utils.Enums.Dtos;

/// <summary>
/// 枚举信息
/// </summary>
public record EnumInfo : ThemeColor
{
    /// <summary>
    /// 键（枚举名称）
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 值（枚举整数值）
    /// </summary>
    public int Value { get; init; }

    /// <summary>
    /// 标签（枚举描述）
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// 是否为默认值
    /// </summary>
    public bool IsDefault => Value == 0;

    /// <summary>
    /// 排序权重
    /// </summary>
    public int Order { get; init; } = 0;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 分组
    /// </summary>
    public string Group { get; init; } = string.Empty;

    /// <summary>
    /// 扩展属性
    /// </summary>
    public Dictionary<string, object> ExtensionData { get; init; } = [];

    /// <summary>
    /// 创建简单枚举信息
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <param name="label">标签</param>
    /// <returns>枚举信息</returns>
    public static EnumInfo Create(string key, int value, string label = "")
    {
        return new EnumInfo
        {
            Key = key,
            Value = value,
            Label = string.IsNullOrEmpty(label) ? key : label
        };
    }

    /// <summary>
    /// 创建带主题的枚举信息
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <param name="label">标签</param>
    /// <param name="theme">主题</param>
    /// <param name="color">颜色</param>
    /// <param name="icon">图标</param>
    /// <returns>枚举信息</returns>
    public static EnumInfo Create(string key, int value, string label, string theme, string color, string icon = "")
    {
        return new EnumInfo
        {
            Key = key,
            Value = value,
            Label = string.IsNullOrEmpty(label) ? key : label,
            Theme = theme,
            Color = color,
            Icon = icon
        };
    }

    /// <summary>
    /// 添加扩展数据
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <returns>当前实例</returns>
    public EnumInfo WithExtension(string key, object value)
    {
        ExtensionData[key] = value;
        return this;
    }

    /// <summary>
    /// 获取扩展数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>扩展数据</returns>
    public T GetExtension<T>(string key, T defaultValue = default!)
    {
        return ExtensionData.TryGetValue(key, out var value) && value is T result ? result : defaultValue;
    }

    /// <summary>
    /// 转换为选择项
    /// </summary>
    /// <returns>选择项</returns>
    public SelectItem ToSelectItem()
    {
        return new SelectItem
        {
            Key = Key,
            Value = Value.ToString(),
            Text = Label
        };
    }

    /// <summary>
    /// 转换为键值对
    /// </summary>
    /// <returns>键值对</returns>
    public KeyValuePair<string, int> ToKeyValuePair()
    {
        return new KeyValuePair<string, int>(Key, Value);
    }

    /// <summary>
    /// 转换为显示信息
    /// </summary>
    /// <returns>显示信息</returns>
    public string ToDisplayString()
    {
        return string.IsNullOrEmpty(Label) ? Key : Label;
    }

    /// <summary>
    /// 转换为完整信息字符串
    /// </summary>
    /// <returns>完整信息字符串</returns>
    public string ToFullString()
    {
        return $"{Key}({Value}): {Label}";
    }
}
