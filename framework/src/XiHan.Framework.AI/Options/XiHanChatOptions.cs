#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanChatOptions
// Guid:9be00b56-842d-4c8a-8559-d6606d2dfd64
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// 聊天选项
/// </summary>
public class XiHanChatOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:AI:Chat";

    /// <summary>
    /// 聊天模型名称
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// 温度(0.0-1.0)，控制回复的随机性
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// 最大令牌数
    /// </summary>
    public int MaxTokens { get; set; } = 2000;

    /// <summary>
    /// 系统提示词
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// 聊天上下文(历史记录)
    /// </summary>
    public List<(string Role, string Content)> ChatHistory { get; set; } = [];
}
