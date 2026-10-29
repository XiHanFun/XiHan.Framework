// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// 捕获出站请求的 HTTP 处理器。
/// </summary>
/// <remarks>
/// 记录方法、URL、头与内容，按设定的状态答复；设为不可达时抛出连接异常。
/// </remarks>
internal sealed class CapturingHandler : HttpMessageHandler
{
    /// <summary>
    /// 捕获的一次请求。
    /// </summary>
    /// <param name="Method">方法。</param>
    /// <param name="Url">URL。</param>
    /// <param name="Headers">请求头与内容头合并后的表。</param>
    /// <param name="Body">内容字节。</param>
    public sealed record Captured(HttpMethod Method, Uri Url, IReadOnlyDictionary<string, string> Headers, byte[] Body);

    /// <summary>
    /// 捕获的请求。
    /// </summary>
    public List<Captured> Requests { get; } = [];

    /// <summary>
    /// 答复的状态码。
    /// </summary>
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    /// <summary>
    /// 为 <see langword="true"/> 时模拟连接失败。
    /// </summary>
    public bool Unreachable { get; set; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        var body = Array.Empty<byte>();
        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }

            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        Requests.Add(new Captured(request.Method, request.RequestUri!, headers, body));
        if (Unreachable)
        {
            throw new HttpRequestException("connection refused");
        }

        return new HttpResponseMessage(Status);
    }
}
