#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AdvancedHttpService
// Guid:a3962d4f-d35f-4a29-84b7-28fd84218110
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 20:18:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Http.Enums;
using XiHan.Framework.Http.Models;
using XiHan.Framework.Http.Options;
using XiHan.Framework.Utils.Text.Json;
using XiHan.Framework.Utils.Text.Json.Dynamic;
using HttpRequestOptions = XiHan.Framework.Http.Options.HttpRequestOptions;

namespace XiHan.Framework.Http.Services;

/// <summary>
/// 高级 HTTP 服务实现
/// </summary>
public class AdvancedHttpService : IAdvancedHttpService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AdvancedHttpService> _logger;
    private readonly HttpClientOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpClientFactory">HTTP客户端工厂</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">HTTP客户端选项</param>
    public AdvancedHttpService(
        IHttpClientFactory httpClientFactory,
        ILogger<AdvancedHttpService> logger,
        IOptions<HttpClientOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// 发送 GET 请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> GetAsync<T>(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await SendRequestAsync<T>(HttpMethod.Get, url, null, options, cancellationToken);
    }

    /// <summary>
    /// 发送 GET 请求(返回字符串)
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<string>> GetStringAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await SendRequestAsync<string>(HttpMethod.Get, url, null, options, cancellationToken);
    }

    /// <summary>
    /// 发送 GET 请求(返回字节数组)
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<byte[]>> GetBytesAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await SendRequestAsync<byte[]>(HttpMethod.Get, url, null, options, cancellationToken);
    }

    /// <summary>
    /// 发送 GET 请求(返回流)
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<Stream>> GetStreamAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await SendRequestAsync<Stream>(HttpMethod.Get, url, null, options, cancellationToken);
    }

    /// <summary>
    /// 发送 POST 请求
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="request">请求数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest request, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), "请求数据不能为空");
        }
        var content = CreateJsonContent(request, options);
        return await SendRequestAsync<TResponse>(HttpMethod.Post, url, content, options, cancellationToken);
    }

    /// <summary>
    /// 发送 POST 请求(JSON)
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="jsonContent">JSON内容</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> PostJsonAsync<T>(string url, string jsonContent, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new StringContent(jsonContent, options?.Encoding ?? Encoding.UTF8, options?.ContentType ?? "application/json");
        return await SendRequestAsync<T>(HttpMethod.Post, url, content, options, cancellationToken);
    }

    /// <summary>
    /// 发送 POST 请求(表单数据)
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="formData">表单数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> PostFormAsync<T>(string url, Dictionary<string, string> formData, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new FormUrlEncodedContent(formData);
        return await SendRequestAsync<T>(HttpMethod.Post, url, content, options, cancellationToken);
    }

    /// <summary>
    /// 上传文件
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="fileStream">文件流</param>
    /// <param name="fileName">文件名</param>
    /// <param name="fieldName">字段名</param>
    /// <param name="additionalData">附加数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> UploadFileAsync<T>(string url, Stream fileStream, string fileName, string fieldName = "file",
        Dictionary<string, string>? additionalData = null, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var files = new[] { new FileUploadInfo { FileStream = fileStream, FileName = fileName, FieldName = fieldName } };
        return await UploadFilesAsync<T>(url, files, additionalData, options, cancellationToken);
    }

    /// <summary>
    /// 上传多个文件
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="files">文件信息</param>
    /// <param name="additionalData">附加数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> UploadFilesAsync<T>(string url, IEnumerable<FileUploadInfo> files,
        Dictionary<string, string>? additionalData = null, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();

        // 添加文件
        foreach (var file in files)
        {
            var streamContent = new StreamContent(file.FileStream);
            if (!string.IsNullOrEmpty(file.ContentType))
            {
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            }
            content.Add(streamContent, file.FieldName, file.FileName);
        }

        // 添加附加数据
        if (additionalData != null)
        {
            foreach (var data in additionalData)
            {
                content.Add(new StringContent(data.Value), data.Key);
            }
        }

        return await SendRequestAsync<T>(HttpMethod.Post, url, content, options, cancellationToken);
    }

    /// <summary>
    /// 发送 PUT 请求
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="request">请求数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<TResponse>> PutAsync<TRequest, TResponse>(string url, TRequest request, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), "请求数据不能为空");
        }
        var content = CreateJsonContent(request, options);
        return await SendRequestAsync<TResponse>(HttpMethod.Put, url, content, options, cancellationToken);
    }

    /// <summary>
    /// 发送 PATCH 请求
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="request">请求数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<TResponse>> PatchAsync<TRequest, TResponse>(string url, TRequest request, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), "请求数据不能为空");
        }
        var content = CreateJsonContent(request, options);
        return await SendRequestAsync<TResponse>(HttpMethod.Patch, url, content, options, cancellationToken);
    }

    /// <summary>
    /// 发送 DELETE 请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> DeleteAsync<T>(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await SendRequestAsync<T>(HttpMethod.Delete, url, null, options, cancellationToken);
    }

    /// <summary>
    /// 发送 DELETE 请求(无响应内容)
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> DeleteAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await SendRequestAsync<object>(HttpMethod.Delete, url, null, options, cancellationToken);
        return new HttpResult
        {
            IsSuccess = result.IsSuccess,
            StatusCode = result.StatusCode,
            ErrorMessage = result.ErrorMessage,
            Exception = result.Exception,
            Headers = result.Headers,
            ElapsedMilliseconds = result.ElapsedMilliseconds,
            RequestUrl = result.RequestUrl,
            RequestMethod = result.RequestMethod,
            RequestBody = result.RequestBody,
        };
    }

    /// <summary>
    /// 发送 HEAD 请求
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> HeadAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await SendRequestAsync<object>(HttpMethod.Head, url, null, options, cancellationToken);
        return new HttpResult
        {
            IsSuccess = result.IsSuccess,
            StatusCode = result.StatusCode,
            ErrorMessage = result.ErrorMessage,
            Exception = result.Exception,
            Headers = result.Headers,
            ElapsedMilliseconds = result.ElapsedMilliseconds,
            RequestUrl = result.RequestUrl,
            RequestMethod = result.RequestMethod,
            RequestBody = result.RequestBody,
        };
    }

    /// <summary>
    /// 发送 OPTIONS 请求
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> OptionsAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await SendRequestAsync<object>(HttpMethod.Options, url, null, options, cancellationToken);
        return new HttpResult
        {
            IsSuccess = result.IsSuccess,
            StatusCode = result.StatusCode,
            ErrorMessage = result.ErrorMessage,
            Exception = result.Exception,
            Headers = result.Headers,
            ElapsedMilliseconds = result.ElapsedMilliseconds,
            RequestUrl = result.RequestUrl,
            RequestMethod = result.RequestMethod,
            RequestBody = result.RequestBody,
        };
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="url">文件URL</param>
    /// <param name="destinationPath">目标路径</param>
    /// <param name="progress">进度回调</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> DownloadFileAsync(string url, string destinationPath, IProgress<long>? progress = null,
        HttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = options?.RequestId ?? Guid.NewGuid().ToString("N")[..8];

        try
        {
            using var client = GetHttpClient(options);
            ConfigureRequest(client, options);

            var fullUrl = BuildUrl(url, options);
            using var response = await client.GetAsync(fullUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return HttpResult.Failure($"HTTP {(int)response.StatusCode} {response.StatusCode}", response.StatusCode);
            }

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;
                progress?.Report(downloadedBytes);
            }

            return HttpResult.Success(response.StatusCode);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "下载文件失败。URL: {Url}, 目标路径: {DestinationPath}, 请求ID: {RequestId}",
                url, destinationPath, requestId);
            return HttpResult.Failure(ex.Message, HttpStatusCode.InternalServerError, ex);
        }
    }

    /// <summary>
    /// 批量请求
    /// </summary>
    /// <param name="requests">请求列表</param>
    /// <param name="maxConcurrency">最大并发数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<IEnumerable<HttpResult<object>>> BatchRequestAsync(IEnumerable<BatchRequestInfo> requests,
        int maxConcurrency = 10, CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = requests.Select(async request =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                HttpContent? content = null;
                if (request.Content != null)
                {
                    content = CreateJsonContent(request.Content, request.Options);
                }

                return await SendRequestAsync<object>(request.Method, request.Url, content, request.Options, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 配置请求
    /// </summary>
    /// <param name="client">HTTP客户端</param>
    /// <param name="options">请求选项</param>
    private static void ConfigureRequest(HttpClient client, HttpRequestOptions? options)
    {
        if (options?.Timeout.HasValue == true)
        {
            client.Timeout = options.Timeout.Value;
        }
    }

    /// <summary>
    /// 构建完整URL
    /// </summary>
    /// <param name="url">基础URL</param>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    private static string BuildUrl(string url, HttpRequestOptions? options)
    {
        if (options?.QueryParameters == null || options.QueryParameters.Count == 0)
        {
            return url;
        }

        var queryString = options.BuildQueryString();
        return url.Contains('?') ? $"{url}&{queryString[1..]}" : $"{url}{queryString}";
    }

    /// <summary>
    /// 发送HTTP请求的核心方法
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="method">HTTP方法</param>
    /// <param name="url">请求URL</param>
    /// <param name="content">请求内容</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    private async Task<HttpResult<T>> SendRequestAsync<T>(HttpMethod method, string url, HttpContent? content,
    HttpRequestOptions? options, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = options?.RequestId ?? Guid.NewGuid().ToString("N")[..8];

        try
        {
            using var client = GetHttpClient(options);
            ConfigureRequest(client, options);

            var fullUrl = BuildUrl(url, options);
            using var request = new HttpRequestMessage(method, fullUrl) { Content = content };

            // 添加请求头
            if (options?.Headers != null)
            {
                foreach (var header in options.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // 添加请求ID
            request.Headers.TryAddWithoutValidation("X-Request-ID", requestId);

            using var response = await client.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            var result = new HttpResult<T>
            {
                IsSuccess = response.IsSuccessStatusCode,
                StatusCode = response.StatusCode,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                RequestUrl = fullUrl,
                RequestMethod = method.Method,
                RequestBody = content?.ReadAsStringAsync(cancellationToken)
            };

            // 复制响应头
            foreach (var header in response.Headers)
            {
                result.Headers[header.Key] = header.Value;
            }

            if (response.Content?.Headers != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    result.Headers[header.Key] = header.Value;
                }
            }

            if (response.IsSuccessStatusCode)
            {
                result.Data = await DeserializeResponseAsync<T>(response, cancellationToken);
                result.RawDataString = await DeserializeResponseAsync<string>(response, cancellationToken);
                result.RawDataByte = await DeserializeResponseAsync<byte[]>(response, cancellationToken);
                result.RawDataSteam = await DeserializeResponseAsync<Stream>(response, cancellationToken);
            }
            else
            {
                result.ErrorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                if (response.Content != null)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!string.IsNullOrEmpty(errorContent))
                    {
                        result.ErrorMessage += $": {errorContent}";
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP请求失败。方法: {Method}, URL: {Url}, 请求ID: {RequestId}",
                method.Method, url, requestId);

            return HttpResult<T>.Failure(ex.Message, HttpStatusCode.InternalServerError, ex)
                .SetElapsedMilliseconds(stopwatch.ElapsedMilliseconds)
                .SetRequestInfo(method.Method, url);
        }
    }

    /// <summary>
    /// 获取HTTP客户端
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    private HttpClient GetHttpClient(HttpRequestOptions? options)
    {
        // 默认使用远程客户端
        var clientName = HttpGroupEnum.Remote.ToString();

        // 可以根据URL或其他条件选择不同的客户端
        if (options?.Tags.ContainsKey("ClientName") == true)
        {
            clientName = options.Tags["ClientName"].ToString() ?? clientName;
        }

        return _httpClientFactory.CreateClient(clientName);
    }

    /// <summary>
    /// 创建JSON内容
    /// </summary>
    /// <param name="data">数据</param>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    private StringContent CreateJsonContent(object data, HttpRequestOptions? options)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "数据不能为空");
        }

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        return new StringContent(json, options?.Encoding ?? Encoding.UTF8, options?.ContentType ?? "application/json");
    }

    /// <summary>
    /// 反序列化响应
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="response">HTTP响应</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    private async Task<T?> DeserializeResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var targetType = typeof(T);

        if (targetType == typeof(string))
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return (T)(object)content;
        }

        if (targetType == typeof(byte[]))
        {
            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (T)(object)content;
        }

        if (targetType == typeof(Stream))
        {
            var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            return (T)(object)content;
        }

        if (targetType == typeof(object))
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return !JsonHelper.TryParseJsonDynamic(content, out var dynamicObject)
                ? throw new JsonException("无法将响应内容转换为动态对象")
                : (T?)dynamicObject;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken);
    }
}

/// <summary>
/// HttpResult 扩展方法
/// </summary>
internal static class HttpResultExtensions
{
    public static HttpResult<T> SetElapsedMilliseconds<T>(this HttpResult<T> result, long elapsedMilliseconds)
    {
        result.ElapsedMilliseconds = elapsedMilliseconds;
        return result;
    }

    public static HttpResult<T> SetRequestInfo<T>(this HttpResult<T> result, string method, string url)
    {
        result.RequestMethod = method;
        result.RequestUrl = url;
        return result;
    }
}
