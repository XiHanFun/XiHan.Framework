// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Localization.Abstractions.Enums;

/// <summary>
/// 枚举本地化查询参数
/// </summary>
public sealed class EnumLocalizationQuery
{
    /// <summary>
    /// 目标文化名称，未指定时使用当前 UI 文化
    /// </summary>
    public string? CultureName { get; set; }

    /// <summary>
    /// 是否包含隐藏项
    /// </summary>
    public bool IncludeHidden { get; set; }

    /// <summary>
    /// 是否按顺序返回
    /// </summary>
    public bool Ordered { get; set; } = true;
}

/// <summary>
/// 枚举项本地化描述
/// </summary>
public sealed class LocalizedEnumItem
{
    /// <summary>
    /// 枚举字段名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 枚举原始值
    /// </summary>
    public object Value { get; set; } = default!;

    /// <summary>
    /// 枚举原始值字符串
    /// </summary>
    public string ValueText { get; set; } = string.Empty;

    /// <summary>
    /// 显示标签（优先本地化）
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 原始描述（来自特性/注释映射）
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 主题
    /// </summary>
    public string? Theme { get; set; }

    /// <summary>
    /// 图标
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 排序
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
    /// 本地化资源名
    /// </summary>
    public string? ResourceName { get; set; }

    /// <summary>
    /// 本地化键
    /// </summary>
    public string? LocalizationKey { get; set; }

    /// <summary>
    /// 扩展字段
    /// </summary>
    public IReadOnlyDictionary<string, object>? Extra { get; set; }
}

/// <summary>
/// 枚举本地化描述
/// </summary>
public sealed class LocalizedEnumDefinition
{
    /// <summary>
    /// 枚举短名称
    /// </summary>
    public string EnumName { get; set; } = string.Empty;

    /// <summary>
    /// 枚举完整名称
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// 枚举显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 当前查询文化
    /// </summary>
    public string CultureName { get; set; } = string.Empty;

    /// <summary>
    /// 是否标记位枚举
    /// </summary>
    public bool IsFlags { get; set; }

    /// <summary>
    /// 底层类型名称
    /// </summary>
    public string UnderlyingTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 默认本地化资源名
    /// </summary>
    public string? ResourceName { get; set; }

    /// <summary>
    /// 枚举项列表
    /// </summary>
    public IReadOnlyList<LocalizedEnumItem> Items { get; set; } = [];
}
