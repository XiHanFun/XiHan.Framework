// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace XiHan.Framework.Authentication.Tests.OAuth.Infrastructure;

/// <summary>
/// 按请求地址返回预置响应的消息处理器
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(string UrlFragment, HttpStatusCode StatusCode, string Body)> _responses = [];

    /// <summary>
    /// 已捕获的请求
    /// </summary>
    public List<CapturedRequest> Requests { get; } = [];

    /// <summary>
    /// 为包含指定片段的请求地址登记响应
    /// </summary>
    /// <param name="urlFragment">请求地址片段</param>
    /// <param name="body">响应体</param>
    /// <param name="statusCode">响应状态码</param>
    /// <returns>当前处理器</returns>
    public StubHttpMessageHandler Respond(string urlFragment, string body, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responses.Add((urlFragment, statusCode, body));
        return this;
    }

    /// <summary>
    /// 取出捕获到的第一个地址包含指定片段的请求
    /// </summary>
    /// <param name="urlFragment">请求地址片段</param>
    /// <returns>捕获的请求</returns>
    public CapturedRequest RequestFor(string urlFragment)
    {
        return Requests.First(request => request.Url.Contains(urlFragment, StringComparison.Ordinal));
    }

    /// <summary>
    /// 处理请求
    /// </summary>
    /// <param name="request">请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request.Method.Method, url, body, request.Headers));

        var matched = _responses.FirstOrDefault(response => url.Contains(response.UrlFragment, StringComparison.Ordinal));
        return matched.Body is null
            ? throw new InvalidOperationException($"未为地址登记响应：{url}")
            : new HttpResponseMessage(matched.StatusCode)
            {
                Content = new StringContent(matched.Body, Encoding.UTF8, "application/json")
            };
    }
}

/// <summary>
/// 捕获到的请求
/// </summary>
/// <param name="Method">请求方法</param>
/// <param name="Url">请求地址</param>
/// <param name="Body">请求体</param>
/// <param name="Headers">请求头</param>
public sealed record CapturedRequest(string Method, string Url, string? Body, HttpRequestHeaders Headers);
