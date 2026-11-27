// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon.Runtime;
using Amazon.S3.Model;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 读内容的流在释放时怎样对待 SDK 的核对（ADR-0333）
/// </summary>
/// <remarks>
/// 没读到末尾（没有一次请求了至少一个字节的读返回 0）就释放时，SDK 抛出的 <see cref="AmazonClientException"/> 交给回调、不抛出；读到末尾之后的释放照旧换成转换出的异常抛出。AWS SDK for .NET 4.0 在返回 0 的那次读里核对、核对过不再核对，读到末尾之后的释放实际不抛：这里用释放时抛出的替身响应流钉住本流自己的判断，不靠 SDK 的时机。真实 SDK 的行为由 <see cref="S3WholeReadETagTests"/> 在线上核对
/// </remarks>
public sealed class S3ObjectStreamTests
{
    [Fact]
    public void Released_BeforeTheEnd_HandsTheCheckToTheCallback_AndDoesNotThrow()
    {
        var discarded = new List<AmazonClientException>();
        var stream = Stream(new ThrowingOnDispose([1, 2, 3]), discarded);

        Assert.Equal(3, stream.Read(new byte[3], 0, 3));
        stream.Dispose();

        Assert.Single(discarded);
    }

    [Fact]
    public void Released_AfterAZeroByteRequest_IsStillBeforeTheEnd()
    {
        // 请求 0 个字节的读返回 0 不是末尾
        var discarded = new List<AmazonClientException>();
        var stream = Stream(new ThrowingOnDispose([1]), discarded);

        Assert.Equal(0, stream.Read([], 0, 0));
        Assert.Equal(0, stream.Read(Span<byte>.Empty));
        stream.Dispose();

        Assert.Single(discarded);
    }

    [Theory]
    [InlineData("array")]
    [InlineData("span")]
    [InlineData("memory")]
    public async Task Released_AfterTheEnd_ThrowsTheTranslatedException(string reading)
    {
        var discarded = new List<AmazonClientException>();
        var stream = Stream(new ThrowingOnDispose([1]), discarded);
        var buffer = new byte[4];

        Assert.Equal(1, stream.Read(buffer, 0, 4));
        var last = reading switch
        {
            "array" => stream.Read(buffer, 0, 4),
            "span" => stream.Read(buffer.AsSpan()),
            _ => await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None),
        };

        Assert.Equal(0, last);
        Assert.Throws<InvalidDataException>(stream.Dispose);
        Assert.Empty(discarded);
    }

    [Fact]
    public void Released_WithoutACheck_DoesNothingElse()
    {
        var discarded = new List<AmazonClientException>();
        var inner = new MemoryStream([1, 2]);
        var stream = Stream(inner, discarded);

        stream.Dispose();

        Assert.Empty(discarded);
        Assert.False(inner.CanRead);
    }

    /// <summary>
    /// 包住 <paramref name="inner"/> 的读内容的流：转换出 <see cref="InvalidDataException"/>，丢弃的记进 <paramref name="discarded"/>
    /// </summary>
    private static S3ObjectStream Stream(Stream inner, List<AmazonClientException> discarded)
        => new(new GetObjectResponse { ResponseStream = inner }, static exception => new InvalidDataException("translated", exception), discarded.Add);

    /// <summary>
    /// 释放时抛出 <see cref="AmazonClientException"/> 的响应流
    /// </summary>
    /// <remarks>
    /// 模拟 SDK 在释放时核对出内容对不上；读照常
    /// </remarks>
    private sealed class ThrowingOnDispose(byte[] content) : MemoryStream(content)
    {
        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            throw new AmazonClientException("Expected hash not equal to calculated hash");
        }
    }
}
