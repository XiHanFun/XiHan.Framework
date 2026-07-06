#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRagPromptAugmenter
// Guid:a1b2c3d4-e5f6-4a18-9c18-0a0b0c0d0e18
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
