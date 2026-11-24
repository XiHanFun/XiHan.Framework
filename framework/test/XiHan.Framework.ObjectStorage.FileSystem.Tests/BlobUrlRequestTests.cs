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
        Assert.Throws<ArgumentException>(static () => BlobUrlRequest.ForDownload(null, new string('a', BlobUrlRequest.MaxDownloadFileNameBytes + 1)));
        Assert.Equal(BlobUrlRequest.MaxDownloadFileNameBytes, BlobUrlRequest.ForDownload(null, new string('a', BlobUrlRequest.MaxDownloadFileNameBytes)).DownloadFileName!.Length);
    }

    [Fact]
    public void DownloadFileName_IsBoundedInUtf8Bytes_NotInCharacters()
    {
        // 上限按 UTF-8 字节计（ADR-0282）：85 个汉字正好 255 字节，再多一个字就超了；以前按码元计，255 个汉字（765 字节）也收
        Assert.Equal(255, BlobUrlRequest.MaxDownloadFileNameBytes);
        Assert.Equal(85, BlobUrlRequest.ForDownload(null, new string('名', 85)).DownloadFileName!.Length);
        Assert.Throws<ArgumentException>(static () => BlobUrlRequest.ForDownload(null, new string('名', 86)));
        Assert.Throws<ArgumentException>(static () => BlobUrlRequest.ForDownload(null, new string('名', 255)));

        // 全是空白的文件名不收，空格夹在中间照常可用
        Assert.Throws<ArgumentException>(static () => BlobUrlRequest.ForDownload(null, "   "));
        Assert.Throws<ArgumentException>(static () => BlobUrlRequest.ForDownload(null, string.Empty));
    }
}
