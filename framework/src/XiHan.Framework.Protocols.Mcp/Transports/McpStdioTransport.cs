// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text;
using System.Text.Unicode;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Mcp.Server;

namespace XiHan.Framework.Protocols.Mcp.Transports;

/// <summary>
/// MCP 的 stdio 传输
/// </summary>
/// <remarks>
/// stdin 每行一条 JSON-RPC 消息，答复每条一行写到 stdout（不含换行的紧凑 JSON），通知不答复；输入结束即返回。每条消息在自己的服务作用域里处理，<c>tools/call</c> 以该作用域的新上下文经完整管道执行。上下文由调用方给出的工厂构造（<c>mcp</c> 命令按命令行选项与环境变量带上凭据、租户、区域与时限，见 <c>spec/protocol/mcp.md</c>）；不给工厂时是没有凭据与 Baggage 的空上下文。日志必须走 stderr，由宿主保证。
/// 输入按字节读：先分行（LF、CR 或 CR LF 结束一行，开头的 UTF-8 BOM 不算内容），再把每一行严格按 UTF-8 解码，非法的 UTF-8 与不是 JSON 同样答复解析错误（-32700）、接着读下一行，不换成 U+FFFD 再处理（RFC-0001「写出与转交」，ADR-0320）。
/// 一行不超过 <see cref="McpProtocolOptions.MaxStdioLineBytes"/> 个字节（不含行尾与开头的 BOM）：超过的一行读到行尾为止丢弃、不缓冲，同样答复解析错误、接着读下一行；每读一块之前核对取消，取消时抛出 <see cref="OperationCanceledException"/>（ADR-0331）。
/// 解码之后的每一行经服务端整条过 JSON 文本的共同检查（ADR-0331）。
/// 与控制台代码页无关：<see cref="Console.In"/> 在 Windows 上按控制台输入代码页（936、437 等）解码重定向的标准输入，所以收的是字节流（如 <see cref="Console.OpenStandardInput()"/>）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class McpStdioTransport
{
    /// <summary>
    /// 以空上下文运行到输入结束或取消
    /// </summary>
    /// <remarks>
    /// 每条消息的上下文没有凭据与 Baggage，区域为默认区域
    /// </remarks>
    /// <param name="server">MCP 服务端</param>
    /// <param name="services">服务；每条消息从它创建作用域</param>
    /// <param name="input">输入（stdin）的字节流，如 <see cref="Console.OpenStandardInput()"/>；由调用方释放</param>
    /// <param name="output">输出（stdout）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static Task RunAsync(McpServer server, IServiceProvider services, Stream input, TextWriter output, CancellationToken cancellationToken)
        => RunAsync(server, services, input, output, scope => App.CreateContext(scope, cancellationToken), cancellationToken);

    /// <summary>
    /// 以给定的上下文工厂运行到输入结束或取消
    /// </summary>
    /// <remarks>
    /// 工厂以每条消息的服务作用域调用：一次为这条消息本身（<c>tools/list</c> 的区域等），每个 <c>tools/call</c> 再调用一次
    /// </remarks>
    /// <param name="server">MCP 服务端</param>
    /// <param name="services">服务；每条消息从它创建作用域</param>
    /// <param name="input">输入（stdin）的字节流，如 <see cref="Console.OpenStandardInput()"/>；由调用方释放</param>
    /// <param name="output">输出（stdout）</param>
    /// <param name="createContext">以服务作用域构造调用上下文：会话的凭据、Baggage 与时限都在这里带上</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task RunAsync(McpServer server, IServiceProvider services, Stream input, TextWriter output, Func<IServiceProvider, InvocationContext> createContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(createContext);
        var lines = new LineReader(input, server.Options.MaxStdioLineBytes);
        while (!cancellationToken.IsCancellationRequested)
        {
            var (message, tooLong) = await lines.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (tooLong)
            {
                // 超过上限的一行已经读到行尾丢弃：与非法的 UTF-8 同一个答复，会话继续（protocol/mcp.md，ADR-0331）
                await WriteAsync(output, McpServer.NotJsonReply(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (message is null)
            {
                // 输入关闭：客户端结束会话
                return;
            }

            // 一行按字节读进来再严格解码：非法的 UTF-8 与不是 JSON 同样答复解析错误、接着读下一行，不换成 U+FFFD 再处理（protocol/mcp.md，ADR-0320）。
            // 先核对再判空白：判空白要按字符解码
            if (!Utf8.IsValid(message))
            {
                await WriteAsync(output, McpServer.NotJsonReply(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (IsBlank(message))
            {
                continue;
            }

            await using var scope = services.CreateAsyncScope();
            var context = createContext(scope.ServiceProvider);
            var reply = await server.HandleAsync(message, context, () => createContext(scope.ServiceProvider)).ConfigureAwait(false);
            if (reply.TryGetValue(out var response))
            {
                await WriteAsync(output, response, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 一行是否只有空白
    /// </summary>
    /// <remarks>
    /// 空白照 RFC-0001「空白字符」（与 <see cref="char.IsWhiteSpace(char)"/> 同一集合），与以前按文本读时的 <see cref="string.IsNullOrWhiteSpace"/> 相同；调用方已核对过是合法的 UTF-8
    /// </remarks>
    private static bool IsBlank(ReadOnlySpan<byte> line)
    {
        while (!line.IsEmpty)
        {
            _ = Rune.DecodeFromUtf8(line, out var rune, out var consumed);
            if (!Rune.IsWhiteSpace(rune))
            {
                return false;
            }

            line = line[consumed..];
        }

        return true;
    }

    /// <summary>
    /// 把一个答复写成一行
    /// </summary>
    /// <remarks>
    /// 答复由服务端的写出器写成，总是合法的 UTF-8
    /// </remarks>
    private static async Task WriteAsync(TextWriter output, McpReply reply, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync(Encoding.UTF8.GetString(reply.Body.Span)).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 按字节分行的读取器
    /// </summary>
    /// <remarks>
    /// LF、CR 或 CR LF 结束一行，行尾不算内容，最后一行可以没有行尾，与以前用的 <see cref="StreamReader.ReadLineAsync(CancellationToken)"/> 分行相同；整个输入开头的 UTF-8 BOM 去掉。
    /// 一行最多缓冲 <c>maxLineBytes</c> 个字节（第一行另加 BOM 的三个），超过即丢弃已缓冲的部分，其余的字节读到行尾为止不再缓冲（ADR-0331；以前不设上限）
    /// </remarks>
    /// <param name="input">输入的字节流</param>
    /// <param name="maxLineBytes">一行的上限（字节），不含行尾与开头的 BOM</param>
    private sealed class LineReader(Stream input, int maxLineBytes)
    {
        /// <summary>
        /// UTF-8 BOM 的字节数
        /// </summary>
        private const int ByteOrderMarkLength = 3;

        /// <summary>
        /// 一次读入的字节
        /// </summary>
        private readonly byte[] _chunk = new byte[4096];

        /// <summary>
        /// 正在拼的一行
        /// </summary>
        private readonly ArrayBufferWriter<byte> _line = new();

        /// <summary>
        /// <see cref="_chunk"/> 里还没交出的字节的起点
        /// </summary>
        private int _start;

        /// <summary>
        /// <see cref="_chunk"/> 里读入的字节的终点
        /// </summary>
        private int _end;

        /// <summary>
        /// 上一行以 CR 结束：紧跟的 LF 属于它
        /// </summary>
        private bool _afterCr;

        /// <summary>
        /// 还没交出过任何一行：开头的 BOM 只在这一行里去掉
        /// </summary>
        private bool _first = true;

        /// <summary>
        /// 读下一行
        /// </summary>
        /// <remarks>
        /// 每读一块之前核对取消：丢弃超长的一行可能要读很多块，不认取消令牌的输入（如控制台的标准输入）也要停得下来
        /// </remarks>
        /// <returns>一行的字节，不含行尾，输入结束时为 <see langword="null"/>；这一行超过上限时字节为 <see langword="null"/>、<c>TooLong</c> 为真</returns>
        public async ValueTask<(byte[]? Line, bool TooLong)> ReadLineAsync(CancellationToken cancellationToken)
        {
            _line.ResetWrittenCount();
            var tooLong = false;
            while (true)
            {
                if (_start == _end)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _start = 0;
                    _end = await input.ReadAsync(_chunk, cancellationToken).ConfigureAwait(false);
                    if (_end == 0)
                    {
                        // 输入在超长的一行里结束：这一行照样答复一次，下一次读到输入结束
                        if (tooLong)
                        {
                            _first = false;
                            return (null, true);
                        }

                        // 输入结束：没有拼到字节就没有这一行，"a\n" 之后不多出一个空行
                        return _line.WrittenCount == 0 ? (null, false) : Take();
                    }
                }

                var available = _chunk.AsSpan(_start, _end - _start);
                if (_afterCr)
                {
                    // CR 与 LF 可能分在两次读入里
                    _afterCr = false;
                    if (available[0] == (byte)'\n')
                    {
                        _start++;
                        continue;
                    }
                }

                var index = available.IndexOfAny((byte)'\r', (byte)'\n');
                var content = index < 0 ? available : available[..index];
                if (!tooLong)
                {
                    // 写之前先比剩下的余量：一块最多 4 KiB，先写再比会多缓冲一块。第一行的 BOM 不算内容，多留它的三个字节，交出时再按去掉 BOM 之后的长度核对
                    var limit = _first ? maxLineBytes + ByteOrderMarkLength : maxLineBytes;
                    if (content.Length > limit - _line.WrittenCount)
                    {
                        // 超过上限：已缓冲的部分不再保留，余下的字节只找行尾、不再缓冲
                        tooLong = true;
                        _line.ResetWrittenCount();
                    }
                    else
                    {
                        _line.Write(content);
                    }
                }

                if (index < 0)
                {
                    _start = _end;
                    continue;
                }

                _afterCr = available[index] == (byte)'\r';
                _start += index + 1;
                if (tooLong)
                {
                    _first = false;
                    return (null, true);
                }

                return Take();
            }
        }

        /// <summary>
        /// 交出拼好的一行
        /// </summary>
        /// <remarks>
        /// 复制一份：服务端处理这一行时读取器不会再写，但交出的字节不随下一行变。第一行去掉 BOM 之后仍超过上限的（缓冲时为 BOM 多留了三个字节）同样超长
        /// </remarks>
        private (byte[]? Line, bool TooLong) Take()
        {
            var line = _line.WrittenSpan;
            if (_first)
            {
                _first = false;
                if (line.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
                {
                    line = line[ByteOrderMarkLength..];
                }
            }

            return line.Length > maxLineBytes ? (null, true) : (line.ToArray(), false);
        }
    }
}
