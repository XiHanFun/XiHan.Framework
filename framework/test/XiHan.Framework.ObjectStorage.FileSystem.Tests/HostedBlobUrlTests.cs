// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
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

        var url = (await signer.SignAsync(new ContainerName(Container), new BlobKey("docs/report.txt"), BlobUrlRequest.ForDownload(downloadFileName: "报告 2026.txt"), CancellationToken.None)).ValueOrThrow();
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
        var upload = (await signer.SignAsync(new ContainerName(Container), new BlobKey("inbox/note.txt"), BlobUrlRequest.ForUpload(contentType: "text/markdown"), CancellationToken.None)).ValueOrThrow();
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
    public void Options_RequireAStrongSecret_AndAValidBase()
    {
        static IConfiguration Build(params (string Key, string Value)[] values) => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        Assert.Throws<ArgumentException>(static () => HostedBlobUrlOptions.FromConfiguration(Build()));
        Assert.Throws<ArgumentException>(static () => HostedBlobUrlOptions.FromConfiguration(Build(("XiHan:ObjectStorage:SignedUrls:Secret", "short"))));
        var absolute = HostedBlobUrlOptions.FromConfiguration(Build(("XiHan:ObjectStorage:SignedUrls:Secret", Secret), ("XiHan:ObjectStorage:SignedUrls:BaseUrl", "https://files.example.com/dl/")));
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
    /// Problem Details 的错误码
    /// </summary>
    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
