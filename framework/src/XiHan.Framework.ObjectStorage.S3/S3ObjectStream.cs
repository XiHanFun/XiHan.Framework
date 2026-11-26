// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon.Runtime;
using Amazon.S3.Model;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 包住 <see cref="GetObjectResponse"/> 的响应流
/// </summary>
/// <remarks>
/// 读取转发给响应流，释放时连同响应一起释放（响应持有 HTTP 连接）。读取与释放时 SDK 抛出的 <see cref="AmazonClientException"/> 经构造时给的转换换成对象存储的异常：SDK 整读时拿 ETag 当内容的 MD5、拿应答里的校验和头核对内容，读到末尾对不上就抛出它，这时元数据早已交出，读内容的流只能以异常报告（ADR-0322）。网络中断等其余异常原样抛出。不转发 <c>CopyToAsync</c>：基类的复制经本流的 <c>ReadAsync</c> 读，只换读时的异常，目标流写入时抛出的不受影响
/// </remarks>
internal sealed class S3ObjectStream : Stream
{
    /// <summary>
    /// 被包住的响应
    /// </summary>
    private readonly GetObjectResponse _response;

    /// <summary>
    /// 把读取时 SDK 抛出的 <see cref="AmazonClientException"/> 换成要抛给调用方的异常
    /// </summary>
    private readonly Func<AmazonClientException, Exception> _translate;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="response">响应，归本流所有</param>
    /// <param name="translate">读取时 SDK 抛出 <see cref="AmazonClientException"/> 即调用，抛出它的返回值</param>
    public S3ObjectStream(GetObjectResponse response, Func<AmazonClientException, Exception> translate)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
        _translate = translate ?? throw new ArgumentNullException(nameof(translate));
    }

    /// <summary>
    /// 响应流
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
    public override int Read(byte[] buffer, int offset, int count)
    {
        try
        {
            return Inner.Read(buffer, offset, count);
        }
        catch (AmazonClientException exception)
        {
            throw _translate(exception);
        }
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        try
        {
            return Inner.Read(buffer);
        }
        catch (AmazonClientException exception)
        {
            throw _translate(exception);
        }
    }

    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        try
        {
            return await Inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonClientException exception)
        {
            throw _translate(exception);
        }
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonClientException exception)
        {
            throw _translate(exception);
        }
    }

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
        try
        {
            if (disposing)
            {
                _response.Dispose();
            }
        }
        catch (AmazonClientException exception)
        {
            // SDK 释放整读的响应时，读到的长度等于应答声明的长度就核对 MD5：调用方读够了长度、没读到流的末尾就释放时，
            // 核对在这里才发生。同读时一样换成对象存储的异常，不把 SDK 的类型漏给调用方（ADR-0322）
            throw _translate(exception);
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
