#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SkillMcpToolsConfigurator
// Guid:b2c3d4e5-f6a7-4b22-9d22-1a1b1c1d1e22
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 18:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using XiHan.Framework.AI.Abstractions.Skills;

namespace XiHan.Framework.AI.Mcp;

/// <summary>
/// 把技能注册表里的全部 <see cref="IAiSkill"/> 投影为 MCP server tools
/// </summary>
/// <remarks>
/// 官方桥接:每个技能 <c>AsFunction()</c>(<c>AIFunction</c>)经 <see cref="McpServerTool.Create(Microsoft.Extensions.AI.AIFunction, McpServerToolCreateOptions)"/>
/// 变为 MCP tool,并入 <see cref="McpServerOptions.ToolCollection"/>(按工具名去重)。注册表构造时已收纳全部技能,
/// 故此处 <c>All</c> 已就绪。仅当 WebHost 调用 <c>AddMcpServer()</c> 时本配置器才被触发。
/// </remarks>
public sealed class SkillMcpToolsConfigurator : IConfigureOptions<McpServerOptions>
{
    private readonly IAiSkillRegistry _registry;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SkillMcpToolsConfigurator(IAiSkillRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public void Configure(McpServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ToolCollection ??= [];
        foreach (var skill in _registry.All)
        {
            options.ToolCollection.TryAdd(McpServerTool.Create(skill.AsFunction()));
        }
    }
}
