// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

    /// <summary>
    /// 把注册表内全部技能投影为 MCP 工具，并入选项的工具集合（按工具名去重）
    /// </summary>
    /// <param name="options">待配置的 MCP 服务端选项</param>
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
