#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:YamlSerializeOptions
// Guid:fcaba130-4544-4f11-8f55-712e0c2f68d2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 08:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Serialization.Yaml;

/// <summary>
/// YAML 序列化选项
/// </summary>
public class YamlSerializeOptions
{
    /// <summary>
    /// 创建默认选项
    /// </summary>
    /// <returns>默认序列化选项</returns>
    public static YamlSerializeOptions Default => new();

    /// <summary>
    /// 创建紧凑格式选项
    /// </summary>
    /// <returns>紧凑格式选项</returns>
    public static YamlSerializeOptions Compact => new()
    {
        IndentSize = 1,
        IncludeDocumentMarkers = false,
        ForceQuoteStrings = false,
        SortKeys = false,
        UseFlowStyle = true
    };

    /// <summary>
    /// 创建格式化选项（带注释、文档标记）
    /// </summary>
    /// <returns>格式化选项</returns>
    public static YamlSerializeOptions Formatted => new()
    {
        IndentSize = 4,
        IncludeDocumentMarkers = true,
        ForceQuoteStrings = false,
        SortKeys = true,
        UseFlowStyle = false,
        HeaderComment = "Generated YAML configuration"
    };

    /// <summary>
    /// 创建严格格式选项（所有字符串都加引号）
    /// </summary>
    /// <returns>严格格式选项</returns>
    public static YamlSerializeOptions Strict => new()
    {
        IndentSize = 2,
        IncludeDocumentMarkers = true,
        ForceQuoteStrings = true,
        SortKeys = true,
        UseFlowStyle = false
    };

    /// <summary>
    /// 缩进大小（空格数）
    /// </summary>
    public int IndentSize { get; set; } = 2;

    /// <summary>
    /// 是否包含文档标记（--- 和 ...）
    /// </summary>
    public bool IncludeDocumentMarkers { get; set; }

    /// <summary>
    /// 头部注释
    /// </summary>
    public string? HeaderComment { get; set; }

    /// <summary>
    /// 是否强制给字符串加引号
    /// </summary>
    public bool ForceQuoteStrings { get; set; }

    /// <summary>
    /// 是否按键排序
    /// </summary>
    public bool SortKeys { get; set; } = true;

    /// <summary>
    /// 最大行长度（超过则换行）
    /// </summary>
    public int MaxLineLength { get; set; } = 80;

    /// <summary>
    /// 是否使用流式样式（紧凑格式）
    /// </summary>
    public bool UseFlowStyle { get; set; }

    /// <summary>
    /// 数组项前缀
    /// </summary>
    public string ArrayPrefix { get; set; } = "- ";
}
