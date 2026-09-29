// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using XiHan.Framework.AI.Abstractions.Skills;
using XiHan.Framework.AI.Mcp;
using XiHan.Framework.AI.Skills;

namespace XiHan.Framework.AI.Tests;

/// <summary>
/// 技能投影为 MCP 工具的测试。
/// </summary>
public sealed class SkillMcpToolsConfiguratorTests
{
    /// <summary>
    /// 工具名互不相同的技能全部并入工具集。
    /// </summary>
    [Fact]
    public void Configure_DistinctToolNamesShouldAddEveryTool()
    {
        var options = new McpServerOptions();

        CreateConfigurator(new StubSkill("alpha"), new StubSkill("beta")).Configure(options);

        Assert.NotNull(options.ToolCollection);
        Assert.Equal(["alpha", "beta"], options.ToolCollection.Select(tool => tool.ProtocolTool.Name).Order());
    }

    /// <summary>
    /// 两个技能投影出同名工具时抛出，消息点出工具名与冲突双方。
    /// </summary>
    [Fact]
    public void Configure_SkillsProjectingSameToolNameShouldThrowNamingBoth()
    {
        var configurator = CreateConfigurator(new StubSkill("first", "shared_tool"), new StubSkill("second", "shared_tool"));

        var exception = Assert.Throws<InvalidOperationException>(() => configurator.Configure(new McpServerOptions()));

        Assert.Contains("shared_tool", exception.Message, StringComparison.Ordinal);
        Assert.Contains("first", exception.Message, StringComparison.Ordinal);
        Assert.Contains("second", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 技能与工具集里已有的工具重名时抛出。
    /// </summary>
    [Fact]
    public void Configure_ToolAlreadyInCollectionShouldThrow()
    {
        var options = new McpServerOptions { ToolCollection = [] };
        options.ToolCollection.Add(McpServerTool.Create(AIFunctionFactory.Create(() => "host", "taken_tool")));

        var configurator = CreateConfigurator(new StubSkill("skill", "taken_tool"));

        var exception = Assert.Throws<InvalidOperationException>(() => configurator.Configure(options));

        Assert.Contains("taken_tool", exception.Message, StringComparison.Ordinal);
        Assert.Contains("工具集中已有的同名工具", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 以给定技能构造配置器。
    /// </summary>
    /// <param name="skills">注册进技能注册表的技能。</param>
    /// <returns>配置器。</returns>
    private static SkillMcpToolsConfigurator CreateConfigurator(params IAiSkill[] skills)
    {
        return new SkillMcpToolsConfigurator(new DefaultAiSkillRegistry(skills));
    }

    /// <summary>
    /// 测试用技能：技能名与投影出的工具名可分别指定。
    /// </summary>
    /// <param name="name">技能名。</param>
    /// <param name="toolName">投影出的工具名，null 表示与技能名相同。</param>
    private sealed class StubSkill(string name, string? toolName = null) : IAiSkill
    {
        /// <inheritdoc />
        public string Name => name;

        /// <inheritdoc />
        public string Description => $"测试技能 {name}";

        /// <inheritdoc />
        public AIFunction AsFunction()
        {
            return AIFunctionFactory.Create(() => name, toolName ?? name, Description);
        }
    }
}
