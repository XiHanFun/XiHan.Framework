// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using XiHan.Framework.Web.Mcp.Options;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace XiHan.Framework.Web.Mcp.Tests.Options;

/// <summary>
/// 工具暴露配置器：清单的合法性校验、匹配不到工具时的告警，以及两个清单都为空时不触碰选项
/// </summary>
/// <remarks>
/// 清单经 /mcp 的实际裁剪效果由 <see cref="McpToolExposureEndToEndTests"/> 在真实协议往返上断言。
/// </remarks>
public class McpToolExposureConfiguratorTests
{
    /// <summary>
    /// 甲工具名
    /// </summary>
    private const string AlphaTool = "xihan_test_alpha";

    /// <summary>
    /// 乙工具名
    /// </summary>
    private const string BetaTool = "xihan_test_beta";

    /// <summary>
    /// 清单里含空白项时装配即失败，异常点出是哪个清单的第几项
    /// </summary>
    /// <param name="listName">放入空白项的清单</param>
    /// <param name="blank">空白项的写法</param>
    [Theory]
    [InlineData(nameof(XiHanMcpOptions.AllowedTools), "")]
    [InlineData(nameof(XiHanMcpOptions.AllowedTools), "   ")]
    [InlineData(nameof(XiHanMcpOptions.DeniedTools), "")]
    [InlineData(nameof(XiHanMcpOptions.DeniedTools), "   ")]
    public void PostConfigure_WithBlankEntry_ThrowsNamingTheEntry(string listName, string blank)
    {
        var policy = new XiHanMcpOptions();
        var list = listName == nameof(XiHanMcpOptions.AllowedTools) ? policy.AllowedTools : policy.DeniedTools;
        list.Add(AlphaTool);
        list.Add(blank);

        var configurator = CreateConfigurator(policy, new RecordingLogger<McpToolExposureConfigurator>());

        var exception = Assert.Throws<InvalidOperationException>(() => configurator.PostConfigure(null, CreateServerOptions(AlphaTool)));

        Assert.Contains($"{XiHanMcpOptions.SectionName}:{listName}:1", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 两个清单都为空时不裁剪工具集、不挂过滤器、不告警
    /// </summary>
    [Fact]
    public void PostConfigure_WithBothListsEmpty_LeavesOptionsUntouched()
    {
        var logger = new RecordingLogger<McpToolExposureConfigurator>();
        var options = CreateServerOptions(AlphaTool, BetaTool);

        CreateConfigurator(new XiHanMcpOptions(), logger).PostConfigure(null, options);

        Assert.NotNull(options.ToolCollection);
        Assert.Equal(2, options.ToolCollection.Count);
        Assert.Empty(options.Filters.Request.ListToolsFilters);
        Assert.Empty(options.Filters.Request.CallToolFilters);
        Assert.Empty(logger.Records);
    }

    /// <summary>
    /// 匹配不到任何工具的清单项记一条警告，点出每个这样的名字
    /// </summary>
    [Fact]
    public void PostConfigure_WithUnmatchedEntries_WarnsNamingThem()
    {
        var logger = new RecordingLogger<McpToolExposureConfigurator>();
        var policy = new XiHanMcpOptions
        {
            AllowedTools = [AlphaTool, "ghost_allowed"],
            DeniedTools = [AlphaTool.ToUpperInvariant()]
        };

        CreateConfigurator(policy, logger).PostConfigure(null, CreateServerOptions(AlphaTool, BetaTool));

        var record = Assert.Single(logger.Records);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("ghost_allowed", record.Message, StringComparison.Ordinal);
        Assert.Contains(AlphaTool.ToUpperInvariant(), record.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 清单项只在首次装配时核对：无状态传输每个请求都会重新装配选项，不能每次都告警
    /// </summary>
    [Fact]
    public void PostConfigure_CalledRepeatedly_WarnsOnlyOnce()
    {
        var logger = new RecordingLogger<McpToolExposureConfigurator>();
        var configurator = CreateConfigurator(new XiHanMcpOptions { DeniedTools = ["ghost_denied"] }, logger);

        configurator.PostConfigure(null, CreateServerOptions(AlphaTool));
        configurator.PostConfigure(null, CreateServerOptions(AlphaTool));

        _ = Assert.Single(logger.Records);
    }

    /// <summary>
    /// 清单项都匹配得上时不告警
    /// </summary>
    [Fact]
    public void PostConfigure_WithAllEntriesMatched_DoesNotWarn()
    {
        var logger = new RecordingLogger<McpToolExposureConfigurator>();
        var policy = new XiHanMcpOptions
        {
            AllowedTools = [AlphaTool, BetaTool],
            DeniedTools = [BetaTool]
        };

        CreateConfigurator(policy, logger).PostConfigure(null, CreateServerOptions(AlphaTool, BetaTool));

        Assert.Empty(logger.Records);
    }

    /// <summary>
    /// 以给定清单构造配置器
    /// </summary>
    /// <param name="policy">MCP 配置</param>
    /// <param name="logger">日志器</param>
    /// <returns>配置器</returns>
    private static McpToolExposureConfigurator CreateConfigurator(XiHanMcpOptions policy, ILogger<McpToolExposureConfigurator> logger)
    {
        return new McpToolExposureConfigurator(MsOptions.Create(policy), logger);
    }

    /// <summary>
    /// 构造一份工具集里装着给定名字工具的 MCP 服务端选项
    /// </summary>
    /// <param name="toolNames">工具名</param>
    /// <returns>MCP 服务端选项</returns>
    private static McpServerOptions CreateServerOptions(params string[] toolNames)
    {
        var options = new McpServerOptions { ToolCollection = [] };
        foreach (var toolName in toolNames)
        {
            options.ToolCollection.Add(McpServerTool.Create(AIFunctionFactory.Create(() => toolName, toolName)));
        }

        return options;
    }
}
