// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Blobs;
using XiHan.Framework.Kernel;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 文件系统存储经宿主代收发的预签名地址
/// </summary>
/// <remarks>
/// 下载（整读、单区间 206、后缀区间、不可满足 416、保存文件名）、上传（限定内容类型）、分片上传的一片，签名不符、方法不符与过期都是 403（<see cref="ObjectStorageErrors.SignatureInvalid"/>）；签名器即 <see cref="IBlobUrlSigner"/>，<see cref="HostedBlobUrlSigner.ForStore"/> 签出的地址由端点交给命名实例收发
/// </remarks>
public sealed class HostedBlobUrlTests : IDisposable
{
    /// <summary>
    /// 测试用签名密钥（32 字节以上）
    /// </summary>
    private const string Secret = "unit-test-signing-secret-with-32-bytes!";

    /// <summary>
    /// 用例的容器
    /// </summary>
    private const string Container = "files";

    /// <summary>
    /// 端点给非 PDF 内容的策略
    /// </summary>
    private const string SandboxPolicy = "sandbox; default-src 'none'; img-src 'self' data:; media-src 'self'; style-src 'unsafe-inline'";

    /// <summary>
    /// 临时根目录
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <summary>
    /// 可拨动的时钟：签名的过期按它判定
    /// </summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// 删除临时根目录
    /// </summary>
    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task Downloads_ServeWholeAndRangedContent_AndRejectTamperingAndExpiry()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var store = app.Services.GetRequiredService<IBlobStore>();
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();
        Assert.IsType<HostedBlobUrlSigner>(signer);
        Assert.True((await store.PutAsync(new ContainerName(Container), new BlobKey("docs/report.txt"), Encoding.UTF8.GetBytes("0123456789"), new BlobPutOptions { ContentType = "text/plain" }, CancellationToken.None)).IsSuccess);

        var url = (await signer.SignAsync(new ContainerName(Container), new BlobKey("docs/report.txt"), BlobUrlRequest.ForDownload(expiresIn: null, downloadFileName: "报告 2026.txt"), CancellationToken.None)).ValueOrThrow();
        Assert.Equal((BlobUrlMethod.Get, _clock.GetUtcNow() + BlobUrlRequest.DefaultExpiresIn), (url.Method, url.ExpiresAt));
        Assert.False(url.Url.IsAbsoluteUri);
        Assert.StartsWith("/blobs/files/docs/report.txt?", url.Url.OriginalString, StringComparison.Ordinal);

        // 整读：内容、内容类型、可续传标记与保存文件名
        using (var whole = await client.GetAsync(url.Url))
        {
            Assert.Equal(HttpStatusCode.OK, whole.StatusCode);
            Assert.Equal("0123456789", await whole.Content.ReadAsStringAsync());
            Assert.Equal("text/plain", whole.Content.Headers.ContentType?.MediaType);
            Assert.Equal(["bytes"], whole.Headers.AcceptRanges);
            Assert.Contains("filename*=UTF-8''%E6%8A%A5%E5%91%8A%202026.txt", whole.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
        }

        // 单区间、后缀区间：206 与 Content-Range；起点越界：416 与 bytes */总长
        Assert.Equal(("234", "bytes 2-4/10"), await RangeAsync(client, url.Url, new RangeHeaderValue(2, 4), HttpStatusCode.PartialContent));
        Assert.Equal(("789", "bytes 7-9/10"), await RangeAsync(client, url.Url, new RangeHeaderValue(null, 3), HttpStatusCode.PartialContent));
        Assert.Equal((string.Empty, "bytes */10"), await RangeAsync(client, url.Url, new RangeHeaderValue(20, null), HttpStatusCode.RequestedRangeNotSatisfiable));

        // 换个键、改过期时刻、拿下载地址去上传：签名不符，403
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url.Url.OriginalString.Replace("report.txt", "other.txt", StringComparison.Ordinal))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url.Url.OriginalString.Replace("x-expires=", "x-expires=9", StringComparison.Ordinal))).StatusCode);
        using (var wrongMethod = await client.PutAsync(url.Url, new StringContent("x")))
        {
            Assert.Equal(HttpStatusCode.Forbidden, wrongMethod.StatusCode);
            Assert.Equal(ObjectStorageErrors.SignatureInvalid, await CodeOf(wrongMethod));
        }

        // 过期：时钟越过有效期后同一地址 403
        var shortLived = (await signer.SignAsync(new ContainerName(Container), new BlobKey("docs/report.txt"), BlobUrlRequest.ForDownload(TimeSpan.FromMinutes(1)), CancellationToken.None)).ValueOrThrow();
        _clock.Advance(TimeSpan.FromMinutes(2));
        using var expired = await client.GetAsync(shortLived.Url);
        Assert.Equal((HttpStatusCode.Forbidden, ObjectStorageErrors.SignatureInvalid), (expired.StatusCode, await CodeOf(expired)));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync((await signer.SignAsync(new ContainerName(Container), new BlobKey("docs/missing.txt"), BlobUrlRequest.ForDownload(), CancellationToken.None)).ValueOrThrow().Url)).StatusCode);
    }

    [Fact]
    public async Task Uploads_WriteWholeBlobsAndParts_HonouringTheSignedContentType()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var store = app.Services.GetRequiredService<IBlobStore>();
        var multipart = app.Services.GetRequiredService<IBlobMultipartStore>();
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();

        // 整个对象：限定内容类型，类型不符 403，相符 200 并带 ETag，存进去的内容类型就是它
        var upload = (await signer.SignAsync(new ContainerName(Container), new BlobKey("inbox/note.txt"), BlobUrlRequest.ForUpload(expiresIn: null, contentType: "text/markdown"), CancellationToken.None)).ValueOrThrow();
        Assert.Equal("text/markdown", upload.Headers["Content-Type"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync(upload.Url, new StringContent("# hi", Encoding.UTF8, new MediaTypeHeaderValue("text/plain")))).StatusCode);
        using (var accepted = await client.PutAsync(upload.Url, new ByteArrayContent(Encoding.UTF8.GetBytes("# hi")) { Headers = { ContentType = new MediaTypeHeaderValue("text/markdown") } }))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var head = (await store.HeadAsync(new ContainerName(Container), new BlobKey("inbox/note.txt"), CancellationToken.None)).ValueOrThrow();
            Assert.Equal(("\"" + head.ETag + "\"", "text/markdown"), (accepted.Headers.ETag?.Tag, head.ContentType));
        }

        // 分片上传：服务端发起，客户端拿每片的地址直传，ETag 头即分片 ETag，服务端按它们完成
        var started = (await multipart.StartAsync(new ContainerName(Container), new BlobKey("inbox/video.bin"), new BlobPutOptions { ContentType = "video/mp4" }, CancellationToken.None)).ValueOrThrow();
        var first = new byte[BlobMultipart.MinPartBytes];
        Random.Shared.NextBytes(first);
        var parts = new List<BlobPart>();
        foreach (var (number, body) in new[] { (1, first), (2, Encoding.UTF8.GetBytes("end")) })
        {
            var partUrl = (await signer.SignAsync(new ContainerName(Container), new BlobKey("inbox/video.bin"), BlobUrlRequest.ForPart(started, number), CancellationToken.None)).ValueOrThrow();
            using var put = await client.PutAsync(partUrl.Url, new ByteArrayContent(body));
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            parts.Add(new BlobPart(number, put.Headers.ETag!.Tag.Trim('"')));
        }

        var completed = (await multipart.CompleteAsync(started, parts, CancellationToken.None)).ValueOrThrow();
        Assert.Equal((first.Length + 3L, "video/mp4"), (completed.Length, completed.ContentType));
        byte[] expected = [.. first, .. Encoding.UTF8.GetBytes("end")];
        Assert.Equal(expected, (await store.ReadAllBytesAsync(new ContainerName(Container), new BlobKey("inbox/video.bin"), CancellationToken.None)).ValueOrThrow());

        // 分片地址只能传那一片：签给另一个对象的地址验签不过
        await Assert.ThrowsAsync<ArgumentException>(async () => await signer.SignAsync(new ContainerName(Container), new BlobKey("inbox/other.bin"), BlobUrlRequest.ForPart(started, 1), CancellationToken.None));
    }

    [Fact]
    public async Task Uploads_WithoutASignedContentType_RejectAnInvalidRequestContentType()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var store = app.Services.GetRequiredService<IBlobStore>();
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();
        var upload = (await signer.SignAsync(new ContainerName(Container), new BlobKey("inbox/any.txt"), BlobUrlRequest.ForUpload(), CancellationToken.None)).ValueOrThrow();

        // 签名没限定内容类型时取请求头：超过 255 个字符不合文法，400 且不写入（ADR-0214）
        using (var rejected = await client.PutAsync(upload.Url, new ByteArrayContent(Encoding.UTF8.GetBytes("x")) { Headers = { ContentType = new MediaTypeHeaderValue("text/" + new string('x', 300)) } }))
        {
            Assert.Equal((HttpStatusCode.BadRequest, ObjectStorageErrors.ContentTypeInvalid), (rejected.StatusCode, await CodeOf(rejected)));
        }

        // 制表符不是 OWS，同样不合文法（ADR-0214、ADR-0295）。测试用的 HttpClient 会把 .NET 解析得了的值改写后再发
        // （text/plain;\tcharset=utf-8 发出去是 text/plain; charset=utf-8），所以用一个它解析不了、原样发出的值
        using (var tabbed = new ByteArrayContent(Encoding.UTF8.GetBytes("x")))
        {
            Assert.True(tabbed.Headers.TryAddWithoutValidation("Content-Type", "text/plain;\t;charset=utf-8"));
            using var rejected = await client.PutAsync(upload.Url, tabbed);
            Assert.Equal((HttpStatusCode.BadRequest, ObjectStorageErrors.ContentTypeInvalid), (rejected.StatusCode, await CodeOf(rejected)));
        }

        Assert.False((await store.ExistsAsync(new ContainerName(Container), new BlobKey("inbox/any.txt"), CancellationToken.None)).ValueOrThrow());
        using (var accepted = await client.PutAsync(upload.Url, new ByteArrayContent(Encoding.UTF8.GetBytes("x")) { Headers = { ContentType = new MediaTypeHeaderValue("text/plain") } }))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        Assert.Equal("text/plain", (await store.HeadAsync(new ContainerName(Container), new BlobKey("inbox/any.txt"), CancellationToken.None)).ValueOrThrow().ContentType);
    }

    [Fact]
    public async Task Uploads_WithoutASignedContentType_KeepAnEmptyParameterAsSent()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var store = app.Services.GetRequiredService<IBlobStore>();
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();
        var upload = (await signer.SignAsync(new ContainerName(Container), new BlobKey("inbox/dangling.txt"), BlobUrlRequest.ForUpload(), CancellationToken.None)).ValueOrThrow();

        // RFC 9110 的参数可以为空（ADR-0295）：ADR-0282 曾以 400 拒绝结尾多出的 ;，现在照收，对象的内容类型就是请求发来的那一串
        using var dangling = new ByteArrayContent(Encoding.UTF8.GetBytes("x"));
        Assert.True(dangling.Headers.TryAddWithoutValidation("Content-Type", "text/plain; charset=utf-8;"));
        using (var accepted = await client.PutAsync(upload.Url, dangling))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        Assert.Equal("text/plain; charset=utf-8;", (await store.HeadAsync(new ContainerName(Container), new BlobKey("inbox/dangling.txt"), CancellationToken.None)).ValueOrThrow().ContentType);
    }

    [Theory]
    [InlineData("text/plain; ", "text/plain;")]
    [InlineData("text/plain; charset=utf-8; ", "text/plain; charset=utf-8;")]
    [InlineData("\ttext/plain;\t", "text/plain;")]
    [InlineData("  text/plain \t ", "text/plain")]
    public async Task Uploads_WithoutASignedContentType_DropTheWhitespaceAroundTheRequestContentType(string sent, string stored)
    {
        await using var app = await StartAsync();
        var store = app.Services.GetRequiredService<IBlobStore>();
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();
        var upload = (await signer.SignAsync(new ContainerName(Container), new BlobKey("inbox/trimmed.txt"), BlobUrlRequest.ForUpload(), CancellationToken.None)).ValueOrThrow();

        // RFC 9110 §5.5：字段值不含首尾的空白（空格与制表符），收的一方先去掉它（ADR-0314）。Kestrel 交给应用之前已经去掉，
        // 进程内的测试服务器原样转交，端点自己去掉，两种宿主结果一致；以前测试服务器上以空格结尾的回 400（ADR-0308）
        var (status, _) = await SendRawAsync(app, HttpMethods.Put, upload.Url, sent);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(stored, (await store.HeadAsync(new ContainerName(Container), new BlobKey("inbox/trimmed.txt"), CancellationToken.None)).ValueOrThrow().ContentType);
    }

    [Fact]
    public async Task Uploads_WithASignedContentType_CompareItWithoutTheSurroundingWhitespace()
    {
        await using var app = await StartAsync();
        var store = app.Services.GetRequiredService<IBlobStore>();
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();
        var upload = (await signer.SignAsync(new ContainerName(Container), new BlobKey("inbox/signed.md"), BlobUrlRequest.ForUpload(expiresIn: null, contentType: "text/markdown"), CancellationToken.None)).ValueOrThrow();

        // 去掉首尾空白之后才与签名限定的值比较；去掉之后仍不同的照旧签名不符
        Assert.Equal(StatusCodes.Status403Forbidden, (await SendRawAsync(app, HttpMethods.Put, upload.Url, "text/markdown;")).Status);
        Assert.Equal(StatusCodes.Status200OK, (await SendRawAsync(app, HttpMethods.Put, upload.Url, " text/markdown\t")).Status);
        Assert.Equal("text/markdown", (await store.HeadAsync(new ContainerName(Container), new BlobKey("inbox/signed.md"), CancellationToken.None)).ValueOrThrow().ContentType);
    }

    [Fact]
    public async Task Uploads_WithoutASignedContentType_DropOnlySpacesAndTabs()
    {
        await using var app = await StartAsync();
        var store = app.Services.GetRequiredService<IBlobStore>();
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();

        // 只有空格与制表符：去掉之后为空，同没给内容类型，存缺省值
        var blank = (await signer.SignAsync(new ContainerName(Container), new BlobKey("inbox/blank.bin"), BlobUrlRequest.ForUpload(), CancellationToken.None)).ValueOrThrow();
        Assert.Equal(StatusCodes.Status200OK, (await SendRawAsync(app, HttpMethods.Put, blank.Url, " \t ")).Status);
        Assert.Equal(BlobPutOptions.DefaultContentType, (await store.HeadAsync(new ContainerName(Container), new BlobKey("inbox/blank.bin"), CancellationToken.None)).ValueOrThrow().ContentType);

        // 别的空白（不换行空格）不是 HTTP 的空白，不去掉：不合内容类型文法，400 且不写入。以前只有它时按没给内容类型写入
        var other = (await signer.SignAsync(new ContainerName(Container), new BlobKey("inbox/other.txt"), BlobUrlRequest.ForUpload(), CancellationToken.None)).ValueOrThrow();
        foreach (var contentType in new[] { "\u00a0", "text/plain\u00a0" })
        {
            Assert.Equal((StatusCodes.Status400BadRequest, ObjectStorageErrors.ContentTypeInvalid), await SendRawAsync(app, HttpMethods.Put, other.Url, contentType));
        }

        Assert.False((await store.ExistsAsync(new ContainerName(Container), new BlobKey("inbox/other.txt"), CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task RangedDownloads_ReadTheRangeHeaderWithoutItsSurroundingWhitespace()
    {
        // 下载读的 Range 头经 ASP.NET Core 的头解析器，本来就照 RFC 9110 去掉首尾空白：测试服务器原样转交也一样
        await using var app = await StartAsync();
        var store = app.Services.GetRequiredService<IBlobStore>();
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();
        Assert.True((await store.PutAsync(new ContainerName(Container), new BlobKey("docs/range.txt"), Encoding.UTF8.GetBytes("0123456789"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        var url = (await signer.SignAsync(new ContainerName(Container), new BlobKey("docs/range.txt"), BlobUrlRequest.ForDownload(), CancellationToken.None)).ValueOrThrow().Url;

        var context = await Raw(app, HttpMethods.Get, url, static request => request.Headers.Range = " bytes=2-4\t");

        Assert.Equal(StatusCodes.Status206PartialContent, context.Response.StatusCode);
        Assert.Equal("bytes 2-4/10", context.Response.Headers.ContentRange.ToString());
        using var reader = new StreamReader(context.Response.Body);
        Assert.Equal("234", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task StoreBoundSigner_ServesNamedStores_AndBindsTheStoreIntoTheSignature()
    {
        await using var app = await StartAsync(
            new("XiHan:ObjectStorage:Stores:archive:Provider", "filesystem"),
            new("XiHan:ObjectStorage:Stores:archive:RootPath", Path.Combine(_root.Path, "archive")));
        using var client = app.GetTestClient();
        var archive = (await app.Services.GetRequiredService<BlobStores>().GetAsync("archive", CancellationToken.None)).ValueOrThrow();
        var store = app.Services.GetRequiredService<IBlobStore>();
        Assert.True((await archive.PutAsync(new ContainerName(Container), new BlobKey("docs/a.txt"), Encoding.UTF8.GetBytes("archived"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.True((await store.PutAsync(new ContainerName(Container), new BlobKey("docs/a.txt"), Encoding.UTF8.GetBytes("default"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);

        var signer = app.Services.GetRequiredService<HostedBlobUrlSigner>();
        var named = signer.ForStore("archive");
        Assert.Equal((null, "archive"), (signer.Store, named.Store));
        Assert.Throws<ArgumentException>(() => signer.ForStore("Not A Name"));

        // 同一个键：命名实例的地址读到命名实例的内容，缺省签名器的地址读到缺省实例的
        var url = (await named.SignAsync(new ContainerName(Container), new BlobKey("docs/a.txt"), BlobUrlRequest.ForDownload(), CancellationToken.None)).ValueOrThrow().Url.OriginalString;
        Assert.Contains("x-store=archive", url, StringComparison.Ordinal);
        Assert.Equal("archived", await client.GetStringAsync(url));
        Assert.Equal("default", await client.GetStringAsync((await signer.SignAsync(new ContainerName(Container), new BlobKey("docs/a.txt"), BlobUrlRequest.ForDownload(), CancellationToken.None)).ValueOrThrow().Url));

        // 实例名在签名里：改成别的实例或去掉都签名不符
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url.Replace("x-store=archive", "x-store=other", StringComparison.Ordinal))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url.Replace("&x-store=archive", string.Empty, StringComparison.Ordinal))).StatusCode);

        // 上传落到命名实例
        var upload = (await named.SignAsync(new ContainerName(Container), new BlobKey("inbox/n.txt"), BlobUrlRequest.ForUpload(), CancellationToken.None)).ValueOrThrow();
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync(upload.Url, new StringContent("new"))).StatusCode);
        Assert.Equal("new", Encoding.UTF8.GetString((await archive.ReadAllBytesAsync(new ContainerName(Container), new BlobKey("inbox/n.txt"), CancellationToken.None)).ValueOrThrow()));
        Assert.False((await store.ExistsAsync(new ContainerName(Container), new BlobKey("inbox/n.txt"), CancellationToken.None)).ValueOrThrow());

        // 签名有效但实例不存在：404
        using var missing = await client.GetAsync((await signer.ForStore("missing").SignAsync(new ContainerName(Container), new BlobKey("docs/a.txt"), BlobUrlRequest.ForDownload(), CancellationToken.None)).ValueOrThrow().Url);
        Assert.Equal((HttpStatusCode.NotFound, ObjectStorageErrors.StoreNotFound), (missing.StatusCode, await CodeOf(missing)));
    }

    [Fact]
    public async Task AKeyWithALoneSurrogate_IsRejected_SoNoSignedUrlOpensTheObjectNamedByItsReplacement()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var store = app.Services.GetRequiredService<IBlobStore>();
        Assert.True((await store.PutAsync(new ContainerName(Container), new BlobKey("a�"), Encoding.UTF8.GetBytes("other"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);

        // 以前键 a\uD800 照收：签出的地址经百分号编码成 a%EF%BF%BD，规范串经宽松编码后与给 a� 签的字节相同，打开的是上面那个对象（ADR-0316）
        (HttpStatusCode Status, string Body)? opened = null;
        var error = await Record.ExceptionAsync(async () => opened = await OpenSignedDownloadAsync(app, client, "a\uD800"));
        Assert.True(error is ArgumentException, $"A key with a lone surrogate must be rejected before it is signed; its signed URL opened {opened}.");
        Assert.False(BlobKey.IsValid("a\uD800"));

        // 那个对象用它自己的键照常签、照常打开；成对的代理项是平面以外的字符，照常合法
        Assert.Equal((HttpStatusCode.OK, "other"), await OpenSignedDownloadAsync(app, client, "a�"));
        Assert.True((await store.PutAsync(new ContainerName(Container), new BlobKey("a\U0001F600"), Encoding.UTF8.GetBytes("pair"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.Equal((HttpStatusCode.OK, "pair"), await OpenSignedDownloadAsync(app, client, "a\U0001F600"));
    }

    [Fact]
    public async Task TheSignature_IsComputedOverStrictUtf8_SoAKeyThatBypassedTheGrammarIsNotSigned()
    {
        await using var app = await StartAsync();
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();

        // 第二道防线：绕过文法核对的键编不成 UTF-8，按编程错误抛出，不换成 U+FFFD 之后照签
        var error = await Record.ExceptionAsync(async () => await signer.SignAsync(new ContainerName(Container), Unchecked("a\uD800"), BlobUrlRequest.ForDownload(), CancellationToken.None));
        Assert.True(error is ArgumentException { InnerException: EncoderFallbackException }, $"Signing a key with a lone surrogate must fail as a programming error, got {error?.GetType().Name ?? "a signed URL"}.");
        Assert.Contains("lone surrogate", error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignedQueryValues_ThatAreNotUtf8_AreASignatureMismatch_NotAServerError()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var store = app.Services.GetRequiredService<IBlobStore>();
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();
        Assert.True((await store.PutAsync(new ContainerName(Container), new BlobKey("docs/a.txt"), Encoding.UTF8.GetBytes("a"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        var url = (await signer.SignAsync(new ContainerName(Container), new BlobKey("docs/a.txt"), BlobUrlRequest.ForDownload(null, "�.txt"), CancellationToken.None)).ValueOrThrow().Url.OriginalString;
        Assert.Contains("x-filename=%EF%BF%BD.txt", url, StringComparison.Ordinal);

        // 查询串里编成 UTF-8 的代理项与截断的序列：宿主从字节解码，出不了孤立的代理项，严格编码的规范串在验签时不会失败，只是签名不符
        foreach (var tampered in new[] { "%ED%A0%80.txt", "%ED%A0.txt", "%F0%9F.txt" })
        {
            using var response = await client.GetAsync(url.Replace("%EF%BF%BD.txt", tampered, StringComparison.Ordinal));
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"'{tampered}' gave {(int)response.StatusCode}.");
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public void Options_RequireAStrongSecret_AndAValidBase()
    {
        static IConfiguration Build(params (string Key, string Value)[] values) => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        Assert.Throws<ArgumentException>(static () => HostedBlobUrlOptions.FromConfiguration(Build()));
        Assert.Throws<ArgumentException>(static () => HostedBlobUrlOptions.FromConfiguration(Build(("XiHan:ObjectStorage:SignedUrls:Secret", "short"))));
        // 宽松的 UTF-8 把孤立的代理项都换成 U+FFFD：以前十一个孤立的代理项按 33 字节过了最小长度，实际的密钥是公开的十一个 U+FFFD
        Assert.Throws<ArgumentException>(static () => HostedBlobUrlOptions.FromConfiguration(Build(("XiHan:ObjectStorage:SignedUrls:Secret", new string('\uD800', 11)))));
        Assert.Throws<ArgumentException>(static () => HostedBlobUrlOptions.FromConfiguration(Build(("XiHan:ObjectStorage:SignedUrls:Secret", Secret + "\uDC00"))));
        // 成对的代理项是一个字符，按它的四个字节计：八个 emoji 正好 32 字节
        Assert.Equal(32, HostedBlobUrlOptions.FromConfiguration(Build(("XiHan:ObjectStorage:SignedUrls:Secret", string.Concat(Enumerable.Repeat("😀", 8))))).Secret.Length);
        var absolute =HostedBlobUrlOptions.FromConfiguration(Build(("XiHan:ObjectStorage:SignedUrls:Secret", Secret), ("XiHan:ObjectStorage:SignedUrls:BaseUrl", "https://files.example.com/dl/")));
        Assert.Equal(("https://files.example.com/dl", "/dl"), (absolute.BaseUrl, absolute.BasePath));
        Assert.Throws<ArgumentException>(static () => new HostedBlobUrlOptions { Secret = Encoding.UTF8.GetBytes(Secret), BaseUrl = "files" });
        Assert.Throws<ArgumentOutOfRangeException>(static () => BlobUrlRequest.ForDownload(TimeSpan.FromDays(8)));
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("image/svg+xml")]
    [InlineData("image/png")]
    [InlineData("application/octet-stream")]
    public async Task Downloads_RunInASandbox_AndCannotBeSniffed(string contentType)
    {
        // 端点常与应用同源，内容是用户上传的：标成 HTML 的上传不能在应用的源上运行脚本
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var url = await UploadAsync(app, "up/file", contentType);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["nosniff"], response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal([SandboxPolicy], response.Headers.GetValues("Content-Security-Policy"));
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/PDF")]
    [InlineData("application/pdf; name=report.pdf")]
    public async Task PdfDownloads_StayOutOfTheSandbox_ButCannotBeSniffed(string contentType)
    {
        // 浏览器内置的 PDF 阅读器在沙箱里打不开；有 nosniff，标成 PDF 的 HTML 也不会按 HTML 解析
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var url = await UploadAsync(app, "up/report", contentType);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["nosniff"], response.Headers.GetValues("X-Content-Type-Options"));
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task RangedDownloads_CarryTheSameHeaders()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var url = await UploadAsync(app, "up/page", "text/html");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(0, 1);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(["nosniff"], response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal([SandboxPolicy], response.Headers.GetValues("Content-Security-Policy"));
    }

    [Fact]
    public async Task ErrorResponses_CannotBeSniffed()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var url = await UploadAsync(app, "up/late", "text/plain");
        _clock.Advance(BlobUrlRequest.DefaultExpiresIn + TimeSpan.FromSeconds(1));

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(["nosniff"], response.Headers.GetValues("X-Content-Type-Options"));
    }

    [Fact]
    public async Task ApplicationPolicy_IsKept()
    {
        // 要换策略的应用在 UseXiHan 之前设自己的头，端点不覆盖
        await using var app = await StartWithAsync(static web => web.Use(static (http, next) =>
        {
            http.Response.Headers.ContentSecurityPolicy = "default-src 'self'";
            return next(http);
        }));
        using var client = app.GetTestClient();
        var url = await UploadAsync(app, "up/app", "text/html");

        var response = await client.GetAsync(url);

        Assert.Equal(["default-src 'self'"], response.Headers.GetValues("Content-Security-Policy"));
    }

    [Fact]
    public async Task MapXiHanBlobs_WithoutTheFeature_FailsAtStartup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection([new("XiHan:ObjectStorage:FileSystem:RootPath", _root.Path)]);
        builder.AddXiHan(static xihan => xihan.UseFeature<FileSystemObjectStorageFeature>());
        await using var app = builder.Build();
        app.UseXiHan();

        Assert.Equal(KernelErrors.InvalidAppState, Assert.Throws<FrameworkException>(() => app.MapXiHanBlobs()).Code);
    }

    /// <summary>
    /// 启动宿主
    /// </summary>
    /// <remarks>
    /// 文件系统存储 + 宿主代收发的预签名地址，端点映射在 <c>/blobs</c>，时钟可拨动；<paramref name="configuration"/> 追加配置
    /// </remarks>
    private Task<WebApplication> StartAsync(params KeyValuePair<string, string?>[] configuration) => StartWithAsync(static _ => { }, configuration);

    /// <summary>
    /// 启动宿主，<paramref name="before"/> 在 <c>UseXiHan</c> 之前加中间件
    /// </summary>
    private async Task<WebApplication> StartWithAsync(Action<WebApplication> before, params KeyValuePair<string, string?>[] configuration)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
        [
            new("XiHan:ObjectStorage:FileSystem:RootPath", _root.Path),
            new("XiHan:ObjectStorage:SignedUrls:Secret", Secret),
            .. configuration,
        ]);
        builder.Services.AddSingleton<TimeProvider>(_clock);
        builder.AddXiHan(static xihan => xihan.UseFeature<FileSystemObjectStorageFeature>().UseFeature<HostedBlobUrlFeature>());
        var app = builder.Build();
        before(app);
        app.UseXiHan();
        app.MapXiHanBlobs();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// 存一个对象并签出下载地址
    /// </summary>
    private static async Task<Uri> UploadAsync(WebApplication app, string key, string contentType)
    {
        var store = app.Services.GetRequiredService<IBlobStore>();
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();
        Assert.True((await store.PutAsync(new ContainerName(Container), new BlobKey(key), Encoding.UTF8.GetBytes("<script>1</script>"), new BlobPutOptions { ContentType = contentType }, CancellationToken.None)).IsSuccess);
        return (await signer.SignAsync(new ContainerName(Container), new BlobKey(key), BlobUrlRequest.ForDownload(), CancellationToken.None)).ValueOrThrow().Url;
    }

    /// <summary>
    /// 给一个键签出下载地址并打开，返回状态码与正文
    /// </summary>
    /// <remarks>
    /// 键的文本当场构造成 <see cref="BlobKey"/>：键不合法时在这里抛出 <see cref="ArgumentException"/>
    /// </remarks>
    private static async Task<(HttpStatusCode Status, string Body)> OpenSignedDownloadAsync(WebApplication app, HttpClient client, string key)
    {
        var signer = app.Services.GetRequiredService<IBlobUrlSigner>();
        var url = (await signer.SignAsync(new ContainerName(Container), new BlobKey(key), BlobUrlRequest.ForDownload(), CancellationToken.None)).ValueOrThrow();
        using var response = await client.GetAsync(url.Url);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// 不经文法核对的键
    /// </summary>
    /// <remarks>
    /// <see cref="BlobKey"/> 只有一个字符串字段，把字符串引用按位重新解释就得到一个没过 <see cref="BlobKey.Validate"/> 的键：模拟绕过核对的代码，只为测签名器的第二道防线
    /// </remarks>
    private static BlobKey Unchecked(string text) => Unsafe.As<string, BlobKey>(ref text);

    /// <summary>
    /// 带 <c>Range</c> 头下载，返回正文与 <c>Content-Range</c>，并断言状态码
    /// </summary>
    private static async Task<(string Body, string? ContentRange)> RangeAsync(HttpClient client, Uri url, RangeHeaderValue range, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = range;
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadAsStringAsync(), response.Content.Headers.ContentRange?.ToString());
    }

    /// <summary>
    /// 经测试服务器原样发一个带 <c>Content-Type</c> 的请求
    /// </summary>
    /// <remarks>
    /// 返回状态码与 Problem Details 的错误码（200 时为空）。不经 HttpClient：它会把 .NET 解析得了的头改写后再发、拒收首尾的空白与制表符；这里直接填请求，请求头的值原样到达端点
    /// </remarks>
    private static async Task<(int Status, string? Code)> SendRawAsync(WebApplication app, string method, Uri url, string contentType)
    {
        var context = await Raw(app, method, url, request =>
        {
            request.Headers.ContentType = contentType;
            request.Body = new MemoryStream(Encoding.UTF8.GetBytes("x"));
            request.ContentLength = 1;
        });
        if (context.Response.StatusCode == StatusCodes.Status200OK)
        {
            return (context.Response.StatusCode, null);
        }

        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, document.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// 经测试服务器原样发一个请求，<paramref name="configure"/> 填请求头与请求体
    /// </summary>
    private static Task<HttpContext> Raw(WebApplication app, string method, Uri url, Action<HttpRequest> configure)
    {
        var target = url.OriginalString;
        var query = target.IndexOf('?', StringComparison.Ordinal);
        return app.GetTestServer().SendAsync(context =>
        {
            context.Request.Method = method;
            context.Request.Path = query < 0 ? target : target[..query];
            context.Request.QueryString = query < 0 ? QueryString.Empty : new QueryString(target[query..]);
            configure(context.Request);
        });
    }

    /// <summary>
    /// Problem Details 的错误码
    /// </summary>
    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
