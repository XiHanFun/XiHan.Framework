// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Mcp.Server;

namespace XiHan.Framework.Protocols.Mcp.Transports;

/// <summary>
/// MCP 的 stdio 传输。
/// </summary>
/// <remarks>
/// stdin 每行一条 JSON-RPC 消息，答复每条一行写到 stdout（不含换行的紧凑 JSON），通知不答复；输入结束即返回。每条消息在自己的服务作用域里处理，<c>tools/call</c> 以该作用域的新上下文经完整管道执行。上下文由调用方给出的工厂构造（<c>mcp</c> 命令按命令行选项与环境变量带上凭据、租户、区域与时限，见 <c>spec/protocol/mcp.md</c>）；不给工厂时是没有凭据与 Baggage 的空上下文。日志必须走 stderr，由宿主保证。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class McpStdioTransport
{
    /// <summary>
    /// 以空上下文运行到输入结束或取消。
    /// </summary>
    /// <remarks>
    /// 每条消息的上下文没有凭据与 Baggage，区域为默认区域。
    /// </remarks>
    /// <param name="server">MCP 服务端。</param>
    /// <param name="services">服务；每条消息从它创建作用域。</param>
    /// <param name="input">输入（stdin）。MCP 消息是 UTF-8，按 UTF-8 构造读取器；不要直接用 <see cref="Console.In"/>，它在 Windows 上按控制台代码页解码。</param>
    /// <param name="output">输出（stdout）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static Task RunAsync(McpServer server, IServiceProvider services, TextReader input, TextWriter output, CancellationToken cancellationToken)
        => RunAsync(server, services, input, output, scope => App.CreateContext(scope, cancellationToken), cancellationToken);

    /// <summary>
    /// 以给定的上下文工厂运行到输入结束或取消。
    /// </summary>
    /// <remarks>
    /// 工厂以每条消息的服务作用域调用：一次为这条消息本身（<c>tools/list</c> 的区域等），每个 <c>tools/call</c> 再调用一次。
    /// </remarks>
    /// <param name="server">MCP 服务端。</param>
    /// <param name="services">服务；每条消息从它创建作用域。</param>
    /// <param name="input">输入（stdin）。MCP 消息是 UTF-8，按 UTF-8 构造读取器；不要直接用 <see cref="Console.In"/>，它在 Windows 上按控制台代码页解码。</param>
    /// <param name="output">输出（stdout）。</param>
    /// <param name="createContext">以服务作用域构造调用上下文：会话的凭据、Baggage 与时限都在这里带上。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static async Task RunAsync(McpServer server, IServiceProvider services, TextReader input, TextWriter output, Func<IServiceProvider, InvocationContext> createContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(createContext);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                // 输入关闭：客户端结束会话
                return;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await using var scope = services.CreateAsyncScope();
            var context = createContext(scope.ServiceProvider);
            var reply = await server.HandleAsync(Encoding.UTF8.GetBytes(line), context, () => createContext(scope.ServiceProvider)).ConfigureAwait(false);
            if (reply.TryGetValue(out var response))
            {
                await output.WriteLineAsync(Encoding.UTF8.GetString(response.Body.Span)).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
