// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Indexing;

/// <summary>
/// 中英混合分词器
/// </summary>
/// <remarks>
/// 中文没有空格分隔，故对连续中文字符切双字词（bigram）；英文与标识符按非字母数字边界切词，
/// 并额外按帕斯卡/驼峰命名拆分，使 <c>ILocalEventBus</c> 与「event bus」能命中同一段文本。
/// 长度不足 2 的词条一律丢弃，因为它们的区分度过低。
/// </remarks>
public static class Tokenizer
{
    /// <summary>
    /// 把文本切分为词条，结果不保证去重
    /// </summary>
    /// <param name="text">待切分的文本</param>
    /// <returns>词条列表，输入为空时返回空集合</returns>
    /// <remarks>
    /// 本方法只保证集合语义：一个词条出现在结果里，当且仅当它能按上述规则从文本中切出来。
    /// 出现次数不承载任何信息，不要拿来当词频用——重复项的有无是实现细节而非契约：
    /// 帕斯卡拆词的结果会用 <c>terms.Contains</c> 对已累积的列表去重，
    /// 而整词与中文 bigram 的追加不去重，两条路径的语义本就不一致。
    /// <para>
    /// 现存的消费方也都在自己那一侧去重，没有一处读词频：
    /// <c>BigramIndex.Add</c> 走 <c>ToHashSet</c>、<c>RelevanceGate</c> 走 <c>Distinct</c>、
    /// <c>SynonymExpander.Expand</c> 用字典累权重。当前语料下这层不一致无害
    /// （章节短、词表小，线性 <c>Contains</c> 也谈不上开销），因此保持现状；
    /// 真要统一，去重该由调用方决定，而不是在这里替所有人做主。
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var terms = new List<string>();
        var index = 0;

        while (index < text.Length)
        {
            var current = text[index];

            if (IsCjk(current))
            {
                var start = index;
                while (index < text.Length && IsCjk(text[index]))
                {
                    index++;
                }

                AppendCjkBigrams(text.AsSpan(start, index - start), terms);
            }
            else if (char.IsLetterOrDigit(current))
            {
                var start = index;
                while (index < text.Length && char.IsLetterOrDigit(text[index]) && !IsCjk(text[index]))
                {
                    index++;
                }

                AppendAsciiTerms(text.AsSpan(start, index - start), terms);
            }
            else
            {
                index++;
            }
        }

        return terms;
    }

    /// <summary>
    /// 判断字符是否属于中日韩统一表意文字区段
    /// </summary>
    private static bool IsCjk(char value)
    {
        return value is >= '一' and <= '鿿';
    }

    /// <summary>
    /// 对一段连续中文追加双字词，单字段落直接丢弃
    /// </summary>
    private static void AppendCjkBigrams(ReadOnlySpan<char> span, List<string> terms)
    {
        for (var i = 0; i + 1 < span.Length; i++)
        {
            terms.Add(span.Slice(i, 2).ToString());
        }
    }

    /// <summary>
    /// 对一段英文或数字追加整词以及帕斯卡拆词结果
    /// </summary>
    private static void AppendAsciiTerms(ReadOnlySpan<char> span, List<string> terms)
    {
        if (span.Length >= 2)
        {
            terms.Add(span.ToString().ToLowerInvariant());
        }

        var start = 0;
        for (var i = 1; i <= span.Length; i++)
        {
            if (!IsWordBoundary(span, i))
            {
                continue;
            }

            var part = span[start..i];
            if (part.Length >= 2)
            {
                var lowered = part.ToString().ToLowerInvariant();
                if (!terms.Contains(lowered))
                {
                    terms.Add(lowered);
                }
            }

            start = i;
        }
    }

    /// <summary>
    /// 判断位置 <paramref name="i"/> 是否为帕斯卡/驼峰命名的词边界
    /// </summary>
    private static bool IsWordBoundary(ReadOnlySpan<char> span, int i)
    {
        if (i == span.Length)
        {
            return true;
        }

        // 小写后接大写：eventBus 在 B 处断开
        if (char.IsUpper(span[i]) && !char.IsUpper(span[i - 1]))
        {
            return true;
        }

        // 连续大写后接小写：HTTPServer 在 S 处断开
        return char.IsUpper(span[i]) && char.IsUpper(span[i - 1]) && i + 1 < span.Length && char.IsLower(span[i + 1]);
    }
}
