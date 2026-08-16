// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using ModelContextProtocol.Protocol;

namespace XiHan.Framework.Web.Mcp.Tests;

/// <summary>
/// 技能到 MCP 工具的投影：宿主注册的 <c>IAiSkill</c> 经 /mcp 既列得出也调得动
/// </summary>
/// <remarks>
/// 投影本身由 XiHan.Framework.AI 的 SkillMcpToolsConfigurator 完成，但它只在宿主调了
/// <c>AddMcpServer()</c> 时才被触发——而那次调用正是本包在 <c>AddXiHanWebMcp</c> 里发出的。
/// 所以这条链路端到端能不能通，只有从 HTTP 这一头才验得了。
/// </remarks>
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
    /// <remarks>
    /// 断言里带上实参「梅花桩」：回显技能的输出由入参决定，拿到 <c>echo:梅花桩</c>
    /// 才说明请求真的穿过 HTTP → 端点过滤器 → MCP 处理器 → 技能函数走了一整趟；
    /// 换成返回常量的技能，一个根本没执行函数的实现也能让断言通过。
    /// <para>
    /// 用包含而非相等：函数返回值会被 SDK 按 JSON 序列化再塞进文本块，
    /// 字符串技能拿到的文本因此是带引号的 <c>"echo:梅花桩"</c>。引号与转义方式属于 SDK 的实现细节，
    /// 钉死它只会在升级依赖时平白变红，钉住「回显内容在里面」才是这条测试要守的东西。
    /// </para>
    /// </remarks>
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

        Assert.Contains("echo:梅花桩", text, StringComparison.Ordinal);
    }
}
