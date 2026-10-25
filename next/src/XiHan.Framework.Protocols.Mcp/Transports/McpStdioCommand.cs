// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Mcp.Server;

namespace XiHan.Framework.Protocols.Mcp.Transports;

/// <summary>
/// 命令行宿主的 <c>mcp</c> 命令：以 stdio 传输提供 MCP，直到 stdin 关闭或取消。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class McpStdioCommand : ICommand
{
    /// <inheritdoc />
    public string Name => "mcp";

    /// <inheritdoc />
    public string DescriptionKey => McpMessages.StdioDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await McpStdioTransport.RunAsync(context.Services.GetRequiredService<McpServer>(), context.Services, Console.In, context.Output, context.CancellationToken).ConfigureAwait(false);
        return CommandLineExitCodes.Success;
    }
}
