// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using XiHan.Framework.Web.Mcp.Options;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace XiHan.Framework.Web.Mcp.Tests.Options;

/// <summary>
/// 工具暴露配置器：清单的合法性校验
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

        var configurator = CreateConfigurator(policy);

        var exception = Assert.Throws<InvalidOperationException>(() => configurator.PostConfigure(null, CreateServerOptions(AlphaTool)));

        Assert.Contains($"{XiHanMcpOptions.SectionName}:{listName}:1", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 以给定清单构造配置器
    /// </summary>
    /// <param name="policy">MCP 配置</param>
    /// <returns>配置器</returns>
    private static McpToolExposureConfigurator CreateConfigurator(XiHanMcpOptions policy)
    {
        return new McpToolExposureConfigurator(MsOptions.Create(policy));
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
