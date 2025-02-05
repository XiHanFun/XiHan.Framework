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

namespace XiHan.Framework.Http.Polly;

/// <summary>
/// IHttpPollyService
/// </summary>
public interface IHttpPollyService
{
    /// <summary>
    /// Get 请求
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity?> GetAsync<TEntity>(HttpGroupEnum httpGroup, string url,
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
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TREntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity?> PostAsync<TEntity, TREntity>(HttpGroupEnum httpGroup, string url, TREntity request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Post 请求 上传文件
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="fileStream"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity?> PostAsync<TEntity>(HttpGroupEnum httpGroup, string url, FileStream fileStream,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Post 请求
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity?> PostAsync<TEntity>(HttpGroupEnum httpGroup, string url, string request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Post 请求
    /// </summary>
    /// <typeparam name="TREntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> PostAsync<TREntity>(HttpGroupEnum httpGroup, string url, TREntity request,
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
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TREntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity?> PutAsync<TEntity, TREntity>(HttpGroupEnum httpGroup, string url, TREntity request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Put 请求
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity?> PutAsync<TEntity>(HttpGroupEnum httpGroup, string url, string request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete 请求
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity?> DeleteAsync<TEntity>(HttpGroupEnum httpGroup, string url,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);
}
