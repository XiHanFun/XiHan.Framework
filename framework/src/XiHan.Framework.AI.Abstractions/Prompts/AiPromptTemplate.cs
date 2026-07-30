// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
