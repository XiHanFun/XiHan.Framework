// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Mcp.Server;

namespace XiHan.Framework.Protocols.Mcp.Transports;

/// <summary>
/// 命令行宿主的 <c>mcp</c> 命令：以 stdio 传输提供 MCP，直到 stdin 关闭或取消。
/// </summary>
/// <remarks>
/// 会话的上下文取自启动进程的命令行选项与环境变量，与 <c>invoke</c> 同一套映射（<c>--authorization</c> 或 <c>XIHAN_AUTHORIZATION</c>、<c>--tenant</c>、<c>--correlation</c>、<c>--locale</c>、<c>--timeout</c>），作用于每个 <c>tools/call</c>；选项不合法时不进入会话，按用法错误退出（<c>spec/protocol/mcp.md</c>）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class McpStdioCommand : ICommand
{
    /// <summary>
    /// 输入。
    /// </summary>
    /// <remarks>
    /// 为空时执行时取 <see cref="Console.In"/>，测试经内部构造函数换成内存读取器。
    /// </remarks>
    private readonly TextReader? _input;

    /// <summary>
    /// 创建读标准输入的命令。
    /// </summary>
    public McpStdioCommand()
    {
    }

    /// <summary>
    /// 创建读给定输入的命令。
    /// </summary>
    internal McpStdioCommand(TextReader input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    /// <inheritdoc />
    public string Name => "mcp";

    /// <inheritdoc />
    public string DescriptionKey => McpMessages.StdioDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 会话选项先校验一次：凭据格式或时限不合法时不进入会话、不读 stdin，按用法错误退出
        var probe = CommandLineInvocation.Create(context, Name);
        if (!probe.IsSuccess)
        {
            return await CommandLineFailure.WriteAsync(context.Error, probe.Error!, context.Services, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
        }

        // 每条消息与每个 tools/call 都以同一组选项在自己的作用域里建上下文；来源由服务端改记 mcp 与 tool 名
        await McpStdioTransport.RunAsync(
            context.Services.GetRequiredService<McpServer>(),
            context.Services,
            _input ?? Console.In,
            context.Output,
            scope => CommandLineInvocation.Create(context, scope, Name).ValueOrThrow(),
            context.CancellationToken).ConfigureAwait(false);
        return CommandLineExitCodes.Success;
    }
}
