// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon.S3.Model;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 包住 <see cref="GetObjectResponse"/> 的响应流：读取转发给响应流，释放时连同响应一起释放（响应持有 HTTP 连接）。
/// </summary>
internal sealed class S3ObjectStream : Stream
{
    /// <summary>
    /// 被包住的响应。
    /// </summary>
    private readonly GetObjectResponse _response;

    /// <summary>
    /// 创建。
    /// </summary>
    public S3ObjectStream(GetObjectResponse response)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
    }

    /// <summary>
    /// 响应流。
    /// </summary>
    private Stream Inner => _response.ResponseStream;

    /// <inheritdoc />
    public override bool CanRead => Inner.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => Inner.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => Inner.Position;
        set => throw new NotSupportedException("The object stream is forward-only.");
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => Inner.Read(buffer);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Inner.ReadAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => Inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => Inner.CopyToAsync(destination, bufferSize, cancellationToken);

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("The object stream is forward-only.");

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException("The object stream is read-only.");

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("The object stream is read-only.");

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _response.Dispose();
        }

        base.Dispose(disposing);
    }
}
