#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAiSkill
// Guid:a1b2c3d4-e5f6-4a07-9c07-0a0b0c0d0e07
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.AI;

namespace XiHan.Framework.AI.Abstractions.Skills;

/// <summary>
/// 应用层 AI 技能（命名的可复用 AI 能力，如「生成模块」「代码审查」「按 XiHan 规范加功能」）
/// </summary>
/// <remarks>
/// 由应用层（如 BasicApp）注册；框架把它统一暴露为两条通道——
/// ① 对话工具（<see cref="AIFunction"/>，供 M.E.AI 自动函数调用）；
/// ② MCP tool（经 ModelContextProtocol 暴露给 Claude Code / Cursor 等外部 AI 客户端）。
/// 「技能」是应用供给的能力单元，MCP 只是其一种交付通道。
/// </remarks>
public interface IAiSkill
{
    /// <summary>
    /// 技能名（唯一；作工具名 / MCP tool 名）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 技能说明（供模型理解何时调用）
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 转为可供对话工具调用 / MCP 暴露的 <see cref="AIFunction"/>
    /// </summary>
    AIFunction AsFunction();
}
