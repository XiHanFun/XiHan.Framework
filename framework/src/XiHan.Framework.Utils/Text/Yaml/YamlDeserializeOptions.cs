#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:YamlDeserializeOptions
// Guid:1124d7f4-06d9-47fa-bca3-8eb636b383e8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 8:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Text.Yaml;

/// <summary>
/// YAML 反序列化选项
/// </summary>
public class YamlDeserializeOptions
{
    /// <summary>
    /// 创建默认选项
    /// </summary>
    /// <returns>默认反序列化选项</returns>
    public static YamlDeserializeOptions Default => new();

    /// <summary>
    /// 创建严格模式选项
    /// </summary>
    /// <returns>严格模式选项</returns>
    public static YamlDeserializeOptions Strict => new()
    {
        IgnoreComments = false,
        ConvertTypes = true,
        AllowDuplicateKeys = false,
        CaseSensitive = true,
        IgnoreUnknownProperties = false,
        UseDefaultValues = false
    };

    /// <summary>
    /// 创建宽松模式选项
    /// </summary>
    /// <returns>宽松模式选项</returns>
    public static YamlDeserializeOptions Lenient => new()
    {
        IgnoreComments = true,
        ConvertTypes = false,
        AllowDuplicateKeys = true,
        CaseSensitive = false,
        IgnoreUnknownProperties = true,
        UseDefaultValues = true
    };

    /// <summary>
    /// 是否忽略注释
    /// </summary>
    public bool IgnoreComments { get; set; } = true;

    /// <summary>
    /// 是否转换数据类型
    /// </summary>
    public bool ConvertTypes { get; set; } = true;

    /// <summary>
    /// 键层级分隔符
    /// </summary>
    public string KeySeparator { get; set; } = ".";

    /// <summary>
    /// 是否允许重复键
    /// </summary>
    public bool AllowDuplicateKeys { get; set; }

    /// <summary>
    /// 是否大小写敏感
    /// </summary>
    public bool CaseSensitive { get; set; } = true;

    /// <summary>
    /// 最大嵌套深度
    /// </summary>
    public int MaxNestingDepth { get; set; } = 100;

    /// <summary>
    /// 是否忽略未知属性
    /// </summary>
    public bool IgnoreUnknownProperties { get; set; } = true;

    /// <summary>
    /// 是否使用默认值填充缺失属性
    /// </summary>
    public bool UseDefaultValues { get; set; } = true;
}
