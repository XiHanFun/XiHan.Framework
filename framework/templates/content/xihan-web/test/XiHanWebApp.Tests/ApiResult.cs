using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHanWebApp.Tests;

/// <summary>
/// 统一响应包装
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class ApiResult<T>
{
    /// <summary>
    /// 业务状态码
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 数据
    /// </summary>
    public T? Data { get; set; }
}

/// <summary>
/// 统一响应读取辅助
/// </summary>
public static class ApiResultReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// 读取统一响应包装
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="response">HTTP 响应</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>统一响应包装</returns>
    public static async Task<ApiResult<T>> ReadApiResultAsync<T>(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var result = await response.Content.ReadFromJsonAsync<ApiResult<T>>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("响应体为空。");
    }
}
