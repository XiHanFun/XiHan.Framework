// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Serialization.Yaml;

/// <summary>
/// YAML 解析选项
/// </summary>
public class YamlParseOptions
{
    /// <summary>
    /// 创建默认选项
    /// </summary>
    /// <returns>默认解析选项</returns>
    public static YamlParseOptions Default => new();

    /// <summary>
    /// 创建严格模式选项
    /// </summary>
    /// <returns>严格模式选项</returns>
    public static YamlParseOptions Strict => new()
    {
        IgnoreComments = false,
        ConvertTypes = true,
        StrictMode = true,
        IgnoreEmptyLines = false
    };

    /// <summary>
    /// 创建宽松模式选项
    /// </summary>
    /// <returns>宽松模式选项</returns>
    public static YamlParseOptions Lenient => new()
    {
        IgnoreComments = true,
        ConvertTypes = false,
        StrictMode = false,
        IgnoreEmptyLines = true
    };

    /// <summary>
    /// 是否忽略注释
    /// </summary>
    public bool IgnoreComments { get; set; } = true;

    /// <summary>
    /// 是否转换数据类型（如 true/false 转为布尔值）
    /// </summary>
    public bool ConvertTypes { get; set; } = true;

    /// <summary>
    /// 键层级分隔符
    /// </summary>
    public string KeySeparator { get; set; } = ".";

    /// <summary>
    /// 是否忽略空行
    /// </summary>
    public bool IgnoreEmptyLines { get; set; } = true;

    /// <summary>
    /// 是否严格模式（严格按照 YAML 规范解析）
    /// </summary>
    public bool StrictMode { get; set; }

    /// <summary>
    /// 最大嵌套深度
    /// </summary>
    public int MaxNestingDepth { get; set; } = 100;
}
