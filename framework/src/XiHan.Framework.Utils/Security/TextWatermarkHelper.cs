#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TextWatermarkHelper
// Guid:e752c4e9-5d23-4c86-8b6c-fb3a9d8e7c5f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/04 18:57:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using System.Text.RegularExpressions;
using XiHan.Framework.Utils.Security.Cryptography;
using XiHan.Framework.Utils.Text;
using XiHan.Framework.Utils.Text.Json.Serialization;
using XiHan.Framework.Utils.Verifications;

namespace XiHan.Framework.Utils.Security;

/// <summary>
/// Unicode文本水印帮助类，提供在文本中嵌入不可见水印的功能
/// </summary>
public static partial class TextWatermarkHelper
{
    // 用于水印的Unicode不可见字符集
    private static readonly char[] WatermarkChars = [
        '\u200B', // 零宽空格
        '\u200C', // 零宽不连字
        '\u200D', // 零宽连字
        '\u2060', // 单词连接符
        '\u2061', // 函数应用
        '\u2062', // 不可见乘号
        '\u2063', // 不可见分隔符
        '\u2064'  // 不可见加号
    ];

    /// <summary>
    /// 在文本中嵌入水印
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <param name="watermark">水印信息</param>
    /// <param name="key">加密密钥，默认为null</param>
    /// <returns>包含水印的文本</returns>
    public static string EmbedWatermark(string text, string watermark, string? key = null)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(watermark))
        {
            return text;
        }

        // 对水印信息进行预处理
        var watermarkData = key != null ? AesHelper.Encrypt(watermark, key) : watermark;

        // 将水印信息转换为二进制
        var watermarkBits = watermarkData.FromStringToBinary();

        // 嵌入水印
        var result = new StringBuilder(text);
        var index = 0;
        var sentences = SplitIntoSentences(text);

        foreach (var sentence in sentences)
        {
            if (index < watermarkBits.Length && !string.IsNullOrWhiteSpace(sentence))
            {
                // 在每个句子结尾处嵌入一个水印位
                var endPos = text.IndexOf(sentence) + sentence.Length;
                if (endPos < result.Length)
                {
                    var bit = watermarkBits[index];
                    var watermarkChar = WatermarkChars[bit % WatermarkChars.Length];
                    result.Insert(endPos, watermarkChar);
                    index++;
                }
            }
        }

        // 如果句子不够，在文本的其他地方添加剩余的水印信息
        for (var i = index; i < watermarkBits.Length; i++)
        {
            var bit = watermarkBits[i];
            var watermarkChar = WatermarkChars[bit % WatermarkChars.Length];
            var position = (i - index + 1) * text.Length / (watermarkBits.Length - index + 1);
            if (position < result.Length)
            {
                result.Insert(position, watermarkChar);
            }
            else
            {
                result.Append(watermarkChar);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 从文本中提取水印
    /// </summary>
    /// <param name="text">包含水印的文本</param>
    /// <param name="key">解密密钥，默认为null</param>
    /// <returns>提取的水印信息，如果未找到水印则返回空字符串</returns>
    public static string ExtractWatermark(string text, string? key = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // 提取水印字符
        var watermarkCharsPattern = new string(WatermarkChars);
        var watermarkRegex = new Regex($"[{Regex.Escape(watermarkCharsPattern)}]");
        var matches = watermarkRegex.Matches(text);

        if (matches.Count == 0)
        {
            return string.Empty;
        }

        // 将水印字符转换为二进制
        var bits = new StringBuilder();
        foreach (Match match in matches)
        {
            var ch = match.Value[0];
            var bitIndex = Array.IndexOf(WatermarkChars, ch);
            bits.Append(bitIndex);
        }

        // 将二进制转换为字符串
        var watermark = bits.ToString().FromBinaryToString();

        // 如果有密钥，解密水印
        return key != null ? AesHelper.Decrypt(watermark, key) : watermark;
    }

    /// <summary>
    /// 检查文本是否包含水印
    /// </summary>
    /// <param name="text">要检查的文本</param>
    /// <returns>如果包含水印则返回true，否则返回false</returns>
    public static bool ContainsWatermark(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var watermarkCharsPattern = new string(WatermarkChars);
        var watermarkRegex = new Regex($"[{Regex.Escape(watermarkCharsPattern)}]");
        return watermarkRegex.IsMatch(text);
    }

    /// <summary>
    /// 在文本中嵌入JSON格式的元数据水印
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <param name="metadata">元数据对象</param>
    /// <param name="key">加密密钥，默认为null</param>
    /// <returns>包含元数据水印的文本</returns>
    public static string EmbedMetadata<T>(string text, T metadata, string? key = null)
    {
        if (string.IsNullOrEmpty(text) || metadata == null)
        {
            return text;
        }

        // 将元数据序列化为JSON
        var json = metadata.SerializeTo();

        // 嵌入水印
        return EmbedWatermark(text, json, key);
    }

    /// <summary>
    /// 从文本中提取JSON格式的元数据水印
    /// </summary>
    /// <typeparam name="T">元数据类型</typeparam>
    /// <param name="text">包含水印的文本</param>
    /// <param name="key">解密密钥，默认为null</param>
    /// <returns>提取的元数据对象，如果未找到或无法解析则返回默认值</returns>
    public static T? ExtractMetadata<T>(string text, string? key = null)
    {
        var json = ExtractWatermark(text, key);
        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        try
        {
            return json.DeserializeTo<T>();
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// 在HTML文本中嵌入水印(在HTML标签之间插入不可见字符)
    /// </summary>
    /// <param name="htmlText">原始HTML文本</param>
    /// <param name="watermark">水印信息</param>
    /// <param name="key">加密密钥，默认为null</param>
    /// <returns>包含水印的HTML文本</returns>
    public static string EmbedHtmlWatermark(string htmlText, string watermark, string? key = null)
    {
        if (string.IsNullOrEmpty(htmlText) || string.IsNullOrEmpty(watermark))
        {
            return htmlText;
        }

        // 对水印信息进行预处理
        var watermarkData = key != null ? AesHelper.Encrypt(watermark, key) : watermark;

        // 将水印信息转换为二进制
        var watermarkBits = watermarkData.FromStringToBinary();

        // 使用正则表达式匹配HTML标签
        var tagRegex = RegexHelper.HtmlTagContentRegex();
        var matches = tagRegex.Matches(htmlText);

        if (matches.Count == 0)
        {
            // 如果没有找到HTML标签，使用普通文本水印方法
            return EmbedWatermark(htmlText, watermark, key);
        }

        var result = new StringBuilder(htmlText);
        var offset = 0;

        for (var i = 0; i < matches.Count && i < watermarkBits.Length; i++)
        {
            var match = matches[i];
            var bit = watermarkBits[i % watermarkBits.Length];
            var watermarkChar = WatermarkChars[bit % WatermarkChars.Length];

            // 在>和文本内容之间插入不可见字符
            var position = match.Groups[0].Index + 1 + offset;
            result.Insert(position, watermarkChar);
            offset++;
        }

        return result.ToString();
    }

    /// <summary>
    /// 从HTML文本中提取水印
    /// </summary>
    /// <param name="htmlText">包含水印的HTML文本</param>
    /// <param name="key">解密密钥，默认为null</param>
    /// <returns>提取的水印信息，如果未找到水印则返回空字符串</returns>
    public static string ExtractHtmlWatermark(string htmlText, string? key = null)
    {
        // 使用相同的提取逻辑
        return ExtractWatermark(htmlText, key);
    }

    /// <summary>
    /// 为多个文本块添加统一标识的水印
    /// </summary>
    /// <param name="textBlocks">文本块集合</param>
    /// <param name="identifier">统一标识符</param>
    /// <param name="key">加密密钥，默认为null</param>
    /// <returns>包含水印的文本块集合</returns>
    public static List<string> EmbedBatchWatermark(List<string> textBlocks, string identifier, string? key = null)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return textBlocks ?? [];
        }

        var result = new List<string>();
        var counter = 0;

        foreach (var text in textBlocks)
        {
            // 为每个文本块添加唯一的连续编号水印
            var watermark = $"{identifier}_{counter++}";
            result.Add(EmbedWatermark(text, watermark, key));
        }

        return result;
    }

    #region 私有辅助方法

    /// <summary>
    /// 将文本分割为句子
    /// </summary>
    private static List<string> SplitIntoSentences(string text)
    {
        var regex = RegexHelper.SentenceSplitterRegex();
        var matches = regex.Matches(text);

        var sentences = new List<string>();
        foreach (Match match in matches)
        {
            sentences.Add(match.Value);
        }

        return sentences;
    }

    #endregion 私有辅助方法
}
