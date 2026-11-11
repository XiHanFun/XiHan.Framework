// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Protocols.Mcp.Server;

namespace XiHan.Framework.Protocols.Mcp.Transports;

/// <summary>
/// 命令行宿主的 <c>mcp</c> 命令：以 stdio 传输提供 MCP，直到 stdin 关闭或取消
/// </summary>
/// <remarks>
/// 会话的上下文取自启动进程的命令行选项与环境变量，与 <c>invoke</c> 同一套映射（<c>--authorization</c> 或 <c>XIHAN_AUTHORIZATION</c>、<c>--tenant</c>、<c>--correlation</c>、<c>--locale</c>、<c>--timeout</c>），作用于每个 <c>tools/call</c>；选项不合法时不进入会话，按用法错误退出（<c>spec/protocol/mcp.md</c>）。标准输入的字节按 UTF-8 解码，与控制台代码页无关；答复只含 ASCII
/// </remarks>
internal sealed class McpStdioCommand : ICommand
{
    /// <summary>
    /// 打开输入的字节流
    /// </summary>
    /// <remarks>
    /// 执行时打开标准输入，测试经内部构造函数换成内存流
    /// </remarks>
    private readonly Func<Stream> _openInput;

    /// <summary>
    /// 创建读标准输入的命令
    /// </summary>
    public McpStdioCommand()
        : this(Console.OpenStandardInput)
    {
    }

    /// <summary>
    /// 创建从给定字节流读输入的命令
    /// </summary>
    internal McpStdioCommand(Func<Stream> openInput)
    {
        _openInput = openInput ?? throw new ArgumentNullException(nameof(openInput));
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

        // MCP 的 stdio 消息是 UTF-8：Console.In 在 Windows 上按控制台输入代码页（936、437 等）解码重定向的 stdin，中文参数会成乱码，
        // 所以直接按 UTF-8 解码标准输入的字节（Encoding.UTF8 带前导码，开头的 UTF-8 BOM 被跳过、不算内容；不按 BOM 改用别的编码）
        // 答复不另换编码：服务端写出的 JSON 把非 ASCII 字符都转义成 \uXXXX，只含 ASCII，经宿主给的输出按任何兼容 ASCII 的代码页写出都是同样的字节
        using var input = new StreamReader(_openInput(), Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        // 每条消息与每个 tools/call 都以同一组选项在自己的作用域里建上下文；来源由服务端改记 mcp 与 tool 名
        await McpStdioTransport.RunAsync(
            context.Services.GetRequiredService<McpServer>(),
            context.Services,
            input,
            context.Output,
            scope => CommandLineInvocation.Create(context, scope, Name).ValueOrThrow(),
            context.CancellationToken).ConfigureAwait(false);
        return CommandLineExitCodes.Success;
    }
}
