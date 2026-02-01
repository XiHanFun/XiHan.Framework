#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SkillRegistry
// Guid:b2e6de95-cc0c-4cd0-8862-38e8a26b9ad8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace XiHan.Framework.AI.Skills;

/// <summary>
/// 技能注册表
/// </summary>
public class SkillRegistry : ISkillRegistry
{
    private readonly ConcurrentDictionary<string, IXiHanSkill> _skills = new();
    private readonly ILogger<SkillRegistry> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public SkillRegistry(ILogger<SkillRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 注册技能
    /// </summary>
    public Task<bool> RegisterSkillAsync(IXiHanSkill skill, CancellationToken cancellationToken = default)
    {
        var result = _skills.TryAdd(skill.Name, skill);
        if (result)
        {
            _logger.LogInformation("已注册技能: {SkillName}", skill.Name);
        }
        else
        {
            _logger.LogWarning("技能注册失败，已存在同名技能: {SkillName}", skill.Name);
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// 获取技能
    /// </summary>
    public Task<IXiHanSkill?> GetSkillAsync(string skillName, CancellationToken cancellationToken = default)
    {
        _skills.TryGetValue(skillName, out var skill);
        return Task.FromResult(skill);
    }

    /// <summary>
    /// 获取所有技能
    /// </summary>
    public Task<IEnumerable<IXiHanSkill>> GetAllSkillsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<IXiHanSkill>>([.. _skills.Values]);
    }

    /// <summary>
    /// 查找匹配的技能
    /// </summary>
    public Task<IEnumerable<IXiHanSkill>> FindMatchingSkillsAsync(string input, SkillContext context, CancellationToken cancellationToken = default)
    {
        var matchingSkills = _skills.Values.Where(skill => skill.CanHandle(input, context)).ToList();
        return Task.FromResult<IEnumerable<IXiHanSkill>>(matchingSkills);
    }
}
