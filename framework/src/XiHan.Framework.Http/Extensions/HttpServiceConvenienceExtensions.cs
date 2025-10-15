#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HttpServiceConvenienceExtensions
// Guid:c6b25fa3-36ee-48d3-8804-d4330429cff0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 16:17:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Http.Models;
using XiHan.Framework.Http.Options;
using XiHan.Framework.Http.Services;

namespace XiHan.Framework.Http.Extensions;

/// <summary>
/// HTTP 服务便捷方法
/// </summary>
public static class HttpServiceConvenienceExtensions
{
    /// <summary>
    /// 快速GET请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="httpService">HTTP服务</param>
    /// <param name="url">Url</param>
    /// <param name="token">授权令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResult<T>> QuickGetAsync<T>(
        this IAdvancedHttpService httpService,
        string url,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        var options = new XiHanHttpRequestOptions();

        if (!string.IsNullOrEmpty(token))
        {
            options.WithAuthorization(token);
        }

        return await httpService.GetAsync<T>(url, options, cancellationToken);
    }

    /// <summary>
    /// 快速POST请求
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="httpService">HTTP服务</param>
    /// <param name="url">Url</param>
    /// <param name="request">请求数据</param>
    /// <param name="token">授权令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResult<TResponse>> QuickPostAsync<TRequest, TResponse>(
        this IAdvancedHttpService httpService,
        string url,
        TRequest request,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        var options = new XiHanHttpRequestOptions().AsJson();

        if (!string.IsNullOrEmpty(token))
        {
            options.WithAuthorization(token);
        }

        return await httpService.PostAsync<TRequest, TResponse>(url, request, options, cancellationToken);
    }

    /// <summary>
    /// 快速PUT请求
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="httpService">HTTP服务</param>
    /// <param name="url">Url</param>
    /// <param name="request">请求数据</param>
    /// <param name="token">授权令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResult<TResponse>> QuickPutAsync<TRequest, TResponse>(
        this IAdvancedHttpService httpService,
        string url,
        TRequest request,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        var options = new XiHanHttpRequestOptions().AsJson();

        if (!string.IsNullOrEmpty(token))
        {
            options.WithAuthorization(token);
        }

        return await httpService.PutAsync<TRequest, TResponse>(url, request, options, cancellationToken);
    }

    /// <summary>
    /// 快速DELETE请求
    /// </summary>
    /// <param name="httpService">HTTP服务</param>
    /// <param name="url">Url</param>
    /// <param name="token">授权令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResult> QuickDeleteAsync(
        this IAdvancedHttpService httpService,
        string url,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        var options = new XiHanHttpRequestOptions();

        if (!string.IsNullOrEmpty(token))
        {
            options.WithAuthorization(token);
        }

        return await httpService.DeleteAsync(url, options, cancellationToken);
    }

    /// <summary>
    /// 分页GET请求
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="httpService">HTTP服务</param>
    /// <param name="url">Url</param>
    /// <param name="page">页码</param>
    /// <param name="size">页大小</param>
    /// <param name="token">授权令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<HttpResult<T>> GetPagedAsync<T>(
        this IAdvancedHttpService httpService,
        string url,
        int page = 1,
        int size = 20,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        var options = new XiHanHttpRequestOptions()
            .AddQueryParameter("page", page.ToString())
            .AddQueryParameter("size", size.ToString());

        if (!string.IsNullOrEmpty(token))
        {
            options.WithAuthorization(token);
        }

        return await httpService.GetAsync<T>(url, options, cancellationToken);
    }
}
