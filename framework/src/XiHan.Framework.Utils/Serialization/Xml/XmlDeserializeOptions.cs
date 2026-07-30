// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Serialization.Xml;

/// <summary>
/// XML 反序列化选项
/// </summary>
public class XmlDeserializeOptions
{
    /// <summary>
    /// 创建默认选项
    /// </summary>
    /// <returns>默认反序列化选项</returns>
    public static XmlDeserializeOptions Default => new();

    /// <summary>
    /// 创建严格模式选项（不忽略任何内容，启用验证）
    /// </summary>
    /// <returns>严格模式选项</returns>
    public static XmlDeserializeOptions Strict => new()
    {
        IgnoreWhitespace = false,
        IgnoreComments = false,
        IgnoreProcessingInstructions = false,
        ValidateXml = true,
        CheckCharacters = true
    };

    /// <summary>
    /// 创建宽松模式选项（忽略所有非必要内容）
    /// </summary>
    /// <returns>宽松模式选项</returns>
    public static XmlDeserializeOptions Lenient => new()
    {
        IgnoreWhitespace = true,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        ValidateXml = false,
        CheckCharacters = false
    };

    /// <summary>
    /// 是否忽略空白字符
    /// </summary>
    public bool IgnoreWhitespace { get; set; } = true;

    /// <summary>
    /// 是否忽略注释
    /// </summary>
    public bool IgnoreComments { get; set; } = true;

    /// <summary>
    /// 是否检查字符有效性
    /// </summary>
    public bool CheckCharacters { get; set; } = true;

    /// <summary>
    /// 是否忽略处理指令
    /// </summary>
    public bool IgnoreProcessingInstructions { get; set; } = true;

    /// <summary>
    /// 是否验证 XML 格式
    /// </summary>
    public bool ValidateXml { get; set; }

    /// <summary>
    /// 最大字符实体扩展数
    /// </summary>
    public long MaxCharactersInDocument { get; set; } = 0; // 0 表示无限制

    /// <summary>
    /// 最大字符实体扩展数
    /// </summary>
    public long MaxCharactersFromEntities { get; set; } = 0; // 0 表示无限制
}
