#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MulanPSL2 License. See LICENSE in the project root for license information.
// FileName:HttpPollyService
// Guid:a0813c9d-590b-48e3-90f1-91d62780ea3d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-09-07 上午 03:12:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace XiHan.Framework.Http.Polly;

/// <summary>
/// HttpPollyService
/// </summary>
public class HttpPollyService : IHttpPollyService
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpClientFactory"></param>
    public HttpPollyService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Get 请求
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<TResponse?> GetAsync<TResponse>(HttpGroupEnum httpGroup, string url,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient(httpGroup.ToString());
        if (headers != null)
        {
            foreach (var header in headers.Where(header =>
                                 !client.DefaultRequestHeaders.Contains(header.Key)))
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        var response = await client.GetAsync(url, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(result);
    }

    /// <summary>
    /// Get 请求
    /// </summary>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string> GetAsync(HttpGroupEnum httpGroup, string url,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient(httpGroup.ToString());
        if (headers != null)
        {
            foreach (var header in headers.Where(header =>
                                 !client.DefaultRequestHeaders.Contains(header.Key)))
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        var response = await client.GetAsync(url, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

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
    public async Task<TResponse?> PostAsync<TResponse, TRequest>(HttpGroupEnum httpGroup, string url, TRequest request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient(httpGroup.ToString());
        if (headers != null)
        {
            foreach (var header in headers.Where(header =>
                                 !client.DefaultRequestHeaders.Contains(header.Key)))
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        StringContent stringContent = new(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, stringContent, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(result);
    }

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
    public async Task<TResponse?> PostAsync<TResponse>(HttpGroupEnum httpGroup, string url, FileStream fileStream,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient(httpGroup.ToString());
        using MultipartFormDataContent formDataContent = [];
        if (headers != null)
        {
            foreach (var header in headers.Where(header =>
                                 !formDataContent.Headers.Contains(header.Key)))
            {
                formDataContent.Headers.Add(header.Key, header.Value);
            }
        }

        formDataContent.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");
        formDataContent.Add(new StreamContent(fileStream, (int)fileStream.Length), "file", fileStream.Name);
        var response = await client.PostAsync(url, formDataContent, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(result);
    }

    /// <summary>
    /// Post 请求 下载文件
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Stream> PostAsync<TRequest>(HttpGroupEnum httpGroup, string url, TRequest request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient(httpGroup.ToString());
        if (headers != null)
        {
            foreach (var header in headers.Where(header =>
                                 !client.DefaultRequestHeaders.Contains(header.Key)))
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        StringContent stringContent = new(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, stringContent, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

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
    public async Task<TResponse?> PostAsync<TResponse>(HttpGroupEnum httpGroup, string url, string request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient(httpGroup.ToString());
        if (headers != null)
        {
            foreach (var header in headers.Where(header =>
                                 !client.DefaultRequestHeaders.Contains(header.Key)))
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        StringContent stringContent = new(request, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, stringContent, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(result);
    }

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
    public async Task<string> PostAsync<TRequest>(HttpGroupEnum httpGroup, string url, TRequest request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient(httpGroup.ToString());
        if (headers != null)
        {
            foreach (var header in headers.Where(header =>
                                 !client.DefaultRequestHeaders.Contains(header.Key)))
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        StringContent stringContent = new(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, stringContent, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Post 请求
    /// </summary>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string> PostAsync(HttpGroupEnum httpGroup, string url, string request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient(httpGroup.ToString());
        if (headers != null)
        {
            foreach (var header in headers.Where(header =>
                                 !client.DefaultRequestHeaders.Contains(header.Key)))
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        StringContent stringContent = new(request, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, stringContent, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

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
    public async Task<TResponse?> PutAsync<TResponse, TRequest>(HttpGroupEnum httpGroup, string url, TRequest request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient(httpGroup.ToString());
        if (headers != null)
        {
            foreach (var header in headers.Where(header =>
                                 !client.DefaultRequestHeaders.Contains(header.Key)))
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        StringContent stringContent = new(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await client.PutAsync(url, stringContent, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(result);
    }

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
    public async Task<TResponse?> PutAsync<TResponse>(HttpGroupEnum httpGroup, string url, string request,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient(httpGroup.ToString());
        if (headers != null)
        {
            foreach (var header in headers.Where(header =>
                                 !client.DefaultRequestHeaders.Contains(header.Key)))
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        StringContent stringContent = new(request, Encoding.UTF8, "application/json");
        var response = await client.PutAsync(url, stringContent, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(result);
    }

    /// <summary>
    /// Delete 请求
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<TResponse?> DeleteAsync<TResponse>(HttpGroupEnum httpGroup, string url,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient(httpGroup.ToString());
        if (headers != null)
        {
            foreach (var header in headers.Where(header =>
                                 !client.DefaultRequestHeaders.Contains(header.Key)))
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        var response = await client.DeleteAsync(url, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(result);
    }
}
