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

namespace XiHan.Framework.Application.Contracts.Dtos;

/// <summary>
/// 统一返回模型（无数据）
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
    public string Message { get; set; } = "操作成功";

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
    /// 创建成功响应
    /// </summary>
    /// <param name="message">提示信息</param>
    /// <param name="traceId">追踪 ID</param>
    /// <returns></returns>
    public static ApiResponse Ok(string message = "操作成功", string? traceId = null)
        => new()
        {
            Code = ApiResponseCodes.Success,
            Message = message,
            TraceId = traceId
        };

    /// <summary>
    /// 创建失败响应
    /// </summary>
    /// <param name="message">错误信息</param>
    /// <param name="code">错误码</param>
    /// <param name="traceId">追踪 ID</param>
    /// <returns></returns>
    public static ApiResponse Fail(string message = "操作失败", ApiResponseCodes code = ApiResponseCodes.Failed, string? traceId = null)
        => new()
        {
            Code = code,
            Message = message,
            TraceId = traceId
        };
}

/// <summary>
/// 统一返回模型（包含数据）
/// </summary>
/// <typeparam name="T">返回数据类型</typeparam>
public class ApiResponse<T> : ApiResponse
{
    /// <summary>
    /// 返回数据
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    /// <param name="data">返回数据</param>
    /// <param name="message">提示信息</param>
    /// <param name="traceId">追踪 ID</param>
    /// <returns></returns>
    public static ApiResponse<T> Ok(T? data, string message = "操作成功", string? traceId = null)
        => new()
        {
            Code = ApiResponseCodes.Success,
            Message = message,
            Data = data,
            TraceId = traceId
        };

    /// <summary>
    /// 创建失败响应
    /// </summary>
    /// <param name="message">错误信息</param>
    /// <param name="code">错误码</param>
    /// <param name="data">可选返回数据</param>
    /// <param name="traceId">追踪 ID</param>
    /// <returns></returns>
    public static ApiResponse<T> Fail(
        string message = "操作失败",
        ApiResponseCodes code = ApiResponseCodes.Failed,
        T? data = default,
        string? traceId = null)
        => new()
        {
            Code = code,
            Message = message,
            Data = data,
            TraceId = traceId
        };
}
