#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumItem
// Guid:3c8b1e5f-2a4d-4e89-b9c1-7f8e6d5c4b3a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Enums;

/// <summary>
/// 枚举项模型
/// </summary>
public record EnumItem
{
    /// <summary>
    /// 枚举名称（字段名）
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 枚举值（数值）
    /// </summary>
    [JsonPropertyName("value")]
    public object Value { get; set; } = default!;

    /// <summary>
    /// 枚举描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 主题标识（用于前端样式等）
    /// </summary>
    [JsonPropertyName("theme")]
    public string? Theme { get; set; }

    /// <summary>
    /// 扩展属性
    /// </summary>
    [JsonPropertyName("extra")]
    public Dictionary<string, object>? Extra { get; set; }
}

/// <summary>
/// 枚举项模型（泛型版本）
/// </summary>
/// <typeparam name="T">枚举值类型</typeparam>
public record EnumItem<T> : EnumItem
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public EnumItem()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="key">枚举名称</param>
    /// <param name="value">枚举值</param>
    /// <param name="description">描述</param>
    /// <param name="theme">主题</param>
    public EnumItem(string key, T value, string description, string? theme = null)
    {
        Key = key;
        Value = value;
        base.Value = value!;
        Description = description;
        Theme = theme;
    }

    /// <summary>
    /// 强类型枚举值
    /// </summary>
    [JsonPropertyName("typedValue")]
    public new T Value { get; set; } = default!;
}