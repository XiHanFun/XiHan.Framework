#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AgentOptions
// Guid:93ecd6f1-c60a-44a2-a307-b6d63bd8888a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// Agent配置选项
/// </summary>
public class AgentOptions
{
    /// <summary>
    /// Agent名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 系统提示词
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// 使用的服务提供商
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// 使用的模型名称
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用记忆
    /// </summary>
    public bool EnableMemory { get; set; } = true;

    /// <summary>
    /// 技能唯一标识列表
    /// </summary>
    public List<string> SkillIds { get; set; } = [];
}
