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
using Polly.CircuitBreaker;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Enums;
using XiHan.Framework.Http.Models;
using XiHan.Framework.Http.Options;
using XiHan.Framework.Http.Proxy;
using XiHan.Framework.Serialization.Dynamic;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Http.Services;

/// <summary>
/// 高级 HTTP 服务实现
/// </summary>
public class AdvancedHttpService : IAdvancedHttpService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AdvancedHttpService> _logger;
    private readonly XiHanHttpClientOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IProxyPoolManager? _proxyPoolManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpClientFactory">HTTP客户端工厂</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">HTTP客户端选项</param>
    /// <param name="proxyPoolManager">代理池管理器(可选)</param>
    public AdvancedHttpService(
        IHttpClientFactory httpClientFactory,
        ILogger<AdvancedHttpService> logger,
        IOptions<XiHanHttpClientOptions> options,
        IProxyPoolManager? proxyPoolManager = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
        _proxyPoolManager = proxyPoolManager;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        };
    }

    /// <summary>
    /// 发送 GET 请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求Url</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> GetAsync<T>(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await SendRequestAsync<T>(HttpMethod.Get, url, null, options, cancellationToken);
    }

    /// <summary>
    /// 发送 GET 请求(返回字符串)
    /// </summary>
    /// <param name="url">请求Url</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<string>> GetStringAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await SendRequestAsync<string>(HttpMethod.Get, url, null, options, cancellationToken);
    }

    /// <summary>
    /// 发送 GET 请求(返回字节数组)
    /// </summary>
    /// <param name="url">请求Url</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<byte[]>> GetBytesAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await SendRequestAsync<byte[]>(HttpMethod.Get, url, null, options, cancellationToken);
    }

    /// <summary>
    /// 发送 GET 请求(返回流)
    /// </summary>
    /// <param name="url">请求Url</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<Stream>> GetStreamAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await SendRequestAsync<Stream>(HttpMethod.Get, url, null, options, cancellationToken);
    }

    /// <summary>
    /// 发送 POST 请求
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="url">请求Url</param>
    /// <param name="request">请求数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest request, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
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
    /// <param name="url">请求Url</param>
    /// <param name="jsonContent">JSON内容</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> PostJsonAsync<T>(string url, string jsonContent, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new StringContent(jsonContent, options?.Encoding ?? Encoding.UTF8, options?.ContentType ?? "application/json");
        return await SendRequestAsync<T>(HttpMethod.Post, url, content, options, cancellationToken);
    }

    /// <summary>
    /// 发送 POST 请求(表单数据)
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求Url</param>
    /// <param name="formData">表单数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> PostFormAsync<T>(string url, Dictionary<string, string> formData, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new FormUrlEncodedContent(formData);
        return await SendRequestAsync<T>(HttpMethod.Post, url, content, options, cancellationToken);
    }

    /// <summary>
    /// 上传文件
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求Url</param>
    /// <param name="fileStream">文件流</param>
    /// <param name="fileName">文件名</param>
    /// <param name="fieldName">字段名</param>
    /// <param name="additionalData">附加数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> UploadFileAsync<T>(string url, Stream fileStream, string fileName, string fieldName = "file",
        Dictionary<string, string>? additionalData = null, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var files = new[] { new FileUploadInfo { FileStream = fileStream, FileName = fileName, FieldName = fieldName } };
        return await UploadFilesAsync<T>(url, files, additionalData, options, cancellationToken);
    }

    /// <summary>
    /// 上传多个文件
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求Url</param>
    /// <param name="files">文件信息</param>
    /// <param name="additionalData">附加数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> UploadFilesAsync<T>(string url, IEnumerable<FileUploadInfo> files,
        Dictionary<string, string>? additionalData = null, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
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
    /// <param name="url">请求Url</param>
    /// <param name="request">请求数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<TResponse>> PutAsync<TRequest, TResponse>(string url, TRequest request, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
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
    /// <param name="url">请求Url</param>
    /// <param name="request">请求数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<TResponse>> PatchAsync<TRequest, TResponse>(string url, TRequest request, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
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
    /// <param name="url">请求Url</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> DeleteAsync<T>(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await SendRequestAsync<T>(HttpMethod.Delete, url, null, options, cancellationToken);
    }

    /// <summary>
    /// 发送 DELETE 请求(无响应内容)
    /// </summary>
    /// <param name="url">请求Url</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> DeleteAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
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
        };
    }

    /// <summary>
    /// 发送 HEAD 请求
    /// </summary>
    /// <param name="url">请求Url</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> HeadAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
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
        };
    }

    /// <summary>
    /// 发送 OPTIONS 请求
    /// </summary>
    /// <param name="url">请求Url</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> OptionsAsync(string url, XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
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
        };
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="url">文件Url</param>
    /// <param name="destinationPath">目标路径</param>
    /// <param name="progress">进度回调</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> DownloadFileAsync(string url, string destinationPath, IProgress<long>? progress = null,
        XiHanHttpRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        // 确定实际使用的超时时间：优先使用请求级别的超时，否则使用全局默认超时
        var effectiveTimeout = options?.Timeout.HasValue == true
            ? options.Timeout.Value
            : TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds);

        // 设置超时时间
        using var cts = new CancellationTokenSource(effectiveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
        var effectiveToken = linkedCts.Token;

        var stopwatch = Stopwatch.StartNew();
        var requestId = options?.RequestId ?? Guid.NewGuid().ToString("N")[..8];

        try
        {
            var client = GetHttpClient(options);
            ConfigureRequest(client, options);

            var fullUrl = BuildUrl(url, options);
            using var response = await client.GetAsync(fullUrl, HttpCompletionOption.ResponseHeadersRead, effectiveToken);

            if (!response.IsSuccessStatusCode)
            {
                return HttpResult.Failure($"HTTP {(int)response.StatusCode} {response.StatusCode}", response.StatusCode);
            }

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync(effectiveToken);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, effectiveToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), effectiveToken);
                downloadedBytes += bytesRead;
                progress?.Report(downloadedBytes);
            }
            var result = HttpResult.Success(response.StatusCode);

            return result;
        }
        catch (TaskCanceledException ex) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "文件下载超时。Url: {Url}, 目标路径: {DestinationPath}, 请求唯一标识: {RequestId}, 超时时间: {Timeout}秒",
                url, destinationPath, requestId, effectiveTimeout.TotalSeconds);

            return HttpResult.Failure($"文件下载超时（超过 {effectiveTimeout.TotalSeconds} 秒）", HttpStatusCode.RequestTimeout, ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "文件下载被取消。Url: {Url}, 目标路径: {DestinationPath}, 请求唯一标识: {RequestId}",
                url, destinationPath, requestId);

            return HttpResult.Failure("文件下载被取消", HttpStatusCode.RequestTimeout, ex);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "断路器阻止了文件下载。Url: {Url}, 目标路径: {DestinationPath}, 请求唯一标识: {RequestId}, 断路器将在 {DurationSeconds} 秒后重置",
                url, destinationPath, requestId, _options.CircuitBreakerDurationOfBreakSeconds);

            return HttpResult.Failure($"断路器已打开，文件下载被阻止。请等待约 {_options.CircuitBreakerDurationOfBreakSeconds} 秒后重试", HttpStatusCode.ServiceUnavailable, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "文件访问权限错误。Url: {Url}, 目标路径: {DestinationPath}, 请求唯一标识: {RequestId}",
                url, destinationPath, requestId);

            return HttpResult.Failure("文件访问权限错误", HttpStatusCode.Forbidden, ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "文件IO错误。Url: {Url}, 目标路径: {DestinationPath}, 请求唯一标识: {RequestId}",
                url, destinationPath, requestId);

            return HttpResult.Failure("文件IO错误", HttpStatusCode.InternalServerError, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载文件失败。Url: {Url}, 目标路径: {DestinationPath}, 请求唯一标识: {RequestId}",
                url, destinationPath, requestId);

            return HttpResult.Failure(ex.Message, HttpStatusCode.InternalServerError, ex);
        }
        finally
        {
            stopwatch.Stop();
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
    /// 发送HTTP请求的核心方法
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="method">HTTP方法</param>
    /// <param name="url">请求Url</param>
    /// <param name="content">请求内容</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    private async Task<HttpResult<T>> SendRequestAsync<T>(HttpMethod method, string url, HttpContent? content,
        XiHanHttpRequestOptions? options, CancellationToken cancellationToken)
    {
        // 确定实际使用的超时时间：优先使用请求级别的超时，否则使用全局默认超时
        var effectiveTimeout = options?.Timeout.HasValue == true
            ? options.Timeout.Value
            : TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds);

        // 设置超时时间
        using var cts = new CancellationTokenSource(effectiveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
        var effectiveToken = linkedCts.Token;

        var stopwatch = Stopwatch.StartNew();
        var requestId = options?.RequestId ?? Guid.NewGuid().ToString("N")[..8];

        // 确定要使用的代理
        ProxyConfiguration? proxyToUse = null;
        if (options?.Proxy != null)
        {
            proxyToUse = options.Proxy;
        }
        else if (options?.UseProxyPool == true && _proxyPoolManager != null)
        {
            proxyToUse = _proxyPoolManager.GetNextProxy();
            if (proxyToUse == null)
            {
                _logger.LogWarning("代理池已启用但没有可用的代理,请求将不使用代理");
            }
        }

        try
        {
            HttpClient client;
            HttpMessageHandler? handler = null;

            // 确定是否忽略 SSL 证书错误
            var ignoreSslErrors = _options.IgnoreSslErrors;
            if (options?.ValidateSslCertificate.HasValue == true)
            {
                // 如果请求明确指定了验证SSL，则不忽略SSL错误；否则忽略
                ignoreSslErrors = !options.ValidateSslCertificate.Value;
            }

            // 如果需要使用代理或需要特殊的SSL配置,创建带特殊配置的客户端
            var needCustomClient = proxyToUse != null || (options?.ValidateSslCertificate.HasValue == true && ignoreSslErrors != _options.IgnoreSslErrors);

            if (needCustomClient)
            {
                handler = CreateProxyHandler(proxyToUse, ignoreSslErrors);
                client = new HttpClient(handler)
                {
                    // 应用超时设置
                    Timeout = options?.Timeout.HasValue == true ? options.Timeout.Value : TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds)
                };
            }
            else
            {
                client = GetHttpClient(options);
                ConfigureRequest(client, options);
            }

            try
            {
                var fullUrl = BuildUrl(url, options);
                using var request = new HttpRequestMessage(method, fullUrl) { Content = content };

                AddHeaders(request, options, requestId);

                // 将断路器、重试和日志选项传递到请求上下文中
                if (options?.EnableCircuitBreaker.HasValue == true)
                {
                    request.Options.Set(new HttpRequestOptionsKey<bool>("EnableCircuitBreaker"), options.EnableCircuitBreaker.Value);
                }
                if (options?.EnableRetry.HasValue == true)
                {
                    request.Options.Set(new HttpRequestOptionsKey<bool>("EnableRetry"), options.EnableRetry.Value);
                }
                if (options?.LogRequest.HasValue == true)
                {
                    request.Options.Set(new HttpRequestOptionsKey<bool>("LogRequest"), options.LogRequest.Value);
                }
                if (options?.LogResponse.HasValue == true)
                {
                    request.Options.Set(new HttpRequestOptionsKey<bool>("LogResponse"), options.LogResponse.Value);
                }

                using var response = await client.SendAsync(request, effectiveToken);

                var result = new HttpResult<T>
                {
                    IsSuccess = response.IsSuccessStatusCode,
                    StatusCode = response.StatusCode,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    RequestUrl = fullUrl,
                    RequestMethod = method.Method,
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

                result.RawDataString = await DeserializeResponseAsync<string>(response, effectiveToken);

                if (response.IsSuccessStatusCode)
                {
                    result.Data = await DeserializeResponseAsync<T>(response, effectiveToken);
                }
                else
                {
                    result.ErrorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    if (response.Content != null)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(effectiveToken);
                        if (!string.IsNullOrEmpty(errorContent))
                        {
                            // 限制错误内容长度，避免刷爆日志
                            result.ErrorMessage += $": {errorContent.Truncate(_options.MaxResponseContentLength)}";
                        }
                    }
                }

                // 如果使用了代理池,记录结果
                if (proxyToUse != null && options?.UseProxyPool == true && _proxyPoolManager != null)
                {
                    _proxyPoolManager.RecordProxyResult(
                        proxyToUse.GetProxyAddress(),
                        response.IsSuccessStatusCode,
                        stopwatch.ElapsedMilliseconds);
                }

                return result;
            }
            finally
            {
                // 如果创建了临时客户端,需要释放
                if (handler != null)
                {
                    client.Dispose();
                    handler.Dispose();
                }
            }
        }
        catch (TaskCanceledException ex) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "HTTP请求超时。方法: {Method}, Url: {Url}, 请求唯一标识: {RequestId}, 超时时间: {Timeout}秒",
                method.Method, url, requestId, effectiveTimeout.TotalSeconds);

            return HttpResult<T>.Failure($"请求超时（超过 {effectiveTimeout.TotalSeconds} 秒）", HttpStatusCode.RequestTimeout, ex)
                .SetElapsedMilliseconds(stopwatch.ElapsedMilliseconds)
                .SetRequestInfo(method.Method, url);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "HTTP请求被取消。方法: {Method}, Url: {Url}, 请求唯一标识: {RequestId}",
                method.Method, url, requestId);

            return HttpResult<T>.Failure("请求被取消", HttpStatusCode.RequestTimeout, ex)
                .SetElapsedMilliseconds(stopwatch.ElapsedMilliseconds)
                .SetRequestInfo(method.Method, url);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "断路器阻止了HTTP请求。方法: {Method}, Url: {Url}, 请求唯一标识: {RequestId}, 断路器将在 {DurationSeconds} 秒后重置",
                method.Method, url, requestId, _options.CircuitBreakerDurationOfBreakSeconds);

            return HttpResult<T>.Failure($"断路器已打开，请求被阻止。请等待约 {_options.CircuitBreakerDurationOfBreakSeconds} 秒后重试", HttpStatusCode.ServiceUnavailable, ex)
                .SetElapsedMilliseconds(stopwatch.ElapsedMilliseconds)
                .SetRequestInfo(method.Method, url);
        }
        catch (Exception ex)
        {
            // 如果使用了代理池,记录失败
            if (proxyToUse != null && options?.UseProxyPool == true && _proxyPoolManager != null)
            {
                _proxyPoolManager.RecordProxyResult(
                    proxyToUse.GetProxyAddress(),
                    false,
                    stopwatch.ElapsedMilliseconds);
            }

            _logger.LogError(ex, "HTTP请求失败。方法: {Method}, Url: {Url}, 请求唯一标识: {RequestId}, 代理: {Proxy}",
                method.Method, url, requestId, proxyToUse?.GetProxyAddress() ?? "无");

            return HttpResult<T>.Failure(ex.Message, HttpStatusCode.InternalServerError, ex)
                .SetElapsedMilliseconds(stopwatch.ElapsedMilliseconds)
                .SetRequestInfo(method.Method, url);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    #region 内部方法

    /// <summary>
    /// 配置请求
    /// </summary>
    /// <param name="client">HTTP客户端</param>
    /// <param name="options">请求选项</param>
    private static void ConfigureRequest(HttpClient client, XiHanHttpRequestOptions? options)
    {
        if (options?.Timeout.HasValue == true)
        {
            client.Timeout = options.Timeout.Value;
        }
    }

    /// <summary>
    /// 构建完整Url
    /// </summary>
    /// <param name="url">基础Url</param>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    private static string BuildUrl(string url, XiHanHttpRequestOptions? options)
    {
        if (options?.QueryParameters == null || options.QueryParameters.Count == 0)
        {
            return url;
        }

        var queryString = options.BuildQueryString();
        return url.Contains('?') ? $"{url}&{queryString[1..]}" : $"{url}{queryString}";
    }

    /// <summary>
    /// 添加请求头
    /// </summary>
    /// <param name="request">HTTP请求消息</param>
    /// <param name="options">请求选项</param>
    /// <param name="requestId">请求唯一标识</param>
    private static void AddHeaders(HttpRequestMessage request, XiHanHttpRequestOptions? options, string requestId)
    {
        // 添加请求头
        if (options?.Headers != null)
        {
            foreach (var header in options.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        // 添加请求唯一标识
        request.Headers.TryAddWithoutValidation("X-Request-Id", requestId);
    }

    /// <summary>
    /// 创建代理处理器
    /// </summary>
    /// <param name="proxy">代理配置(可选)</param>
    /// <param name="ignoreSslErrors">是否忽略SSL错误</param>
    /// <returns></returns>
    private static HttpClientHandler CreateProxyHandler(ProxyConfiguration? proxy, bool ignoreSslErrors)
    {
        var handler = new HttpClientHandler();

        // 如果提供了代理配置，则设置代理
        if (proxy != null)
        {
            var scheme = proxy.Type switch
            {
                ProxyType.Http => "http",
                ProxyType.Https => "https",
                ProxyType.Socks4 => "socks4",
                ProxyType.Socks4A => "socks4a",
                ProxyType.Socks5 => "socks5",
                _ => "http"
            };

            var webProxy = new WebProxy
            {
                Address = new Uri($"{scheme}://{proxy.Host}:{proxy.Port}"),
                BypassProxyOnLocal = !proxy.UseProxyForLocalAddress,
                UseDefaultCredentials = false
            };

            // 设置绕过列表
            if (proxy.BypassList.Count > 0)
            {
                webProxy.BypassList = [.. proxy.BypassList];
            }

            // 设置认证
            if (!string.IsNullOrEmpty(proxy.Username) && !string.IsNullOrEmpty(proxy.Password))
            {
                webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
            }

            handler.Proxy = webProxy;
            handler.UseProxy = true;
        }

        // 设置SSL证书验证
        if (ignoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        return handler;
    }

    /// <summary>
    /// 获取HTTP客户端
    /// </summary>
    /// <param name="options">请求选项</param>
    /// <returns></returns>
    private HttpClient GetHttpClient(XiHanHttpRequestOptions? options)
    {
        // 默认使用远程客户端
        var clientName = HttpGroupEnum.Remote.ToString();

        // 可以根据Url或其他条件选择不同的客户端
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
    private StringContent CreateJsonContent(object data, XiHanHttpRequestOptions? options)
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
            return !DynamicJsonHelper.TryDeserialize(content, out var dynamicObject)
                ? throw new JsonException("无法将响应内容转换为动态对象")
                : (T?)dynamicObject;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken);
    }

    #endregion 内部方法
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
