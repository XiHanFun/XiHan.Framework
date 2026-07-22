// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;

namespace XiHan.Framework.AI.Abstractions.Chat;

/// <summary>
/// XiHan 会话选项（在原生 <see cref="ChatOptions"/> 上叠加 provider 选择等 XiHan 语义）
/// </summary>
public class XiHanChatOptions
{
    /// <summary>
    /// 指定 provider（null 用默认 provider）
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// 原生对话选项（工具/温度/模型覆盖等，透传 Microsoft.Extensions.AI）
    /// </summary>
    public ChatOptions? ChatOptions { get; set; }
}
