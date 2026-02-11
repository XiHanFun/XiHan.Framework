#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanAIAgent
// Guid:ee473294-4159-4199-930e-313091e47d17
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI.Memory;
using XiHan.Framework.AI.Results;
using XiHan.Framework.AI.Skills;

namespace XiHan.Framework.AI.Agents;

/// <summary>
/// Agent基础接口
/// </summary>
public interface IXiHanAIAgent
{
    /// <summary>
    /// Agent唯一标识
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Agent名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Agent记忆服务
    /// </summary>
    IXiHanAIMemoryService? Memory { get; }

    /// <summary>
    /// Agent技能列表
    /// </summary>
    IReadOnlyCollection<IXiHanSkill> Skills { get; }

    /// <summary>
    /// 初始化Agent
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>初始化结果</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行Agent任务
    /// </summary>
    /// <param name="message">用户消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<XiHanChatResult> InvokeAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加技能
    /// </summary>
    /// <param name="skill">技能实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加结果</returns>
    Task<bool> AddSkillAsync(IXiHanSkill skill, CancellationToken cancellationToken = default);
}
