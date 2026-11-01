// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Cli.Integration;
using XiHan.Framework.Cli.Knowledge;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Mcp;
using XiHan.Framework.Protocols.Mcp.Server;
using XiHan.Framework.Protocols.Mcp.Transports;

namespace XiHan.Framework.Cli.Commands;

/// <summary>
/// 命令 <c>mcp</c>：以 stdio 提供框架知识库的 MCP 服务。
/// </summary>
/// <remarks>
/// 用法：<c>mcp [--path &lt;项目目录&gt;] [--locale &lt;区域&gt;]</c>。内嵌一个最小的 XiHan 应用（内存本地化 + <c>Protocols.Mcp</c> + <see cref="KnowledgeFeature"/>），由 <c>Protocols.Mcp</c> 的 stdio 传输处理协议，直到 stdin 关闭或取消；tool 的描述按 <c>--locale</c> 或当前 UI 文化给出。项目引用的框架版本与本工具不同时在 stderr 与服务端说明里提示。
/// </remarks>
internal sealed class McpCommand : ICommand
{
    /// <summary>
    /// 输入。
    /// </summary>
    /// <remarks>
    /// 为空时执行时以 UTF-8 读标准输入，测试换成内存读取器。
    /// </remarks>
    private readonly TextReader? _input;

    /// <summary>
    /// 创建。
    /// </summary>
    public McpCommand(TextReader? input)
    {
        _input = input;
    }

    /// <inheritdoc />
    public string Name => "mcp";

    /// <inheritdoc />
    public string DescriptionKey => CliMessages.McpDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var start = Path.GetFullPath(context.Option("path").TryGetValue(out var explicitPath) ? explicitPath : ".");
        var project = ProjectDetector.Detect(start);
        string? notice = null;
        if (project.FrameworkVersion is { } referenced && !string.Equals(referenced, FrameworkKnowledge.Version, StringComparison.Ordinal))
        {
            // stdout 只能有协议消息，提示写 stderr，同时放进服务端说明让编码助手也看得到
            notice = CliMessages.McpVersionMismatch.L(("project", referenced), ("version", FrameworkKnowledge.Version));
            await context.Error.WriteLineAsync(notice.AsMemory(), context.CancellationToken).ConfigureAwait(false);
        }

        var locale = context.Services.GetRequiredService<IMessageLocalizer>().Options.DefaultLocale;
        var instructions = CliMessages.McpInstructions.L(("version", FrameworkKnowledge.Version)) + (notice is null ? string.Empty : "\n" + notice);
        using var host = CreateHost(FrameworkKnowledge.Load(), locale, instructions);
        await host.StartAsync(context.CancellationToken).ConfigureAwait(false);
        try
        {
            // 标准输入按 UTF-8 读：Windows 上重定向的 Console.In 用 OEM 代码页解码，中文检索词会变成乱码
            using var stdin = _input is null ? new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)) : null;
            await McpStdioTransport.RunAsync(host.Services.GetRequiredService<McpServer>(), host.Services, _input ?? stdin!, context.Output, context.CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // 停止不受会话取消影响：特性按顺序正常关闭
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }

        return CommandLineExitCodes.Success;
    }

    /// <summary>
    /// 内嵌应用：服务端名、tool 名、说明与区域都由配置给出。
    /// </summary>
    /// <remarks>
    /// 用空的宿主构建器：不读当前目录的 appsettings 与环境变量里的 <c>XiHan</c> 配置，不注册控制台日志（stdout 只留给协议消息）。
    /// </remarks>
    internal static IHost CreateHost(FrameworkKnowledge knowledge, string locale, string instructions)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { ApplicationName = KnowledgeFeature.ServerName });
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [McpProtocolOptions.ConfigurationSection + ":ServerName"] = KnowledgeFeature.ServerName,
            [McpProtocolOptions.ConfigurationSection + ":Instructions"] = instructions,
            [LocalizationOptions.ConfigurationSection + ":DefaultLocale"] = locale,
        };
        foreach (var (operation, tool) in KnowledgeFeature.Tools)
        {
            settings[McpProtocolOptions.ConfigurationSection + ":Tools:" + operation] = tool;
        }

        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton(knowledge);
        builder.AddXiHan(static xihan => xihan
            .UseFeature<InMemoryLocalizationFeature>()
            .UseFeature<McpProtocolFeature>()
            .UseFeature<KnowledgeFeature>());
        return builder.Build();
    }
}
