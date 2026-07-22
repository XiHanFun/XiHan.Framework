// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.AI.Abstractions.Skills;

/// <summary>
/// AI 技能注册表（应用注册其技能；框架按名解析，并暴露为对话工具 / MCP tools）
/// </summary>
public interface IAiSkillRegistry
{
    /// <summary>
    /// 注册一个技能（同名覆盖）
    /// </summary>
    void Register(IAiSkill skill);

    /// <summary>
    /// 全部已注册技能
    /// </summary>
    IReadOnlyList<IAiSkill> All { get; }

    /// <summary>
    /// 按名查找，未注册返回 null
    /// </summary>
    IAiSkill? Find(string name);
}
