// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.OutOfProcess.Processes;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 插件输出的按行读取（ADR-0185）
/// </summary>
/// <remarks>
/// 行尾与 <see cref="StreamReader.ReadLine"/> 一致（<c>\n</c>、<c>\r\n</c>、单独的 <c>\r</c>），一行最多保留上限个字符
/// </remarks>
public sealed class PluginOutputReaderTests
{
    [Fact]
    public async Task LineEndings_MatchStreamReader()
    {
        var reader = new PluginOutputReader(new StringReader("a\nb\r\nc\rd\n\ne"), 100);

        Assert.Equal(["a", "b", "c", "d", "", "e"], await ReadAllAsync(reader, stopAtLimit: false));
    }

    [Fact]
    public async Task LongLine_KeepsThePrefix_AndTheNextLineIsIntact()
    {
        var reader = new PluginOutputReader(new StringReader(new string('x', 10_000) + "\r\nnext"), 8);

        Assert.Equal(((string, bool)?)("xxxxxxxx", true), await reader.ReadLineAsync(stopAtLimit: false, CancellationToken.None));
        Assert.Equal(((string, bool)?)("next", false), await reader.ReadLineAsync(stopAtLimit: false, CancellationToken.None));
        Assert.Null(await reader.ReadLineAsync(stopAtLimit: false, CancellationToken.None));
    }

    [Fact]
    public async Task StopAtLimit_ReturnsAsSoonAsTheLineIsTooLong()
    {
        // 流在超长之后没有行尾、也不结束：不能等下去
        var reader = new PluginOutputReader(new StreamReader(new EndlessStream()), 16);

        var line = await reader.ReadLineAsync(stopAtLimit: true, CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(((string, bool)?)(new string('z', 16), true), line);
    }

    /// <summary>
    /// 读到结尾，返回每行的文本
    /// </summary>
    private static async Task<List<string>> ReadAllAsync(PluginOutputReader reader, bool stopAtLimit)
    {
        var lines = new List<string>();
        while (await reader.ReadLineAsync(stopAtLimit, CancellationToken.None) is var (text, _))
        {
            lines.Add(text);
        }

        return lines;
    }

    /// <summary>
    /// 一直给出 <c>z</c>、从不结束的只读流
    /// </summary>
    private sealed class EndlessStream : Stream
    {
        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            buffer.AsSpan(offset, count).Fill((byte)'z');
            return count;
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
