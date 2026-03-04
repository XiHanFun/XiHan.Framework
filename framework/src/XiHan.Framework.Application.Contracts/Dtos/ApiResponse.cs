#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApiResponse
// Guid:6ea22bcb-c6ee-4eaf-86dc-4f6a11a9d10d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Application.Contracts.Dtos;

/// <summary>
/// 统一返回模型
/// </summary>
public class ApiResponse
{
    /// <summary>
    /// 业务码（默认 200 表示成功，序列化到 JSON 为 int）
    /// </summary>
    public ApiResponseCodes Code { get; set; } = ApiResponseCodes.Success;

    /// <summary>
    /// 提示信息
    /// </summary>
    public string Message { get; set; } = ApiResponseCodes.Success.GetDescription();

    /// <summary>
    /// 返回数据
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 请求追踪 ID
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// 服务端时间（UTC）
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => Code == ApiResponseCodes.Success;

    /// <summary>
    /// 继续响应 100
    /// </summary>
    /// <returns></returns>
    public static ApiResponse Continue()
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.Continue,
            Message = ApiResponseCodes.Continue.GetDescription()
        };
    }

    /// <summary>
    /// 响应成功，返回通用数据 200
    /// </summary>
    /// <param name="data"></param>
    /// <param name="traceId"></param>
    /// <returns></returns>
    public static ApiResponse Success(object? data, string? traceId)
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.Success,
            Message = ApiResponseCodes.Success.GetDescription(),
            Data = data,
            TraceId = traceId
        };
    }

    /// <summary>
    /// 响应失败，访问出错 400
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <param name="traceId"></param>
    /// <returns></returns>
    public static ApiResponse BadRequest(string? errorMessage = null, string? traceId = null)
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.BadRequest,
            Message = ApiResponseCodes.BadRequest.GetDescription(),
            Data = errorMessage,
            TraceId = traceId
        };
    }

    /// <summary>
    /// 响应失败，访问未授权 401
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public static ApiResponse Unauthorized(string? errorMessage = null)
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.Unauthorized,
            Message = ApiResponseCodes.Unauthorized.GetDescription(),
            Data = errorMessage
        };
    }

    /// <summary>
    /// 响应失败，内容禁止访问 403
    /// </summary>
    /// <returns></returns>
    public static ApiResponse Forbidden()
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.Forbidden,
            Message = ApiResponseCodes.Forbidden.GetDescription()
        };
    }

    /// <summary>
    /// 响应失败，数据未找到 404
    /// </summary>
    /// <returns></returns>
    public static ApiResponse NotFound()
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.NotFound,
            Message = ApiResponseCodes.NotFound.GetDescription()
        };
    }

    /// <summary>
    /// 响应失败，参数不合法 422
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public static ApiResponse UnprocessableEntity(string? errorMessage = null)
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.UnprocessableEntity,
            Message = ApiResponseCodes.UnprocessableEntity.GetDescription(),
            Data = errorMessage
        };
    }

    /// <summary>
    /// 响应失败，并发请求过多 429
    /// </summary>
    /// <returns></returns>
    public static ApiResponse TooManyRequests()
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.TooManyRequests,
            Message = ApiResponseCodes.TooManyRequests.GetDescription()
        };
    }

    /// <summary>
    /// 响应失败，服务器内部错误 500
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <param name="traceId"></param>
    /// <returns></returns>
    public static ApiResponse Fail(string? errorMessage = null, string? traceId = null)
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.Failed,
            Message = ApiResponseCodes.Failed.GetDescription(),
            Data = errorMessage,
            TraceId = traceId
        };
    }

    /// <summary>
    /// 响应出错，服务不可用 501
    /// </summary>
    /// <returns></returns>
    public static ApiResponse ServiceUnavailable()
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.ServiceUnavailable,
            Message = ApiResponseCodes.ServiceUnavailable.GetDescription()
        };
    }
}

/// <summary>
/// 统一返回模型
/// </summary>
/// <typeparam name="T">返回数据类型</typeparam>
public class ApiResponse<T> : ApiResponse
{
    /// <summary>
    /// 返回数据
    /// </summary>
    public new T? Data { get; set; }

    /// <summary>
    /// 响应成功，返回通用数据 200
    /// </summary>
    /// <param name="data"></param>
    /// <param name="traceId"></param>
    /// <returns></returns>
    public static ApiResponse Success(T? data, string? traceId = null)
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.Success,
            Message = ApiResponseCodes.Success.GetDescription(),
            Data = data,
            TraceId = traceId
        };
    }

    /// <summary>
    /// 响应失败，服务器内部错误 500
    /// </summary>
    /// <param name="data"></param>
    /// <param name="traceId"></param>
    /// <returns></returns>
    public static ApiResponse Failed(T? data = default, string? traceId = null)
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.Failed,
            Message = ApiResponseCodes.Failed.GetDescription(),
            Data = data,
            TraceId = traceId
        };
    }
}
