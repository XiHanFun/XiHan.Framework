// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace XiHan.Framework.Web.Mcp.Tests.Options;

/// <summary>
/// 工具暴露策略：允许/拒绝清单决定哪些技能能经 /mcp 被看见和被调用
/// </summary>
/// <remarks>
/// 每条测试同时断言 tools/list 与 tools/call。
/// <para>
/// 限制类断言都配一个可调用的对照工具：清单放行的那个工具仍能调用，且回显里带着它自己的技能名。
/// </para>
/// </remarks>
public class McpToolExposureEndToEndTests
{
    /// <summary>
    /// 宿主经 ListToolsHandler / CallToolHandler 提供的工具名
    /// </summary>
    private const string HandlerTool = "xihan_test_handler";

    /// <summary>
    /// 宿主配置的正确密钥
    /// </summary>
    private const string ApiKey = "tool-exposure-key";

    /// <summary>
    /// 甲工具名（同时也是技能名）
    /// </summary>
    private const string AlphaTool = "xihan_test_alpha";

    /// <summary>
    /// 乙工具名（同时也是技能名）
    /// </summary>
    private const string BetaTool = "xihan_test_beta";

    /// <summary>
    /// 甲工具名的大写写法，用来验证名字匹配区分大小写
    /// </summary>
    private const string AlphaToolInWrongCase = "XIHAN_TEST_ALPHA";

    /// <summary>
    /// 调用时传给回显技能的实参
    /// </summary>
    private const string CallArgument = "梅花桩";

    /// <summary>
    /// 两个清单都不配时，全部技能照旧暴露且照旧调得动（升级兼容性保证）
    /// </summary>
    [Fact]
    public async Task PostConfigure_WithBothListsEmpty_ExposesEveryTool()
    {
        await using var host = await McpTestHost.StartAsync(
            enabled: true,
            ApiKey,
            new NamedEchoAiSkill(AlphaTool),
            new NamedEchoAiSkill(BetaTool));

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);

        var names = await ListToolNamesAsync(session);

        Assert.Contains(AlphaTool, names);
        Assert.Contains(BetaTool, names);

        await AssertCallableAsync(session, AlphaTool);
        await AssertCallableAsync(session, BetaTool);
    }

    /// <summary>
    /// 配了允许清单时，清单外的工具既不出现在列表里也调不动
    /// </summary>
    [Fact]
    public async Task PostConfigure_WithAllowList_HidesAndBlocksToolsOutsideIt()
    {
        await using var host = await McpTestHost.StartAsync(
            enabled: true,
            ApiKey,
            [AlphaTool],
            [],
            new NamedEchoAiSkill(AlphaTool),
            new NamedEchoAiSkill(BetaTool));

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);

        var names = await ListToolNamesAsync(session);

        Assert.Contains(AlphaTool, names);
        Assert.DoesNotContain(BetaTool, names);

        Assert.Null(await TryCallAsync(session, BetaTool));
        await AssertCallableAsync(session, AlphaTool);
    }

    /// <summary>
    /// 配了拒绝清单时，清单里的工具既不出现在列表里也调不动
    /// </summary>
    [Fact]
    public async Task PostConfigure_WithDenyList_HidesAndBlocksListedTools()
    {
        await using var host = await McpTestHost.StartAsync(
            enabled: true,
            ApiKey,
            [],
            [BetaTool],
            new NamedEchoAiSkill(AlphaTool),
            new NamedEchoAiSkill(BetaTool));

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);

        var names = await ListToolNamesAsync(session);

        Assert.Contains(AlphaTool, names);
        Assert.DoesNotContain(BetaTool, names);

        Assert.Null(await TryCallAsync(session, BetaTool));
        await AssertCallableAsync(session, AlphaTool);
    }

    /// <summary>
    /// 同一个名字同时出现在两个清单里时，以拒绝为准
    /// </summary>
    [Fact]
    public async Task PostConfigure_WithNameInBothLists_PrefersDeny()
    {
        await using var host = await McpTestHost.StartAsync(
            enabled: true,
            ApiKey,
            [AlphaTool, BetaTool],
            [BetaTool],
            new NamedEchoAiSkill(AlphaTool),
            new NamedEchoAiSkill(BetaTool));

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);

        var names = await ListToolNamesAsync(session);

        Assert.Contains(AlphaTool, names);
        Assert.DoesNotContain(BetaTool, names);

        Assert.Null(await TryCallAsync(session, BetaTool));
        await AssertCallableAsync(session, AlphaTool);
    }

    /// <summary>
    /// 允许清单里的名字大小写不对时不会误放行
    /// </summary>
    /// <remarks>
    /// 名字按序号比较、区分大小写，大小写不匹配的名字匹配不上任何工具，于是没有工具被放行。
    /// </remarks>
    [Fact]
    public async Task PostConfigure_WithMiscasedAllowEntry_StillHidesTheTool()
    {
        await using var host = await McpTestHost.StartAsync(
            enabled: true,
            ApiKey,
            [AlphaToolInWrongCase],
            [],
            new NamedEchoAiSkill(AlphaTool));

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);

        var names = await ListToolNamesAsync(session);

        Assert.DoesNotContain(AlphaTool, names);
        Assert.Null(await TryCallAsync(session, AlphaTool));
    }

    /// <summary>
    /// 拒绝清单里的名字大小写不对时不会误拦截
    /// </summary>
    /// <remarks>
    /// 大小写写错的拒绝清单拦不住任何工具；启动期会对这样的清单项记一次警告。
    /// </remarks>
    [Fact]
    public async Task PostConfigure_WithMiscasedDenyEntry_DoesNotBlockTheTool()
    {
        await using var host = await McpTestHost.StartAsync(
            enabled: true,
            ApiKey,
            [],
            [AlphaToolInWrongCase],
            new NamedEchoAiSkill(AlphaTool));

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);

        var names = await ListToolNamesAsync(session);

        Assert.Contains(AlphaTool, names);
        await AssertCallableAsync(session, AlphaTool);
    }

    /// <summary>
    /// 清单同样作用于宿主 handler 提供的工具：被拒绝的 handler 工具既不出现在列表里，调用也到不了 handler
    /// </summary>
    [Fact]
    public async Task PostConfigure_WithDenyList_HidesAndBlocksHandlerProvidedTool()
    {
        var handlerCalls = new StrongBox<int>();
        await using var host = await McpTestHost.StartAsync(
            enabled: true,
            ApiKey,
            [],
            [HandlerTool],
            AddHandlerTool(handlerCalls),
            new NamedEchoAiSkill(AlphaTool));

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);

        var names = await ListToolNamesAsync(session);

        Assert.Contains(AlphaTool, names);
        Assert.DoesNotContain(HandlerTool, names);

        Assert.Null(await TryCallAsync(session, HandlerTool));
        Assert.Equal(0, handlerCalls.Value);
        await AssertCallableAsync(session, AlphaTool);
    }

    /// <summary>
    /// 允许清单外的名字不会落到 CallToolHandler 的回退上
    /// </summary>
    /// <remarks>
    /// 乙技能已从工具集移除，若没有 tools/call 过滤器，对它的调用会回退到宿主的 CallToolHandler 并被当成成功处理。
    /// </remarks>
    [Fact]
    public async Task PostConfigure_WithAllowList_KeepsCallsOffTheFallbackHandler()
    {
        var handlerCalls = new StrongBox<int>();
        await using var host = await McpTestHost.StartAsync(
            enabled: true,
            ApiKey,
            [AlphaTool],
            [],
            AddHandlerTool(handlerCalls),
            new NamedEchoAiSkill(AlphaTool),
            new NamedEchoAiSkill(BetaTool));

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);

        var names = await ListToolNamesAsync(session);

        Assert.Contains(AlphaTool, names);
        Assert.DoesNotContain(BetaTool, names);
        Assert.DoesNotContain(HandlerTool, names);

        Assert.Null(await TryCallAsync(session, BetaTool));
        Assert.Null(await TryCallAsync(session, HandlerTool));
        Assert.Equal(0, handlerCalls.Value);
        await AssertCallableAsync(session, AlphaTool);
    }

    /// <summary>
    /// 清单没点到的 handler 工具照常可见、可调用（对照：handler 确实接上了）
    /// </summary>
    [Fact]
    public async Task PostConfigure_WithListsNotNamingHandlerTool_KeepsItReachable()
    {
        var handlerCalls = new StrongBox<int>();
        await using var host = await McpTestHost.StartAsync(
            enabled: true,
            ApiKey,
            [],
            [BetaTool],
            AddHandlerTool(handlerCalls),
            new NamedEchoAiSkill(AlphaTool),
            new NamedEchoAiSkill(BetaTool));

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);

        var names = await ListToolNamesAsync(session);

        Assert.Contains(HandlerTool, names);
        Assert.Null(await TryCallAsync(session, BetaTool));
        Assert.Equal($"handler:{HandlerTool}", await TryCallAsync(session, HandlerTool));
        Assert.Equal(1, handlerCalls.Value);
    }

    /// <summary>
    /// 清单含空白项时宿主启动即失败
    /// </summary>
    [Fact]
    public async Task Startup_WithBlankListEntry_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var host = await McpTestHost.StartAsync(
                enabled: true,
                ApiKey,
                [AlphaTool, " "],
                [],
                new NamedEchoAiSkill(AlphaTool));
        });

        Assert.Contains("AllowedTools:1", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 给宿主挂上自定义 ListToolsHandler / CallToolHandler，提供一个不在工具集里的工具
    /// </summary>
    /// <param name="handlerCalls">CallToolHandler 被调用的次数</param>
    /// <returns>追加到宿主的服务注册</returns>
    private static Action<IServiceCollection> AddHandlerTool(StrongBox<int> handlerCalls)
    {
        return services => services.Configure<McpServerOptions>(options =>
        {
            options.Handlers.ListToolsHandler = (_, _) => ValueTask.FromResult(new ListToolsResult
            {
                Tools = [new Tool { Name = HandlerTool, Description = "宿主 handler 提供的工具，仅用于测试" }]
            });

            options.Handlers.CallToolHandler = (request, cancellationToken) =>
            {
                _ = Interlocked.Increment(ref handlerCalls.Value);
                return ValueTask.FromResult(new CallToolResult
                {
                    Content = [new TextContentBlock { Text = $"handler:{request.Params?.Name}" }]
                });
            };
        });
    }

    /// <summary>
    /// 取 tools/list 里的工具名
    /// </summary>
    /// <param name="session">已握手的会话</param>
    /// <returns>工具名列表</returns>
    private static async Task<IReadOnlyList<string>> ListToolNamesAsync(McpTestSession session)
    {
        var tools = await session.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        return [.. tools.Select(tool => tool.Name)];
    }

    /// <summary>
    /// 调一次工具，拿回回显文本；服务端不认这个名字时返回 null
    /// </summary>
    /// <param name="session">已握手的会话</param>
    /// <param name="toolName">工具名</param>
    /// <returns>回显文本，工具不可调用时为 null</returns>
    private static async Task<string?> TryCallAsync(McpTestSession session, string toolName)
    {
        try
        {
            var result = await session.Client.CallToolAsync(
                toolName,
                new Dictionary<string, object?> { ["text"] = CallArgument },
                cancellationToken: TestContext.Current.CancellationToken);

            // 工具集里没有这个名字时，服务端也可能以「错误结果」而非 JSON-RPC 错误作答，两种都算调不动
            return result.IsError is true
                ? null
                : Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        }
        catch (McpException)
        {
            // 名字不在工具集里，服务端回 JSON-RPC 错误，客户端把它抛成 McpException
            return null;
        }
    }

    /// <summary>
    /// 断言一个工具确实调得动，且跑的是它自己那个技能
    /// </summary>
    /// <param name="session">已握手的会话</param>
    /// <param name="toolName">工具名</param>
    /// <returns>断言任务</returns>
    private static async Task AssertCallableAsync(McpTestSession session, string toolName)
    {
        var text = await TryCallAsync(session, toolName);

        Assert.NotNull(text);

        // 回显里带着技能名与实参，确认调用真的进了这个技能的函数体
        Assert.Contains($"echo:{toolName}:{CallArgument}", text, StringComparison.Ordinal);
    }
}
