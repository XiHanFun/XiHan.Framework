#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanSkill
// Guid:eaf5d1fc-7dea-4526-afaa-4b0b641c4d2b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Skills;

/// <summary>
/// 曦寒技能接口
/// </summary>
public interface IXiHanSkill
{
    /// <summary>
    /// 技能名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 技能描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 执行技能
    /// </summary>
    /// <param name="input">输入内容</param>
    /// <param name="context">技能上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>技能执行结果</returns>
    Task<XiHanSkillResult> ExecuteAsync(string input, SkillContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断技能是否可处理指定输入
    /// </summary>
    /// <param name="input">输入内容</param>
    /// <param name="context">技能上下文</param>
    /// <returns>是否可处理</returns>
    bool CanHandle(string input, SkillContext context);
}
