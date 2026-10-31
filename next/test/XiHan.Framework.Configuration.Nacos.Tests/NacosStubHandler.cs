// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace XiHan.Framework.Configuration.Nacos.Tests;

/// <summary>
/// 按 Nacos v1 配置接口作答的 HTTP 桩。
/// </summary>
/// <remarks>
/// 只认一份配置、只在 <see cref="ContextPath"/> 下作答：取配置返回 <see cref="Content"/>，监听按 Nacos 的约定把报来的 MD5 与 <see cref="ContentMd5"/> 逐字比较，相同为未变化（空响应），不同报告这一项变化，登录发一个令牌。别的路径一律 404。记录收到的每个请求。
/// </remarks>
internal sealed class NacosStubHandler : HttpMessageHandler
{
    /// <summary>
    /// 服务端的上下文路径。
    /// </summary>
    /// <remarks>
    /// 缺省是 Nacos 的 <c>/nacos</c>。
    /// </remarks>
    public string ContextPath { get; set; } = "/nacos";

    /// <summary>
    /// 配置内容。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 服务端存的内容 MD5（Nacos 按 UTF-8 编码计算的小写十六进制）。
    /// </summary>
    public string ContentMd5 { get; set; } = string.Empty;

    /// <summary>
    /// 取配置的响应是否带 <c>Content-MD5</c> 头。
    /// </summary>
    public bool SendContentMd5Header { get; set; }

    /// <summary>
    /// 收到的请求：方法与路径（不含查询）。
    /// </summary>
    public ConcurrentQueue<(HttpMethod Method, string Path)> Requests { get; } = new();

    /// <summary>
    /// 监听请求里报来的 MD5，按收到的顺序。
    /// </summary>
    public ConcurrentQueue<string> ListenedMd5 { get; } = new();

    /// <summary>
    /// 是否已释放。
    /// </summary>
    public bool Disposed { get; private set; }

    /// <summary>
    /// 取配置的次数。
    /// </summary>
    public int Fetches => Requests.Count(static r => r.Method == HttpMethod.Get && r.Path.EndsWith("/v1/cs/configs", StringComparison.Ordinal));

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        Requests.Enqueue((request.Method, path));
        if (request.Method == HttpMethod.Post && path == ContextPath + "/v1/auth/login")
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"accessToken":"stub-token","tokenTtl":18000}""", Encoding.UTF8, "application/json") };
        }

        if (request.Method == HttpMethod.Get && path == ContextPath + "/v1/cs/configs")
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Content, Encoding.UTF8, "text/plain") };
            if (SendContentMd5Header)
            {
                // 与真实响应一致：HttpClient 把 Content-MD5 归入内容头
                response.Content.Headers.TryAddWithoutValidation("Content-MD5", ContentMd5);
            }

            return response;
        }

        if (request.Method == HttpMethod.Post && path == ContextPath + "/v1/cs/configs/listener")
        {
            // Listening-Configs：dataId ^2 group ^2 md5 [^2 tenant] ^1
            var form = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var listening = WebUtility.UrlDecode(form["Listening-Configs=".Length..]);
            var fields = listening.TrimEnd('\u0001').Split('\u0002');
            ListenedMd5.Enqueue(fields[2]);
            var changed = fields[2] == ContentMd5 ? string.Empty : WebUtility.UrlEncode($"{fields[0]}\u0002{fields[1]}\u0001");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(changed) };
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("config data not exist") };
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
