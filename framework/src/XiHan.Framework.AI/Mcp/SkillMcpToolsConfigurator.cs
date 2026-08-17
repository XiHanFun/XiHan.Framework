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
/// 变为 MCP tool,并入 <see cref="McpServerOptions.ToolCollection"/>。注册表构造时已收纳全部技能,
/// 故此处 <c>All</c> 已就绪。仅当 WebHost 调用 <c>AddMcpServer()</c> 时本配置器才被触发。
/// <para>
/// 工具名冲突直接抛异常，不再静默去重。丢能力只是其一：撞名的两个技能里注定有一个既列不出也调不到，
/// 与其让运维在「注册过的技能凭空不存在」上排查，不如在装配 MCP 选项时就点名说清是谁撞了谁。
/// </para>
/// <para>
/// 更要紧的是**授权语义的歧义**：对外暴露的允许/拒绝清单（如 <c>XiHan.Framework.Web.Mcp</c> 的
/// <c>AllowedTools</c>）按工具名放行，重名会让同一条「允许 X」指向两个不同的能力，
/// 具体生效哪一个取决于技能的注册顺序——运维以为放行了甲，实际放行的可能是乙。
/// 安全配置不能建立在「谁先注册」上，所以这里宁可让宿主起不来。
/// </para>
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
    /// 把注册表内全部技能投影为 MCP 工具，并入选项的工具集合；工具名冲突即失败
    /// </summary>
    /// <param name="options">待配置的 MCP 服务端选项</param>
    /// <exception cref="InvalidOperationException">两个技能投影出同名工具，或技能与工具集里已有的工具重名</exception>
    public void Configure(McpServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ToolCollection ??= [];

        // 记下每个工具名由哪个技能贡献，撞名时才点得出双方
        var owners = new Dictionary<string, IAiSkill>(StringComparer.Ordinal);

        foreach (var skill in _registry.All)
        {
            var tool = McpServerTool.Create(skill.AsFunction());
            var toolName = tool.ProtocolTool.Name;

            if (options.ToolCollection.TryAdd(tool))
            {
                owners[toolName] = skill;
                continue;
            }

            // 加不进去只有一个原因：同名工具已在集合里。是不是技能贡献的，决定了另一方能报到多细
            var rival = owners.TryGetValue(toolName, out var owner)
                ? $"技能 {Describe(owner)}"
                : "工具集中已有的同名工具（如宿主经 WithTools 直接注册的）";

            throw new InvalidOperationException(
                $"MCP 工具名冲突：技能 {Describe(skill)} 投影出的工具「{toolName}」与{rival}重名。"
                + "同名工具只有一个能被列出与调用，另一个会无声消失；且对外暴露的允许/拒绝清单按工具名放行，"
                + "重名会让同一条清单项指向两个不同的能力，究竟是哪一个取决于注册顺序。"
                + "故此处直接失败；请改掉其中一方的工具名。");
        }
    }

    /// <summary>
    /// 描述一个技能，冲突信息里用来指名道姓
    /// </summary>
    /// <param name="skill">待描述的技能</param>
    /// <returns>形如 <c>名字(类型全名)</c> 的描述</returns>
    private static string Describe(IAiSkill skill)
    {
        return $"{skill.Name}({skill.GetType().FullName})";
    }
}
