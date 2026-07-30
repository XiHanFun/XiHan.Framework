// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.AI.Abstractions.Rag.Models;

namespace XiHan.Framework.AI.Abstractions.Rag;

/// <summary>
/// RAG 提示增强器（把检索片段注入用户提问，形成带上下文的最终提示）
/// </summary>
public interface IRagPromptAugmenter
{
    /// <summary>
    /// 用检索到的片段增强用户提问（context 为空则原样返回）
    /// </summary>
    string Augment(string userPrompt, IReadOnlyList<RetrievedChunk> context);
}
