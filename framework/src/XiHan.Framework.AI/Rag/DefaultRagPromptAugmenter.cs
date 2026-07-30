// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.AI.Abstractions.Rag;
using XiHan.Framework.AI.Abstractions.Rag.Models;

namespace XiHan.Framework.AI.Rag;

/// <summary>
/// 默认 RAG 提示增强器（简单模板：约束 + 编号片段 + 问题）
/// </summary>
/// <remarks>不走 Scriban（框架 ITemplateService 的 string 引擎是简单替换，见 framework-templateservice-not-scriban）；直接插值即可。</remarks>
public sealed class DefaultRagPromptAugmenter : IRagPromptAugmenter
{
    /// <inheritdoc />
    public string Augment(string userPrompt, IReadOnlyList<RetrievedChunk> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Count == 0)
        {
            return userPrompt;
        }

        var builder = new StringBuilder();
        builder.AppendLine("请仅依据以下「知识片段」回答问题；若片段不足以回答，请明确说明未找到依据，不要编造。引用时标注片段编号（如 [1]）。");
        builder.AppendLine();
        builder.AppendLine("# 知识片段");
        for (var i = 0; i < context.Count; i++)
        {
            var chunk = context[i];
            var label = string.IsNullOrWhiteSpace(chunk.Title) ? chunk.Source : chunk.Title;
            builder.AppendLine(string.IsNullOrWhiteSpace(label) ? $"[{i + 1}]" : $"[{i + 1}] 来源：{label}");
            builder.AppendLine(chunk.Text);
            builder.AppendLine();
        }

        builder.AppendLine("# 问题");
        builder.AppendLine(userPrompt);
        return builder.ToString();
    }
}
