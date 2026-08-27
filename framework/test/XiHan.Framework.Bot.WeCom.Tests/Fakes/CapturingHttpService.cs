// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Http.Models;
using XiHan.Framework.Http.Options;
using XiHan.Framework.Http.Services;

namespace XiHan.Framework.Bot.WeCom.Tests.Fakes;

/// <summary>
/// 记录式 HTTP 服务替身
/// </summary>
/// <remarks>
/// 企业微信机器人的所有出站请求最终都落到 <see cref="IAdvancedHttpService"/>。
/// 这里只实现被测路径真正用到的 POST 分支，把 URL、请求体、请求头原样记下来供断言；
/// 其余成员一律抛 <see cref="NotSupportedException"/>——一旦被测代码换了出站方式，
/// 测试会立刻炸出来，而不是悄悄发出真实网络请求。
/// </remarks>
internal sealed class CapturingHttpService : IAdvancedHttpService
{
    private const string NotSupportedMessage = "企业微信机器人测试不应调用该 HTTP 方法。";

    /// <summary>
    /// 下一次响应的原始报文（同时用于反序列化成强类型响应）
    /// </summary>
    public string? NextRawJson { get; set; } = """{"errcode":0,"errmsg":"ok"}""";

    /// <summary>
    /// 下一次响应在传输层是否成功
    /// </summary>
    public bool NextIsSuccess { get; set; } = true;

    /// <summary>
    /// 最后一次请求的 URL
    /// </summary>
    public string? LastUrl { get; private set; }

    /// <summary>
    /// 最后一次请求的请求体对象
    /// </summary>
    public object? LastBody { get; private set; }

    /// <summary>
    /// 最后一次请求的请求选项（请求头挂在这里）
    /// </summary>
    public XiHanHttpRequestOptions? LastOptions { get; private set; }

    /// <summary>
    /// 最后一次请求收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// 累计出站请求次数
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// 复位记录与预置响应
    /// </summary>
    public void Reset()
    {
        NextRawJson = """{"errcode":0,"errmsg":"ok"}""";
        NextIsSuccess = true;
        LastUrl = null;
        LastBody = null;
        LastOptions = null;
        LastCancellationToken = CancellationToken.None;
        CallCount = 0;
    }

    /// <summary>
    /// 把最后一次请求体按运行时类型序列化后解析为 JSON 节点
    /// </summary>
    /// <returns>JSON 节点；无请求体时为 null</returns>
    public JsonNode? LastBodyAsJson()
    {
        var body = LastBody;
        return body is null ? null : JsonNode.Parse(JsonSerializer.Serialize(body, body.GetType()));
    }

    /// <inheritdoc />
    public Task<HttpResult<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest request, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        Capture(url, request, options, cancellationToken);
        return Task.FromResult(BuildResult<TResponse>());
    }

    /// <inheritdoc />
    public Task<HttpResult<T>> PostJsonAsync<T>(string url, string jsonContent, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        Capture(url, jsonContent, options, cancellationToken);
        return Task.FromResult(BuildResult<T>());
    }

    /// <inheritdoc />
    public Task<HttpResult<T>> PostFormAsync<T>(string url, Dictionary<string, string> formData, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        Capture(url, formData, options, cancellationToken);
        return Task.FromResult(BuildResult<T>());
    }

    /// <inheritdoc />
    public Task<HttpResult<T>> GetAsync<T>(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult<string>> GetStringAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult<byte[]>> GetBytesAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult<Stream>> GetStreamAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult<T>> UploadFileAsync<T>(string url, Stream fileStream, string fileName, string fieldName = "file",
        Dictionary<string, string>? additionalData = null, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult<T>> UploadFilesAsync<T>(string url, IEnumerable<FileUploadInfo> files,
        Dictionary<string, string>? additionalData = null, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult<TResponse>> PutAsync<TRequest, TResponse>(string url, TRequest request, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult<TResponse>> PatchAsync<TRequest, TResponse>(string url, TRequest request, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult<T>> DeleteAsync<T>(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult> DeleteAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult> HeadAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult> OptionsAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<HttpResult> DownloadFileAsync(string url, string destinationPath, IProgress<long>? progress = null,
        XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    /// <inheritdoc />
    public Task<IEnumerable<HttpResult<object>>> BatchRequestAsync(IEnumerable<BatchRequestInfo> requests,
        int maxConcurrency = 10, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(NotSupportedMessage);
    }

    private void Capture(string url, object? body, XiHanHttpRequestOptions? options, CancellationToken cancellationToken)
    {
        LastUrl = url;
        LastBody = body;
        LastOptions = options;
        LastCancellationToken = cancellationToken;
        CallCount++;
    }

    private HttpResult<T> BuildResult<T>()
    {
        var raw = NextRawJson;

        if (!NextIsSuccess)
        {
            return new HttpResult<T>
            {
                IsSuccess = false,
                StatusCode = HttpStatusCode.BadGateway,
                RawDataString = raw,
                ErrorMessage = "transport failed"
            };
        }

        var result = new HttpResult<T>
        {
            IsSuccess = true,
            StatusCode = HttpStatusCode.OK,
            RawDataString = raw
        };

        if (!string.IsNullOrEmpty(raw))
        {
            result.Data = JsonSerializer.Deserialize<T>(raw);
        }

        return result;
    }
}
