#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XmlDeserializationOptions
// Guid:984689b5-7ba5-4a1a-b85e-07aadcd06c72
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 8:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Serialization.Xml;

/// <summary>
/// XML 反序列化选项
/// </summary>
public class XmlDeserializationOptions
{
    /// <summary>
    /// 创建默认选项
    /// </summary>
    /// <returns>默认反序列化选项</returns>
    public static XmlDeserializationOptions Default => new();

    /// <summary>
    /// 创建严格模式选项（不忽略任何内容，启用验证）
    /// </summary>
    /// <returns>严格模式选项</returns>
    public static XmlDeserializationOptions Strict => new()
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
    public static XmlDeserializationOptions Lenient => new()
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
