#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XmlSerializationOptions
// Guid:93bf8224-ecd0-4554-beb8-af313aa2319a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 8:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.Text.Xml;

/// <summary>
/// XML 序列化选项
/// </summary>
public class XmlSerializationOptions
{
    /// <summary>
    /// 创建默认选项
    /// </summary>
    /// <returns>默认序列化选项</returns>
    public static XmlSerializationOptions Default => new();

    /// <summary>
    /// 创建紧凑格式选项（无缩进、无声明）
    /// </summary>
    /// <returns>紧凑格式选项</returns>
    public static XmlSerializationOptions Compact => new()
    {
        OmitXmlDeclaration = true,
        Indent = false,
        OmitNamespaces = true
    };

    /// <summary>
    /// 创建格式化选项（带缩进、带声明）
    /// </summary>
    /// <returns>格式化选项</returns>
    public static XmlSerializationOptions Formatted => new()
    {
        OmitXmlDeclaration = false,
        Indent = true,
        IndentChars = "    ", // 4个空格缩进
        OmitNamespaces = false
    };

    /// <summary>
    /// 是否省略 XML 声明
    /// </summary>
    public bool OmitXmlDeclaration { get; set; }

    /// <summary>
    /// 是否格式化输出（缩进）
    /// </summary>
    public bool Indent { get; set; } = true;

    /// <summary>
    /// 缩进字符
    /// </summary>
    public string IndentChars { get; set; } = "  ";

    /// <summary>
    /// 编码格式
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// 换行符
    /// </summary>
    public string NewLineChars { get; set; } = Environment.NewLine;

    /// <summary>
    /// 是否检查字符有效性
    /// </summary>
    public bool CheckCharacters { get; set; } = true;

    /// <summary>
    /// 是否省略命名空间
    /// </summary>
    public bool OmitNamespaces { get; set; } = true;

    /// <summary>
    /// 自定义命名空间映射
    /// </summary>
    public Dictionary<string, string>? CustomNamespaces { get; set; }
}
