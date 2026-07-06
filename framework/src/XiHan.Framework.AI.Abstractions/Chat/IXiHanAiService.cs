#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanAiService
// Guid:a1b2c3d4-e5f6-4a06-9c06-0a0b0c0d0e06
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.AI;

namespace XiHan.Framework.AI.Abstractions.Chat;

/// <summary>
/// XiHan AI 会话服务门面
/// </summary>
/// <remarks>
/// 薄封装 Microsoft.Extensions.AI 的 <see cref="IChatClient"/>：只加「多 provider 选择」这层 XiHan 语义；
/// 工具调用、结构化输出、中间件（遥测/缓存）均走 M.E.AI 原生，不重造。
/// </remarks>
public interface IXiHanAiService
{
    /// <summary>
    /// 一次对话
    /// </summary>
    Task<ChatResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        XiHanChatOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式对话
    /// </summary>
    IAsyncEnumerable<ChatResponseUpdate> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        XiHanChatOptions? options = null,
        CancellationToken cancellationToken = default);
}
