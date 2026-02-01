#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISkillRegistry
// Guid:cf6578f5-9f14-498b-805f-d5c51c97d341
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Skills;

/// <summary>
/// 技能注册表接口
/// </summary>
public interface ISkillRegistry
{
    /// <summary>
    /// 注册技能
    /// </summary>
    /// <param name="skill">技能实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>注册结果</returns>
    Task<bool> RegisterSkillAsync(IXiHanSkill skill, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取技能
    /// </summary>
    /// <param name="skillName">技能名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>技能实例或null</returns>
    Task<IXiHanSkill?> GetSkillAsync(string skillName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有技能
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>技能列表</returns>
    Task<IEnumerable<IXiHanSkill>> GetAllSkillsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找匹配的技能
    /// </summary>
    /// <param name="input">输入内容</param>
    /// <param name="context">技能上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匹配的技能列表</returns>
    Task<IEnumerable<IXiHanSkill>> FindMatchingSkillsAsync(string input, SkillContext context, CancellationToken cancellationToken = default);
}
