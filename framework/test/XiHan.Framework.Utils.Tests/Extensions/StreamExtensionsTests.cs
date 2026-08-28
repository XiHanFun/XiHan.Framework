// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// 文件流扩展方法测试
/// </summary>
/// <remarks>
/// 不落盘：非 MemoryStream 的分支用手写的只读单向流覆盖。
/// CopyToAsync 必须走静态调用——Stream 自带同签名实例方法，扩展方法在实例语法下永远拿不到。
/// </remarks>
public class StreamExtensionsTests
{
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("曦寒框架 XiHan");

    /// <summary>
    /// 内存流走快捷路径，直接返回全部字节
    /// </summary>
    [Fact]
    public void GetAllBytes_OnMemoryStream_ReturnsWholeBuffer()
    {
        using var stream = new MemoryStream(Payload);

        Assert.Equal(Payload, stream.GetAllBytes());
    }

    /// <summary>
    /// 非内存流通过中转内存流读出全部字节
    /// </summary>
    [Fact]
    public void GetAllBytes_OnNonSeekableStream_ReadsEverything()
    {
        using var stream = new ForwardOnlyStream(Payload);

        Assert.Equal(Payload, stream.GetAllBytes());
    }

    /// <summary>
    /// 空流读出空字节数组
    /// </summary>
    [Fact]
    public void GetAllBytes_OnEmptyStream_ReturnsEmptyArray()
    {
        using var stream = new ForwardOnlyStream([]);

        Assert.Empty(stream.GetAllBytes());
    }

    /// <summary>
    /// 异步读取内存流得到全部字节
    /// </summary>
    [Fact]
    public async Task GetAllBytesAsync_OnMemoryStream_ReturnsWholeBuffer()
    {
        using var stream = new MemoryStream(Payload);

        var bytes = await stream.GetAllBytesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Payload, bytes);
    }

    /// <summary>
    /// 异步读取非内存流得到全部字节
    /// </summary>
    [Fact]
    public async Task GetAllBytesAsync_OnNonSeekableStream_ReadsEverything()
    {
        using var stream = new ForwardOnlyStream(Payload);

        var bytes = await stream.GetAllBytesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Payload, bytes);
    }

    /// <summary>
    /// 创建内存流后源流与新流的位置都被复位到起点
    /// </summary>
    [Fact]
    public void CreateMemoryStream_RewindsBothStreams()
    {
        using var source = new MemoryStream(Payload);
        source.Position = 3;

        using var copy = source.CreateMemoryStream();

        Assert.Equal(0, source.Position);
        Assert.Equal(0, copy.Position);
        Assert.Equal(Payload, copy.ToArray());
    }

    /// <summary>
    /// 异步创建内存流同样复位位置并复制全部内容
    /// </summary>
    [Fact]
    public async Task CreateMemoryStreamAsync_RewindsBothStreams()
    {
        using var source = new MemoryStream(Payload);
        source.Position = 3;

        using var copy = await source.CreateMemoryStreamAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, source.Position);
        Assert.Equal(0, copy.Position);
        Assert.Equal(Payload, copy.ToArray());
    }

    /// <summary>
    /// 复制前会把可定位的源流复位，因此即使已读到末尾也能复制完整内容
    /// </summary>
    [Fact]
    public async Task CopyToAsync_RewindsSeekableSourceBeforeCopying()
    {
        using var source = new MemoryStream(Payload);
        source.Position = source.Length;
        using var destination = new MemoryStream();

        await StreamExtensions.CopyToAsync(source, destination, TestContext.Current.CancellationToken);

        Assert.Equal(Payload, destination.ToArray());
    }

    /// <summary>
    /// 只读单向流：用于覆盖"非 MemoryStream 且不可定位"的分支
    /// </summary>
    private sealed class ForwardOnlyStream : Stream
    {
        private readonly MemoryStream _inner;

        public ForwardOnlyStream(byte[] data)
        {
            _inner = new MemoryStream(data);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
