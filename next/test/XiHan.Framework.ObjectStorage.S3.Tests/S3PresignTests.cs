// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// S3 原生预签名。
/// </summary>
/// <remarks>
/// 签出的地址不带凭据直连真实的 S3 兼容服务——下载（带保存文件名、可带 <c>Range</c>）、限定内容类型的上传、分片上传的每一片；未设置 <c>XIHAN_TEST_S3</c> 时跳过。
/// </remarks>
public sealed class S3PresignTests
{
    /// <summary>
    /// 用例的桶。
    /// </summary>
    private const string Bucket = "presign";

    /// <summary>
    /// 直连存储的客户端：不带任何凭据。
    /// </summary>
    private static readonly HttpClient _http = new();

    [Fact]
    public async Task SignedUrls_DownloadAndUpload_WithoutCredentials()
    {
        var store = S3TestServer.CreateStore(Bucket);
        var key = Guid.NewGuid().ToString("N")[..8] + "/report.txt";
        Assert.True((await store.PutAsync(Bucket, key, Encoding.UTF8.GetBytes("0123456789"), new BlobPutOptions { ContentType = "text/plain" }, CancellationToken.None)).IsSuccess);

        // 下载：整读带保存文件名，范围请求 206
        var download = (await store.SignAsync(Bucket, key, BlobUrlRequest.ForDownload(downloadFileName: "报告.txt"), CancellationToken.None)).ValueOrThrow();
        Assert.True(download.Url.IsAbsoluteUri);
        using (var whole = await _http.GetAsync(download.Url))
        {
            Assert.Equal(HttpStatusCode.OK, whole.StatusCode);
            Assert.Equal("0123456789", await whole.Content.ReadAsStringAsync());
            Assert.Contains("filename*=UTF-8''%E6%8A%A5%E5%91%8A.txt", whole.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
        }

        using (var request = new HttpRequestMessage(HttpMethod.Get, download.Url) { Headers = { Range = new RangeHeaderValue(2, 4) } })
        using (var ranged = await _http.SendAsync(request))
        {
            Assert.Equal((HttpStatusCode.PartialContent, "234"), (ranged.StatusCode, await ranged.Content.ReadAsStringAsync()));
        }

        // 上传：限定内容类型，请求带上签出的头即成功，存进去的内容类型就是它
        var target = Guid.NewGuid().ToString("N")[..8] + "/upload.md";
        var upload = (await store.SignAsync(Bucket, target, BlobUrlRequest.ForUpload(contentType: "text/markdown"), CancellationToken.None)).ValueOrThrow();
        using (var put = await _http.PutAsync(upload.Url, new ByteArrayContent(Encoding.UTF8.GetBytes("# hi")) { Headers = { ContentType = new MediaTypeHeaderValue(upload.Headers["Content-Type"]) } }))
        {
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }

        Assert.Equal("text/markdown", (await store.HeadAsync(Bucket, target, CancellationToken.None)).ValueOrThrow().ContentType);
    }

    [Fact]
    public async Task SignedPartUrls_UploadPartsDirectly_ThenTheServerCompletes()
    {
        var store = S3TestServer.CreateStore(Bucket);
        var key = Guid.NewGuid().ToString("N")[..8] + "/video.bin";
        var upload = (await store.StartAsync(Bucket, key, new BlobPutOptions { ContentType = "video/mp4" }, CancellationToken.None)).ValueOrThrow();
        var first = new byte[BlobMultipart.MinPartBytes];
        Random.Shared.NextBytes(first);

        var parts = new List<BlobPart>();
        foreach (var (number, body) in new[] { (1, first), (2, Encoding.UTF8.GetBytes("end")) })
        {
            var url = (await store.SignAsync(Bucket, key, BlobUrlRequest.ForPart(upload, number), CancellationToken.None)).ValueOrThrow();
            using var put = await _http.PutAsync(url.Url, new ByteArrayContent(body));
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            parts.Add(new BlobPart(number, put.Headers.ETag!.Tag.Trim('"')));
        }

        var completed = (await store.CompleteAsync(upload, parts, CancellationToken.None)).ValueOrThrow();
        Assert.Equal((first.Length + 3L, "video/mp4"), (completed.Length, completed.ContentType));
        byte[] expected = [.. first, .. Encoding.UTF8.GetBytes("end")];
        Assert.Equal(expected, (await store.ReadAllBytesAsync(Bucket, key, CancellationToken.None)).ValueOrThrow());
    }
}
