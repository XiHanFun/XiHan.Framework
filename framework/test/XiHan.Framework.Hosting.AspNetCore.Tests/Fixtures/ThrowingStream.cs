// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Hosting.AspNetCore.Tests.Fixtures;

/// <summary>
/// 先交出一段内容、再读就抛出指定异常的流
/// </summary>
/// <remarks>
/// 代替服务器在读请求体途中报错（如读到自己的上限时报 413）
/// </remarks>
/// <param name="failure">第二次读取时抛出的异常</param>
internal sealed class ThrowingStream(Exception failure) : Stream
{
    /// <summary>
    /// 是否已经交出过第一段
    /// </summary>
    private bool _served;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_served)
        {
            throw failure;
        }

        _served = true;
        var length = Math.Min(count, 16);
        Array.Clear(buffer, offset, length);
        return length;
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_served)
        {
            return ValueTask.FromException<int>(failure);
        }

        _served = true;
        var length = Math.Min(buffer.Length, 16);
        buffer.Span[..length].Clear();
        return ValueTask.FromResult(length);
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
