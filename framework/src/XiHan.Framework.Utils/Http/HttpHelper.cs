#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HttpHelper
// Guid:7afa9878-a394-4997-a4cf-3dcc833de591
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/9 6:35:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using System.Text.Json;

namespace XiHan.Framework.Utils.Http;

/// <summary>
/// 简单的 HTTP 请求工具类
/// </summary>
public static class HttpHelper
{
    private static readonly HttpClient HttpClient = new();

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static HttpHelper()
    {
        // 设置默认超时时间
        HttpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    #region 公共方法

    /// <summary>
    /// GET 请求
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public static async Task<T?> GetAsync<T>(string url, Dictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request, headers);

        var response = await HttpClient.SendAsync(request);
        return await HandleResponse<T>(response);
    }

    /// <summary>
    /// POST 请求 (JSON)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="url"></param>
    /// <param name="data"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public static async Task<T?> PostAsync<T>(string url, object? data = null, Dictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = SerializeJson(data)
        };
        AddHeaders(request, headers);

        var response = await HttpClient.SendAsync(request);
        return await HandleResponse<T>(response);
    }

    /// <summary>
    /// PUT 请求 (JSON)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="url"></param>
    /// <param name="data"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public static async Task<T?> PutAsync<T>(string url, object? data = null, Dictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = SerializeJson(data)
        };
        AddHeaders(request, headers);

        var response = await HttpClient.SendAsync(request);
        return await HandleResponse<T>(response);
    }

    /// <summary>
    /// DELETE 请求
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public static async Task<T?> DeleteAsync<T>(string url, Dictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        AddHeaders(request, headers);

        var response = await HttpClient.SendAsync(request);
        return await HandleResponse<T>(response);
    }

    #endregion 公共方法

    #region 私有方法

    /// <summary>
    /// 添加请求头
    /// </summary>
    /// <param name="request"></param>
    /// <param name="headers"></param>
    private static void AddHeaders(HttpRequestMessage request, Dictionary<string, string>? headers)
    {
        if (headers != null)
        {
            foreach (var header in headers)
            {
                _ = request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }

    /// <summary>
    /// 序列化为 JSON
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    private static StringContent SerializeJson(object? data)
    {
        if (data == null)
        {
            return new StringContent("");
        }

        var json = JsonSerializer.Serialize(data);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// 处理 HTTP 响应
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="response"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    private static async Task<T?> HandleResponse<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"请求失败，状态码: {response.StatusCode}, 错误信息: {error}");
        }

        var responseData = await response.Content.ReadAsStringAsync();
        return typeof(T) == typeof(string) ? (T)(object)responseData : JsonSerializer.Deserialize<T>(responseData);
    }

    #endregion 私有方法
}
