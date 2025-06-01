#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:StringHttpExtensions
// Guid:4a38dfab-3c8f-4b9d-860d-f647a4070932
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 20:14:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Http.Fluent;
using XiHan.Framework.Http.Models;
using XiHan.Framework.Http.Services;

namespace XiHan.Framework.Http.Extensions;

/// <summary>
/// 字符串 HTTP 扩展方法
/// </summary>
public static class StringHttpExtensions
{
    private static IAdvancedHttpService? _httpService;

    /// <summary>
    /// 设置HTTP服务(通常在应用启动时调用)
    /// </summary>
    /// <param name="httpService">HTTP服务实例</param>
    public static void SetHttpService(IAdvancedHttpService httpService)
    {
        _httpService = httpService;
    }

    /// <summary>
    /// 从服务提供者获取HTTP服务
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    public static void SetHttpService(IServiceProvider serviceProvider)
    {
        _httpService = serviceProvider.GetRequiredService<IAdvancedHttpService>();
    }

    /// <summary>
    /// 创建HTTP请求构建器
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <returns></returns>
    public static HttpRequestBuilder AsHttp(this string url)
    {
        return new HttpRequestBuilder(GetHttpService(), url);
    }

    /// <summary>
    /// 设置请求头
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="name">头名称</param>
    /// <param name="value">头值</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetHeader(this string url, string name, string value)
    {
        return url.AsHttp().SetHeader(name, value);
    }

    /// <summary>
    /// 设置多个请求头
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="headers">请求头字典</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetHeaders(this string url, Dictionary<string, string> headers)
    {
        return url.AsHttp().SetHeaders(headers);
    }

    /// <summary>
    /// 设置授权头
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="token">令牌</param>
    /// <param name="scheme">认证方案</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetAuthorization(this string url, string token, string scheme = "Bearer")
    {
        return url.AsHttp().SetAuthorization(token, scheme);
    }

    /// <summary>
    /// 设置基本认证
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetBasicAuth(this string url, string username, string password)
    {
        return url.AsHttp().SetBasicAuth(username, password);
    }

    /// <summary>
    /// 设置查询参数
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetQuery(this string url, string name, string value)
    {
        return url.AsHttp().SetQuery(name, value);
    }

    /// <summary>
    /// 设置多个查询参数
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="parameters">查询参数字典</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetQueries(this string url, Dictionary<string, string> parameters)
    {
        return url.AsHttp().SetQueries(parameters);
    }

    /// <summary>
    /// 设置请求体
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="body">请求体对象</param>
    /// <param name="contentType">内容类型</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetBody(this string url, object body, string contentType = "application/json")
    {
        return url.AsHttp().SetBody(body, contentType);
    }

    /// <summary>
    /// 设置JSON请求体
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="body">请求体对象</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetJsonBody(this string url, object body)
    {
        return url.AsHttp().SetJsonBody(body);
    }

    /// <summary>
    /// 设置表单数据
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="formData">表单数据</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetFormData(this string url, Dictionary<string, string> formData)
    {
        return url.AsHttp().SetFormData(formData);
    }

    /// <summary>
    /// 设置超时时间
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="timeout">超时时间</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetTimeout(this string url, TimeSpan timeout)
    {
        return url.AsHttp().SetTimeout(timeout);
    }

    /// <summary>
    /// 设置超时时间(秒)
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="seconds">超时秒数</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetTimeout(this string url, int seconds)
    {
        return url.AsHttp().SetTimeout(seconds);
    }

    /// <summary>
    /// 使用指定的HTTP客户端
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="clientName">客户端名称</param>
    /// <returns></returns>
    public static HttpRequestBuilder UseClient(this string url, string clientName)
    {
        return url.AsHttp().UseClient(clientName);
    }

    /// <summary>
    /// 快速GET请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResult<T>> GetAsync<T>(this string url, CancellationToken cancellationToken = default)
    {
        return await url.AsHttp().GetAsync<T>(cancellationToken);
    }

    /// <summary>
    /// 快速GET请求(返回字符串)
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResult<string>> GetStringAsync(this string url, CancellationToken cancellationToken = default)
    {
        return await url.AsHttp().GetStringAsync(cancellationToken);
    }

    /// <summary>
    /// 快速POST请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="body">请求体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResult<T>> PostAsync<T>(this string url, object? body = null, CancellationToken cancellationToken = default)
    {
        var builder = url.AsHttp();
        if (body != null)
        {
            builder.SetJsonBody(body);
        }
        return await builder.PostAsync<T>(cancellationToken);
    }

    /// <summary>
    /// 快速PUT请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="body">请求体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResult<T>> PutAsync<T>(this string url, object body, CancellationToken cancellationToken = default)
    {
        return await url.AsHttp().SetJsonBody(body).PutAsync<T>(cancellationToken);
    }

    /// <summary>
    /// 快速DELETE请求
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResult> DeleteAsync(this string url, CancellationToken cancellationToken = default)
    {
        return await url.AsHttp().DeleteAsync(cancellationToken);
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="url">文件URL</param>
    /// <param name="destinationPath">目标路径</param>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResult> DownloadAsync(this string url, string destinationPath,
        IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        return await url.AsHttp().DownloadAsync(destinationPath, progress, cancellationToken);
    }

    /// <summary>
    /// 获取HTTP服务实例
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">HTTP服务未初始化时抛出</exception>
    private static IAdvancedHttpService GetHttpService()
    {
        return _httpService ?? throw new InvalidOperationException(
            "HTTP服务未初始化。请在应用启动时调用 StringHttpExtensions.SetHttpService() 方法。");
    }
}
