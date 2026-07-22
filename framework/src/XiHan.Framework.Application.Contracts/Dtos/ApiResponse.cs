// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Application.Contracts.Dtos;

/// <summary>
/// 统一返回模型
/// </summary>
/// <remarks>
/// 所有接口的统一响应信封：<see cref="Code"/> 表示调用结果（见 <see cref="ApiResponseCodes"/>，
/// 序列化到 JSON 为 int），<see cref="Message"/> 为面向用户的提示信息，<see cref="Data"/> 承载业务数据或错误明细。
/// 建议通过本类的静态工厂方法构造，保证 Code 与 Message 语义一致。
/// </remarks>
public class ApiResponse
{
    /// <summary>
    /// 业务码（默认 <see cref="ApiResponseCodes.Success"/>，序列化到 JSON 为 int）
    /// </summary>
    public ApiResponseCodes Code { get; set; } = ApiResponseCodes.Success;

    /// <summary>
    /// 提示信息（默认取业务码的 <see cref="System.ComponentModel.DescriptionAttribute"/> 描述）
    /// </summary>
    public string Message { get; set; } = ApiResponseCodes.Success.GetDescription();

    /// <summary>
    /// 返回数据（成功时为业务数据；失败时可承载错误明细）
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 请求追踪 ID（用于跨日志/链路定位一次请求）
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// 服务端时间（UTC）
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 是否成功（Code 为 2xx 视为成功）
    /// </summary>
    public bool IsSuccess => (int)Code is >= 200 and < 300;

    /// <summary>
    /// 继续请求 100：请求已接收，客户端应继续发送剩余内容
    /// </summary>
    /// <returns>统一返回模型</returns>
    public static ApiResponse Continue()
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.Continue,
            Message = ApiResponseCodes.Continue.GetDescription()
        };
    }

    /// <summary>
    /// 请求成功 200：返回业务数据
    /// </summary>
    /// <param name="data">业务数据</param>
    /// <param name="traceId">请求追踪 ID</param>
    /// <returns>统一返回模型</returns>
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
    /// 资源创建成功 201：通常用于 POST 创建操作
    /// </summary>
    /// <param name="data">创建结果（如新资源标识或详情）</param>
    /// <param name="traceId">请求追踪 ID</param>
    /// <returns>统一返回模型</returns>
    public static ApiResponse Created(object? data = null, string? traceId = null)
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.Created,
            Message = ApiResponseCodes.Created.GetDescription(),
            Data = data,
            TraceId = traceId
        };
    }

    /// <summary>
    /// 请求错误 400：参数错误、格式错误或缺少必要参数
    /// </summary>
    /// <param name="errorMessage">错误明细（置于 Data 返回）</param>
    /// <param name="traceId">请求追踪 ID</param>
    /// <returns>统一返回模型</returns>
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
    /// 未授权 401：未通过身份认证，需重新登录或提供有效凭据
    /// </summary>
    /// <param name="errorMessage">错误明细（置于 Data 返回）</param>
    /// <returns>统一返回模型</returns>
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
    /// 禁止访问 403：已认证但没有访问该资源的权限
    /// </summary>
    /// <returns>统一返回模型</returns>
    public static ApiResponse Forbidden()
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.Forbidden,
            Message = ApiResponseCodes.Forbidden.GetDescription()
        };
    }

    /// <summary>
    /// 资源不存在 404：请求的资源不存在或已被删除
    /// </summary>
    /// <returns>统一返回模型</returns>
    public static ApiResponse NotFound()
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.NotFound,
            Message = ApiResponseCodes.NotFound.GetDescription()
        };
    }

    /// <summary>
    /// 请求语义错误 422：参数格式正确但业务语义校验未通过
    /// </summary>
    /// <param name="errorMessage">错误明细（置于 Data 返回）</param>
    /// <returns>统一返回模型</returns>
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
    /// 请求过于频繁 429：超过限流阈值，请稍后再试
    /// </summary>
    /// <returns>统一返回模型</returns>
    public static ApiResponse TooManyRequests()
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.TooManyRequests,
            Message = ApiResponseCodes.TooManyRequests.GetDescription()
        };
    }

    /// <summary>
    /// 服务器内部错误 500：处理请求时发生未预期的异常
    /// </summary>
    /// <param name="errorMessage">错误明细（置于 Data 返回；对外响应建议留空以免泄露内部细节）</param>
    /// <param name="traceId">请求追踪 ID</param>
    /// <returns>统一返回模型</returns>
    public static ApiResponse InternalServerError(string? errorMessage = null, string? traceId = null)
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.InternalServerError,
            Message = ApiResponseCodes.InternalServerError.GetDescription(),
            Data = errorMessage,
            TraceId = traceId
        };
    }

    /// <summary>
    /// 服务不可用 503：系统维护、服务过载或依赖服务不可用，请稍后重试
    /// </summary>
    /// <returns>统一返回模型</returns>
    public static ApiResponse ServiceUnavailable()
    {
        return new ApiResponse
        {
            Code = ApiResponseCodes.ServiceUnavailable,
            Message = ApiResponseCodes.ServiceUnavailable.GetDescription()
        };
    }

    /// <summary>
    /// 通用失败：按指定业务码构造失败响应（适用于业务状态码 10000+ 及未内置工厂的协议码）
    /// </summary>
    /// <param name="code">业务码</param>
    /// <param name="errorMessage">错误明细（置于 Data 返回）</param>
    /// <param name="traceId">请求追踪 ID</param>
    /// <returns>统一返回模型</returns>
    public static ApiResponse Failure(ApiResponseCodes code, string? errorMessage = null, string? traceId = null)
    {
        return new ApiResponse
        {
            Code = code,
            Message = Enum.IsDefined(code) ? code.GetDescription() : "请求失败",
            Data = errorMessage,
            TraceId = traceId
        };
    }
}

/// <summary>
/// 统一返回模型（泛型）
/// </summary>
/// <typeparam name="T">返回数据类型</typeparam>
/// <remarks>
/// 与 <see cref="ApiResponse"/> 一致，但 <see cref="Data"/> 为强类型，
/// 便于客户端代码生成与 OpenAPI 文档表达精确的响应结构。
/// </remarks>
public class ApiResponse<T> : ApiResponse
{
    /// <summary>
    /// 返回数据（强类型）
    /// </summary>
    public new T? Data { get; set; }

    /// <summary>
    /// 请求成功 200：返回强类型业务数据
    /// </summary>
    /// <param name="data">业务数据</param>
    /// <param name="traceId">请求追踪 ID</param>
    /// <returns>统一返回模型（泛型）</returns>
    public static ApiResponse<T> Success(T? data, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Code = ApiResponseCodes.Success,
            Message = ApiResponseCodes.Success.GetDescription(),
            Data = data,
            TraceId = traceId
        };
    }

    /// <summary>
    /// 服务器内部错误 500：处理请求时发生未预期的异常
    /// </summary>
    /// <param name="data">错误明细（置于 Data 返回）</param>
    /// <param name="traceId">请求追踪 ID</param>
    /// <returns>统一返回模型（泛型）</returns>
    public static ApiResponse<T> InternalServerError(T? data = default, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Code = ApiResponseCodes.InternalServerError,
            Message = ApiResponseCodes.InternalServerError.GetDescription(),
            Data = data,
            TraceId = traceId
        };
    }
}
