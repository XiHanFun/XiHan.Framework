﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAdvancedHttpService
// Guid:9af99a55-d4bb-4a9d-b6de-48d36e3778d4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 20:18:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Http.Models;
using HttpRequestOptions = XiHan.Framework.Http.Options.HttpRequestOptions;

namespace XiHan.Framework.Http.Services;

/// <summary>
/// 高级 HTTP 服务接口
/// </summary>
public interface IAdvancedHttpService
{
    /// <summary>
    /// 发送 GET 请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<HttpResult<T>> GetAsync<T>(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送 GET 请求（返回字符串）
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<HttpResult<string>> GetStringAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送 GET 请求（返回字节数组）
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<HttpResult<byte[]>> GetBytesAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送 GET 请求（返回流）
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<HttpResult<Stream>> GetStreamAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

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
    Task<HttpResult<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest request, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送 POST 请求（JSON）
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="jsonContent">JSON内容</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<HttpResult<T>> PostJsonAsync<T>(string url, string jsonContent, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送 POST 请求（表单数据）
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="formData">表单数据</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<HttpResult<T>> PostFormAsync<T>(string url, Dictionary<string, string> formData, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

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
    Task<HttpResult<T>> UploadFileAsync<T>(string url, Stream fileStream, string fileName, string fieldName = "file",
        Dictionary<string, string>? additionalData = null, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

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
    Task<HttpResult<T>> UploadFilesAsync<T>(string url, IEnumerable<FileUploadInfo> files,
        Dictionary<string, string>? additionalData = null, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

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
    Task<HttpResult<TResponse>> PutAsync<TRequest, TResponse>(string url, TRequest request, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

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
    Task<HttpResult<TResponse>> PatchAsync<TRequest, TResponse>(string url, TRequest request, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送 DELETE 请求
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<HttpResult<T>> DeleteAsync<T>(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送 DELETE 请求（无响应内容）
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<HttpResult> DeleteAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送 HEAD 请求
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<HttpResult> HeadAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送 OPTIONS 请求
    /// </summary>
    /// <param name="url">请求URL</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<HttpResult> OptionsAsync(string url, HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="url">文件URL</param>
    /// <param name="destinationPath">目标路径</param>
    /// <param name="progress">进度回调</param>
    /// <param name="options">请求选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<HttpResult> DownloadFileAsync(string url, string destinationPath, IProgress<long>? progress = null,
        HttpRequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量请求
    /// </summary>
    /// <param name="requests">请求列表</param>
    /// <param name="maxConcurrency">最大并发数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<IEnumerable<HttpResult<object>>> BatchRequestAsync(IEnumerable<BatchRequestInfo> requests,
        int maxConcurrency = 10, CancellationToken cancellationToken = default);
}

/// <summary>
/// 文件上传信息
/// </summary>
public class FileUploadInfo
{
    /// <summary>
    /// 文件流
    /// </summary>
    public Stream FileStream { get; set; } = null!;

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// 字段名
    /// </summary>
    public string FieldName { get; set; } = "file";

    /// <summary>
    /// 内容类型
    /// </summary>
    public string? ContentType { get; set; }
}

/// <summary>
/// 批量请求信息
/// </summary>
public class BatchRequestInfo
{
    /// <summary>
    /// 请求方法
    /// </summary>
    public HttpMethod Method { get; set; } = HttpMethod.Get;

    /// <summary>
    /// 请求URL
    /// </summary>
    public string Url { get; set; } = null!;

    /// <summary>
    /// 请求内容
    /// </summary>
    public object? Content { get; set; }

    /// <summary>
    /// 请求选项
    /// </summary>
    public HttpRequestOptions? Options { get; set; }

    /// <summary>
    /// 请求标识
    /// </summary>
    public string? Id { get; set; }
}
