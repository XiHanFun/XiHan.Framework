#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HttpRequestBuilder
// Guid:1cc8f386-ad8e-421c-b923-ce22d4474224
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 20:14:48
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Http.Models;
using XiHan.Framework.Http.Services;
using HttpRequestOptions = XiHan.Framework.Http.Options.HttpRequestOptions;

namespace XiHan.Framework.Http.Fluent;

/// <summary>
/// HTTP 请求构建器
/// </summary>
public class HttpRequestBuilder
{
    private readonly IAdvancedHttpService _httpService;
    private readonly string _url;
    private readonly HttpRequestOptions _options;
    private object? _body;
    private string? _bodyContent;
    private string? _contentType;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpService">HTTP服务</param>
    /// <param name="url">请求URL</param>
    internal HttpRequestBuilder(IAdvancedHttpService httpService, string url)
    {
        _httpService = httpService;
        _url = url;
        _options = new HttpRequestOptions();
    }

    /// <summary>
    /// 设置请求头
    /// </summary>
    /// <param name="name">头名称</param>
    /// <param name="value">头值</param>
    /// <returns></returns>
    public HttpRequestBuilder SetHeader(string name, string value)
    {
        _options.AddHeader(name, value);
        return this;
    }

    /// <summary>
    /// 设置多个请求头
    /// </summary>
    /// <param name="headers">请求头字典</param>
    /// <returns></returns>
    public HttpRequestBuilder SetHeaders(Dictionary<string, string> headers)
    {
        foreach (var header in headers)
        {
            _options.AddHeader(header.Key, header.Value);
        }
        return this;
    }

    /// <summary>
    /// 设置授权头
    /// </summary>
    /// <param name="token">令牌</param>
    /// <param name="scheme">认证方案</param>
    /// <returns></returns>
    public HttpRequestBuilder SetAuthorization(string token, string scheme = "Bearer")
    {
        _options.WithAuthorization(token, scheme);
        return this;
    }

    /// <summary>
    /// 设置基本认证
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns></returns>
    public HttpRequestBuilder SetBasicAuth(string username, string password)
    {
        _options.WithBasicAuth(username, password);
        return this;
    }

    /// <summary>
    /// 设置查询参数
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <returns></returns>
    public HttpRequestBuilder SetQuery(string name, string value)
    {
        _options.AddQueryParameter(name, value);
        return this;
    }

    /// <summary>
    /// 设置多个查询参数
    /// </summary>
    /// <param name="parameters">查询参数字典</param>
    /// <returns></returns>
    public HttpRequestBuilder SetQueries(Dictionary<string, string> parameters)
    {
        foreach (var param in parameters)
        {
            _options.AddQueryParameter(param.Key, param.Value);
        }
        return this;
    }

    /// <summary>
    /// 设置请求体(对象)
    /// </summary>
    /// <param name="body">请求体对象</param>
    /// <param name="contentType">内容类型</param>
    /// <returns></returns>
    public HttpRequestBuilder SetBody(object body, string contentType = "application/json")
    {
        _body = body;
        _contentType = contentType;
        _options.ContentType = contentType;
        return this;
    }

    /// <summary>
    /// 设置请求体(字符串)
    /// </summary>
    /// <param name="content">请求体内容</param>
    /// <param name="contentType">内容类型</param>
    /// <returns></returns>
    public HttpRequestBuilder SetBodyContent(string content, string contentType = "application/json")
    {
        _bodyContent = content;
        _contentType = contentType;
        _options.ContentType = contentType;
        return this;
    }

    /// <summary>
    /// 设置JSON请求体
    /// </summary>
    /// <param name="body">请求体对象</param>
    /// <returns></returns>
    public HttpRequestBuilder SetJsonBody(object body)
    {
        return SetBody(body, "application/json");
    }

    /// <summary>
    /// 设置表单数据
    /// </summary>
    /// <param name="formData">表单数据</param>
    /// <returns></returns>
    public HttpRequestBuilder SetFormData(Dictionary<string, string> formData)
    {
        _body = formData;
        _contentType = "application/x-www-form-urlencoded";
        _options.ContentType = _contentType;
        return this;
    }

    /// <summary>
    /// 设置超时时间
    /// </summary>
    /// <param name="timeout">超时时间</param>
    /// <returns></returns>
    public HttpRequestBuilder SetTimeout(TimeSpan timeout)
    {
        _options.SetTimeout(timeout);
        return this;
    }

    /// <summary>
    /// 设置超时时间(秒)
    /// </summary>
    /// <param name="seconds">超时秒数</param>
    /// <returns></returns>
    public HttpRequestBuilder SetTimeout(int seconds)
    {
        return SetTimeout(TimeSpan.FromSeconds(seconds));
    }

    /// <summary>
    /// 设置请求ID
    /// </summary>
    /// <param name="requestId">请求ID</param>
    /// <returns></returns>
    public HttpRequestBuilder SetRequestId(string requestId)
    {
        _options.SetRequestId(requestId);
        return this;
    }

    /// <summary>
    /// 设置关联ID
    /// </summary>
    /// <param name="correlationId">关联ID</param>
    /// <returns></returns>
    public HttpRequestBuilder SetCorrelationId(string? correlationId = null)
    {
        _options.WithCorrelationId(correlationId);
        return this;
    }

    /// <summary>
    /// 设置用户代理
    /// </summary>
    /// <param name="userAgent">用户代理</param>
    /// <returns></returns>
    public HttpRequestBuilder SetUserAgent(string userAgent)
    {
        _options.WithUserAgent(userAgent);
        return this;
    }

    /// <summary>
    /// 设置接受语言
    /// </summary>
    /// <param name="language">语言代码</param>
    /// <returns></returns>
    public HttpRequestBuilder SetLanguage(string language)
    {
        _options.WithLanguage(language);
        return this;
    }

    /// <summary>
    /// 使用指定的HTTP客户端
    /// </summary>
    /// <param name="clientName">客户端名称</param>
    /// <returns></returns>
    public HttpRequestBuilder UseClient(string clientName)
    {
        _options.UseClient(clientName);
        return this;
    }

    /// <summary>
    /// 禁用重试
    /// </summary>
    /// <returns></returns>
    public HttpRequestBuilder WithoutRetry()
    {
        _options.WithoutRetry();
        return this;
    }

    /// <summary>
    /// 禁用熔断器
    /// </summary>
    /// <returns></returns>
    public HttpRequestBuilder WithoutCircuitBreaker()
    {
        _options.WithoutCircuitBreaker();
        return this;
    }

    /// <summary>
    /// 禁用缓存
    /// </summary>
    /// <returns></returns>
    public HttpRequestBuilder WithoutCache()
    {
        _options.WithoutCache();
        return this;
    }

    /// <summary>
    /// 启用详细日志
    /// </summary>
    /// <returns></returns>
    public HttpRequestBuilder WithVerboseLogging()
    {
        _options.WithVerboseLogging();
        return this;
    }

    /// <summary>
    /// 禁用日志
    /// </summary>
    /// <returns></returns>
    public HttpRequestBuilder WithoutLogging()
    {
        _options.WithoutLogging();
        return this;
    }

    /// <summary>
    /// 添加标签
    /// </summary>
    /// <param name="key">标签键</param>
    /// <param name="value">标签值</param>
    /// <returns></returns>
    public HttpRequestBuilder AddTag(string key, object value)
    {
        _options.AddTag(key, value);
        return this;
    }

    /// <summary>
    /// 发送 GET 请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> GetAsync<T>(CancellationToken cancellationToken = default)
    {
        return await _httpService.GetAsync<T>(_url, _options, cancellationToken);
    }

    /// <summary>
    /// 发送 GET 请求(返回字符串)
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<string>> GetStringAsync(CancellationToken cancellationToken = default)
    {
        return await _httpService.GetStringAsync(_url, _options, cancellationToken);
    }

    /// <summary>
    /// 发送 GET 请求(返回字节数组)
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<byte[]>> GetBytesAsync(CancellationToken cancellationToken = default)
    {
        return await _httpService.GetBytesAsync(_url, _options, cancellationToken);
    }

    /// <summary>
    /// 发送 POST 请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> PostAsync<T>(CancellationToken cancellationToken = default)
    {
        if (_bodyContent != null)
        {
            return await _httpService.PostJsonAsync<T>(_url, _bodyContent, _options, cancellationToken);
        }

        if (_body != null)
        {
            if (_contentType == "application/x-www-form-urlencoded" && _body is Dictionary<string, string> formData)
            {
                return await _httpService.PostFormAsync<T>(_url, formData, _options, cancellationToken);
            }

            var json = JsonSerializer.Serialize(_body);
            return await _httpService.PostJsonAsync<T>(_url, json, _options, cancellationToken);
        }

        return await _httpService.PostJsonAsync<T>(_url, "{}", _options, cancellationToken);
    }

    /// <summary>
    /// 发送 POST 请求(返回字符串)
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<string>> PostStringAsync(CancellationToken cancellationToken = default)
    {
        return await PostAsync<string>(cancellationToken);
    }

    /// <summary>
    /// 发送 PUT 请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> PutAsync<T>(CancellationToken cancellationToken = default)
    {
        return _body == null
            ? throw new InvalidOperationException("PUT 请求需要设置请求体")
            : await _httpService.PutAsync<object, T>(_url, _body, _options, cancellationToken);
    }

    /// <summary>
    /// 发送 PATCH 请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> PatchAsync<T>(CancellationToken cancellationToken = default)
    {
        return _body == null
            ? throw new InvalidOperationException("PATCH 请求需要设置请求体")
            : await _httpService.PatchAsync<object, T>(_url, _body, _options, cancellationToken);
    }

    /// <summary>
    /// 发送 DELETE 请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult<T>> DeleteAsync<T>(CancellationToken cancellationToken = default)
    {
        return await _httpService.DeleteAsync<T>(_url, _options, cancellationToken);
    }

    /// <summary>
    /// 发送 DELETE 请求(无响应内容)
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> DeleteAsync(CancellationToken cancellationToken = default)
    {
        return await _httpService.DeleteAsync(_url, _options, cancellationToken);
    }

    /// <summary>
    /// 发送 HEAD 请求
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> HeadAsync(CancellationToken cancellationToken = default)
    {
        return await _httpService.HeadAsync(_url, _options, cancellationToken);
    }

    /// <summary>
    /// 发送 OPTIONS 请求
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> OptionsAsync(CancellationToken cancellationToken = default)
    {
        return await _httpService.OptionsAsync(_url, _options, cancellationToken);
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="destinationPath">目标路径</param>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<HttpResult> DownloadAsync(string destinationPath, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        return await _httpService.DownloadFileAsync(_url, destinationPath, progress, _options, cancellationToken);
    }
}
