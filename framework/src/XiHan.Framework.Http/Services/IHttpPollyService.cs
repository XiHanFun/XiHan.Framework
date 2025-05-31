#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MulanPSL2 License. See LICENSE in the project root for license information.
// FileName:IHttpPollyService
// Guid:6cd09b99-c24d-4ef5-b8ca-15aa97f898c5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-12-06 下午 03:22:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Http.Enums;

namespace XiHan.Framework.Http.Services;

/// <summary>
/// IHttpPollyService
/// </summary>
public interface IHttpPollyService
{
    /// <summary>
    /// Get 请求
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TResponse?> GetAsync<TResponse>(HttpGroupEnum httpGroup, string url,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get 请求
    /// </summary>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> GetAsync(HttpGroupEnum httpGroup, string url,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Post 请求
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <typeparam name="TRequest"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TResponse?> PostAsync<TResponse, TRequest>(HttpGroupEnum httpGroup, string url, TRequest request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Post 请求 上传文件
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="fileStream"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TResponse?> PostAsync<TResponse>(HttpGroupEnum httpGroup, string url, FileStream fileStream,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Post 请求
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TResponse?> PostAsync<TResponse>(HttpGroupEnum httpGroup, string url, string request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Post 请求
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> PostAsync<TRequest>(HttpGroupEnum httpGroup, string url, TRequest request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Post 请求
    /// </summary>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> PostAsync(HttpGroupEnum httpGroup, string url, string request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Put 请求
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <typeparam name="TRequest"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TResponse?> PutAsync<TResponse, TRequest>(HttpGroupEnum httpGroup, string url, TRequest request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Put 请求
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TResponse?> PutAsync<TResponse>(HttpGroupEnum httpGroup, string url, string request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete 请求
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TResponse?> DeleteAsync<TResponse>(HttpGroupEnum httpGroup, string url,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);
}
