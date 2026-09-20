// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using ModelContextProtocol.Protocol;

namespace XiHan.Framework.Web.Mcp.Tests;

/// <summary>
/// 技能到 MCP 工具的投影：宿主注册的 <c>IAiSkill</c> 经 /mcp 既列得出也调得动
/// </summary>
public class McpSkillProjectionTests
{
    /// <summary>
    /// 宿主配置的正确密钥
    /// </summary>
    private const string ApiKey = "skill-projection-key";

    /// <summary>
    /// 已注册的技能出现在 tools/list 里，名称与说明都取自技能本身
    /// </summary>
    [Fact]
    public async Task 已注册的技能出现在工具列表里()
    {
        var skill = new EchoAiSkill();
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey, skill);

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);
        var tools = await session.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var tool = Assert.Single(tools, candidate => candidate.Name == skill.Name);

        Assert.Equal(skill.Description, tool.Description);
    }

    /// <summary>
    /// 已注册的技能可经 tools/call 调用，返回的是技能自己算出来的结果
    /// </summary>
    [Fact]
    public async Task 已注册的技能可经工具调用并返回结果()
    {
        var skill = new EchoAiSkill();
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey, skill);

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);
        var result = await session.Client.CallToolAsync(
            skill.Name,
            new Dictionary<string, object?> { ["text"] = "梅花桩" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsError is not true, "技能调用返回了错误结果。");

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        // 返回值经 SDK 序列化后带引号，故用 Contains 而非 Equal
        Assert.Contains("echo:梅花桩", text, StringComparison.Ordinal);
    }
}
