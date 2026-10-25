// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Mcp.Server;

namespace XiHan.Framework.Protocols.Mcp.Transports;

/// <summary>
/// MCP 的 stdio 传输：stdin 每行一条 JSON-RPC 消息，答复每条一行写到 stdout（不含换行的紧凑 JSON），通知不答复；输入结束即返回。
/// 每条消息在自己的服务作用域里处理，<c>tools/call</c> 以该作用域的新上下文经完整管道执行；区域取进程默认区域。日志必须走 stderr，由宿主保证。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class McpStdioTransport
{
    /// <summary>
    /// 运行到输入结束或取消。
    /// </summary>
    /// <param name="server">MCP 服务端。</param>
    /// <param name="services">服务；每条消息从它创建作用域。</param>
    /// <param name="input">输入（stdin）。</param>
    /// <param name="output">输出（stdout）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static async Task RunAsync(McpServer server, IServiceProvider services, TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
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
            var context = App.CreateContext(scope.ServiceProvider, cancellationToken);
            var reply = await server.HandleAsync(Encoding.UTF8.GetBytes(line), context, () => App.CreateContext(scope.ServiceProvider, cancellationToken)).ConfigureAwait(false);
            if (reply.TryGetValue(out var response))
            {
                await output.WriteLineAsync(Encoding.UTF8.GetString(response.Body.Span)).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
