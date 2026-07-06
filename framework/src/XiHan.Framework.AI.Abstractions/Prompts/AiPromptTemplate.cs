#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AiPromptTemplate
// Guid:592963b4-8629-448d-bdd5-bf2fd8842630
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Abstractions.Prompts;

/// <summary>
/// AI 提示词模板（内容由应用层提供；支持 store 化 + 版本）
/// </summary>
public class AiPromptTemplate
{
    /// <summary>
    /// 模板名（唯一标识）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模板正文（可含占位/Scriban 变量，由渲染器填充）
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 版本（空为当前/最新）
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 说明
    /// </summary>
    public string? Description { get; set; }
}
