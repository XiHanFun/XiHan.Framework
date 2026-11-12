// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;

namespace XiHan.Framework.Plugins.OutOfProcess.Processes;

/// <summary>
/// 按行读插件输出，一行最多保留给定的字符数
/// </summary>
/// <remarks>
/// 插件不可信（ADR-0185）：超过上限的部分边读边丢弃，不进内存。行尾认 <c>\n</c>、<c>\r\n</c> 与单独的 <c>\r</c>。不是线程安全的，同一时刻只有一个读取方
/// </remarks>
internal sealed class PluginOutputReader(TextReader reader, int maxChars)
{
    /// <summary>
    /// 读缓冲
    /// </summary>
    private readonly char[] _buffer = new char[4096];

    /// <summary>
    /// 缓冲里未消费部分的起点
    /// </summary>
    private int _start;

    /// <summary>
    /// 缓冲里有效内容的终点
    /// </summary>
    private int _end;

    /// <summary>
    /// 已读到流的结尾
    /// </summary>
    private bool _ended;

    /// <summary>
    /// 上一行以 <c>\r</c> 结束：紧跟的 <c>\n</c> 属于同一个行尾
    /// </summary>
    private bool _afterCarriageReturn;

    /// <summary>
    /// 读一行
    /// </summary>
    /// <remarks>
    /// 流结束且没有内容时返回 <see langword="null"/>。<paramref name="stopAtLimit"/> 为真时超过上限即返回已保留的部分（握手行用：超长就不合法，不必等到行尾）；否则读掉并丢弃到行尾，下次从下一行开始
    /// </remarks>
    public async ValueTask<(string Text, bool Truncated)?> ReadLineAsync(bool stopAtLimit, CancellationToken cancellationToken)
    {
        var line = new StringBuilder();
        var truncated = false;
        while (true)
        {
            if (_start == _end)
            {
                if (_ended)
                {
                    return line.Length > 0 || truncated ? (line.ToString(), truncated) : null;
                }

                _start = 0;
                _end = await reader.ReadAsync(_buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                _ended = _end == 0;
                continue;
            }

            if (_afterCarriageReturn)
            {
                _afterCarriageReturn = false;
                if (_buffer[_start] == '\n')
                {
                    _start++;
                    continue;
                }
            }

            var newline = Array.FindIndex(_buffer, _start, _end - _start, static c => c is '\n' or '\r');
            var stop = newline < 0 ? _end : newline;
            truncated |= Keep(line, stop - _start);
            if (newline >= 0)
            {
                _afterCarriageReturn = _buffer[newline] == '\r';
                _start = newline + 1;
                return (line.ToString(), truncated);
            }

            _start = _end;
            if (truncated && stopAtLimit)
            {
                return (line.ToString(), true);
            }
        }
    }

    /// <summary>
    /// 把缓冲里从起点开始的 <paramref name="count"/> 个字符留进行里，超出上限的不留
    /// </summary>
    /// <remarks>
    /// 返回这一段有没有被截掉
    /// </remarks>
    private bool Keep(StringBuilder line, int count)
    {
        var room = Math.Max(0, maxChars - line.Length);
        line.Append(_buffer, _start, Math.Min(count, room));
        return count > room;
    }
}
