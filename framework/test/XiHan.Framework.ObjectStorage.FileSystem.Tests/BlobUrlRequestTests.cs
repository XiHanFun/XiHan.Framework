// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Signing;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 预签名请求的限定项不能带换行（ADR-0219）
/// </summary>
/// <remarks>
/// 宿主签名的规范串以换行分隔各限定项：值里带换行，就能把一项挪成另一项；S3 的响应头覆盖也会把它写进头
/// </remarks>
public sealed class BlobUrlRequestTests
{
    [Fact]
    public void ContentType_MustBeAMediaType()
    {
        Assert.Equal("text/plain; charset=utf-8", BlobUrlRequest.ForUpload(null, "text/plain; charset=utf-8").ContentType);
        Assert.Null(BlobUrlRequest.ForUpload(null, null).ContentType);
        Assert.Throws<ArgumentException>(static () => BlobUrlRequest.ForUpload(null, "text/plain\nx-filename"));
        Assert.Throws<ArgumentException>(static () => BlobUrlRequest.ForUpload(null, "plain"));
        Assert.Throws<ArgumentException>(static () => BlobUrlRequest.ForUpload(null, " "));
    }

    [Fact]
    public void DownloadFileName_HasNoControlCharacters_AndIsBounded()
    {
        Assert.Equal("报告 2026.pdf", BlobUrlRequest.ForDownload(null, "报告 2026.pdf").DownloadFileName);
        Assert.Throws<ArgumentException>(static () => BlobUrlRequest.ForDownload(null, "a.pdf\r\nSet-Cookie: x=1"));
        Assert.Throws<ArgumentException>(static () => BlobUrlRequest.ForDownload(null, "a\u0000.pdf"));
        Assert.Throws<ArgumentException>(static () => BlobUrlRequest.ForDownload(null, new string('a', BlobUrlRequest.MaxDownloadFileNameLength + 1)));
        Assert.Equal(BlobUrlRequest.MaxDownloadFileNameLength, BlobUrlRequest.ForDownload(null, new string('a', BlobUrlRequest.MaxDownloadFileNameLength)).DownloadFileName!.Length);
    }
}
