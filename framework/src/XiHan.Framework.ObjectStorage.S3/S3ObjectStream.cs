// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon.Runtime;
using Amazon.S3.Model;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 包住 <see cref="GetObjectResponse"/> 的响应流
/// </summary>
/// <remarks>
/// 读取转发给响应流，释放时连同响应一起释放（响应持有 HTTP 连接）。读取时 SDK 抛出的 <see cref="AmazonClientException"/> 经构造时给的转换换成对象存储的异常：SDK 整读时拿 ETag 当内容的 MD5、拿应答里的校验和头核对内容，读到末尾对不上就抛出它，这时元数据早已交出，读内容的流只能以异常报告（ADR-0322）。SDK 释放时也核对（已读的字节数等于应答声明的长度就算）：本流在读到末尾之后的释放照样换成异常抛出，没读到末尾的释放不抛出、只交给构造时给的回调记下（ADR-0333）。网络中断等其余异常原样抛出。不转发 <c>CopyToAsync</c>：基类的复制经本流的 <c>ReadAsync</c> 读，只换读时的异常，目标流写入时抛出的不受影响
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
    /// 没读到末尾就释放时 SDK 抛出的 <see cref="AmazonClientException"/> 交给它，不抛出
    /// </summary>
    private readonly Action<AmazonClientException> _discard;

    /// <summary>
    /// 是否读到了末尾
    /// </summary>
    /// <remarks>
    /// 有一次请求了至少一个字节的读返回 0 即为真。请求 0 个字节的读返回 0 不算：那不是末尾
    /// </remarks>
    private bool _ended;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="response">响应，归本流所有</param>
    /// <param name="translate">读取时（与读到末尾之后的释放时）SDK 抛出 <see cref="AmazonClientException"/> 即调用，抛出它的返回值</param>
    /// <param name="discard">没读到末尾就释放、SDK 抛出 <see cref="AmazonClientException"/> 时调用，不抛出</param>
    public S3ObjectStream(GetObjectResponse response, Func<AmazonClientException, Exception> translate, Action<AmazonClientException> discard)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
        _translate = translate ?? throw new ArgumentNullException(nameof(translate));
        _discard = discard ?? throw new ArgumentNullException(nameof(discard));
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
            return Track(Inner.Read(buffer, offset, count), count);
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
            return Track(Inner.Read(buffer), buffer.Length);
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
            return Track(await Inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false), count);
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
            return Track(await Inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false), buffer.Length);
        }
        catch (AmazonClientException exception)
        {
            throw _translate(exception);
        }
    }

    /// <summary>
    /// 记下这次读是否读到了末尾，原样交出读到的字节数
    /// </summary>
    private int Track(int read, int requested)
    {
        if (read == 0 && requested > 0)
        {
            _ended = true;
        }

        return read;
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
        catch (AmazonClientException exception) when (_ended)
        {
            // AWS SDK for .NET 4.0 在返回 0 的那次读里核对、核对过不再核对，读到末尾之后的释放实际不会走到这里；
            // SDK 改成只在释放时核对的话，读到末尾的核对失败照旧报告给调用方，不把 SDK 的类型漏给它（ADR-0322、ADR-0333）
            throw _translate(exception);
        }
        catch (AmazonClientException exception)
        {
            // 没读到末尾就释放：SDK 在已读的字节数等于声明的长度时核对，调用方读够了长度（如 ReadExactly）、没读到返回 0 就释放即在这里。
            // 不抛出：释放不抛是 .NET 的约定，没读到末尾也不算读完了内容；只交给回调记日志（ADR-0333）
            _discard(exception);
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
