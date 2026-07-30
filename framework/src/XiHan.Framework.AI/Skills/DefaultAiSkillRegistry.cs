// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.AI.Abstractions.Skills;

namespace XiHan.Framework.AI.Skills;

/// <summary>
/// <see cref="IAiSkillRegistry"/> 默认实现（线程安全,按名索引,同名覆盖）
/// </summary>
/// <remarks>
/// 构造时自动收纳 DI 里所有已注册的 <see cref="IAiSkill"/>（应用层 <c>AddSingleton&lt;IAiSkill, XxxSkill&gt;()</c> 即入表）;
/// 也支持运行时 <see cref="Register"/> 追加。框架据此把技能暴露为对话工具 / MCP tools。
/// </remarks>
public sealed class DefaultAiSkillRegistry : IAiSkillRegistry
{
    private readonly ConcurrentDictionary<string, IAiSkill> _skills = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 构造函数（收纳 DI 注册的全部技能）
    /// </summary>
    public DefaultAiSkillRegistry(IEnumerable<IAiSkill> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        foreach (var skill in skills)
        {
            Register(skill);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IAiSkill> All => _skills.Values.ToArray();

    /// <inheritdoc />
    public void Register(IAiSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentException.ThrowIfNullOrWhiteSpace(skill.Name);
        _skills[skill.Name] = skill;
    }

    /// <inheritdoc />
    public IAiSkill? Find(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? null : _skills.GetValueOrDefault(name);
    }
}
