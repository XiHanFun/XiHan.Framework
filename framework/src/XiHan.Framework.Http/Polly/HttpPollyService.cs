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

using System.Net;
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
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public async Task<TEntity?> GetAsync<TEntity>(HttpGroupEnum httpGroup, string url,
        Dictionary<string, string>? headers = null)
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

        var response = await client.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TEntity>(result);
        }
        else
        {
            throw new Exception($"Http Error StatusCode:{response.StatusCode}");
        }
    }

    /// <summary>
    /// Get 请求
    /// </summary>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public async Task<string> GetAsync(HttpGroupEnum httpGroup, string url,
        Dictionary<string, string>? headers = null)
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

        var response = await client.GetAsync(url);
        return response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadAsStringAsync()
            : throw new Exception($"Http Error StatusCode:{response.StatusCode}");
    }

    /// <summary>
    /// Post 请求
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TREntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public async Task<TEntity?> PostAsync<TEntity, TREntity>(HttpGroupEnum httpGroup, string url, TREntity request,
        Dictionary<string, string>? headers = null)
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
        var response = await client.PostAsync(url, stringContent);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TEntity>(result);
        }
        else
        {
            throw new Exception($"Http Error StatusCode:{response.StatusCode}");
        }
    }

    /// <summary>
    /// Post 请求 上传文件
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="fileStream"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public async Task<TEntity?> PostAsync<TEntity>(HttpGroupEnum httpGroup, string url, FileStream fileStream,
        Dictionary<string, string>? headers = null)
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
        var response = await client.PostAsync(url, formDataContent);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TEntity>(result);
        }
        else
        {
            throw new Exception($"Http Error StatusCode:{response.StatusCode}");
        }
    }

    /// <summary>
    /// Post 请求
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public async Task<TEntity?> PostAsync<TEntity>(HttpGroupEnum httpGroup, string url, string request,
        Dictionary<string, string>? headers = null)
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
        var response = await client.PostAsync(url, stringContent);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TEntity>(result);
        }
        else
        {
            throw new Exception($"Http Error StatusCode:{response.StatusCode}");
        }
    }

    /// <summary>
    /// Post 请求
    /// </summary>
    /// <typeparam name="TREntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public async Task<string> PostAsync<TREntity>(HttpGroupEnum httpGroup, string url, TREntity request,
        Dictionary<string, string>? headers = null)
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
        var response = await client.PostAsync(url, stringContent);
        return response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadAsStringAsync()
            : throw new Exception($"Http Error StatusCode:{response.StatusCode}");
    }

    /// <summary>
    /// Post 请求
    /// </summary>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public async Task<string> PostAsync(HttpGroupEnum httpGroup, string url, string request,
        Dictionary<string, string>? headers = null)
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
        var response = await client.PostAsync(url, stringContent);
        return response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadAsStringAsync()
            : throw new Exception($"Http Error StatusCode:{response.StatusCode}");
    }

    /// <summary>
    /// Put 请求
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TREntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public async Task<TEntity?> PutAsync<TEntity, TREntity>(HttpGroupEnum httpGroup, string url, TREntity request,
        Dictionary<string, string>? headers = null)
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
        var response = await client.PutAsync(url, stringContent);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TEntity>(result);
        }
        else
        {
            throw new Exception($"Http Error StatusCode:{response.StatusCode}");
        }
    }

    /// <summary>
    /// Put 请求
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public async Task<TEntity?> PutAsync<TEntity>(HttpGroupEnum httpGroup, string url, string request,
        Dictionary<string, string>? headers = null)
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
        var response = await client.PutAsync(url, stringContent);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TEntity>(result);
        }
        else
        {
            throw new Exception($"Http Error StatusCode:{response.StatusCode}");
        }
    }

    /// <summary>
    /// Delete 请求
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="httpGroup"></param>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public async Task<TEntity?> DeleteAsync<TEntity>(HttpGroupEnum httpGroup, string url,
        Dictionary<string, string>? headers = null)
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

        var response = await client.DeleteAsync(url);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TEntity>(result);
        }
        else
        {
            throw new Exception($"Http Error StatusCode:{response.StatusCode}");
        }
    }
}
