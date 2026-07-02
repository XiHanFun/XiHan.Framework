#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanApiResponseResultFilter
// Guid:9d8a8b7b-2b0e-4da4-8f6f-6b9d7a1f2f3c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 21:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Logging;

namespace XiHan.Framework.Web.Api.Filters;

/// <summary>
/// 统一返回结果过滤器：包装正常返回，并把未处理异常统一转为接口响应（与正常响应同序列化与业务码语义）
/// </summary>
public class XiHanApiResponseResultFilter : IAsyncResultFilter, IAsyncExceptionFilter
{
    /// <summary>
    /// 本地化资源名：响应默认消息（对应 JSON 资源 ApiResponse）
    /// </summary>
    private const string ApiResponseResourceName = "ApiResponse";

    private readonly IStringLocalizerFactory? _stringLocalizerFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="stringLocalizerFactory">字符串本地化工厂（缺失时回退到非本地化行为）</param>
    public XiHanApiResponseResultFilter(IStringLocalizerFactory? stringLocalizerFactory = null)
    {
        _stringLocalizerFactory = stringLocalizerFactory;
    }

    /// <summary>
    /// 未处理异常 → 统一接口响应：按异常类型映射 4xx/5xx 业务码，错误信息放 Message 供前端直接展示；
    /// 经 MVC 输出格式化器序列化（camelCase + 中文不转义 + 业务码 int），与正常响应完全一致。
    /// 异常日志（异常日志表）经 <see cref="ExceptionLogReporter"/> 落库；ILogger 告警由 ActionLoggingFilter 负责。
    /// </summary>
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.ExceptionHandled)
        {
            return;
        }

        var traceId = ResolveTraceId(context.HttpContext);
        var (statusCode, response) = MapExceptionLocalized(context.Exception);
        response.TraceId = traceId;

        // 异常日志表落库（队列异步），统一由 reporter 承担，避免与中间件重复
        await ExceptionLogReporter.ReportAsync(context.HttpContext, context.Exception, statusCode, traceId, context.HttpContext.RequestAborted);

        context.Result = new ObjectResult(response)
        {
            StatusCode = statusCode
        };
        context.ExceptionHandled = true;
    }

    /// <inheritdoc />
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (ShouldIgnore(context))
        {
            await next();
            return;
        }

        // 裸 Stream 返回值（如文件下载接口）转为文件流响应；否则会被包进 ApiResponse 再 JSON 序列化，
        // 而 Stream.Handle 为 IntPtr 不可序列化，导致 500（System.IntPtr instances ... is not supported）。
        if (TryConvertStreamToFile(context))
        {
            await next();
            return;
        }

        if (!TryWrapObjectResult(context) &&
            !TryWrapJsonResult(context) &&
            !TryWrapContentResult(context) &&
            !TryWrapStatusCodeResult(context) &&
            !TryWrapEmptyResult(context))
        {
            await next();
            return;
        }

        await next();
    }

    private static bool ShouldIgnore(ResultExecutingContext context)
    {
        if (context.Filters.OfType<IgnoreApiResponseAttribute>().Any())
        {
            return true;
        }

        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IgnoreApiResponseAttribute>() is not null)
        {
            return true;
        }

        if (context.Result is FileResult)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 将裸 Stream 返回值转为文件流响应，避免被统一包装为 ApiResponse 并 JSON 序列化
    /// </summary>
    private static bool TryConvertStreamToFile(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: Stream stream })
        {
            return false;
        }

        context.Result = new FileStreamResult(stream, "application/octet-stream");
        return true;
    }

    private bool TryWrapObjectResult(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult objectResult)
        {
            return false;
        }

        if (objectResult.Value is ApiResponse)
        {
            return false;
        }

        var statusCode = ResolveStatusCode(context.HttpContext, objectResult.StatusCode, objectResult.Value);
        var traceId = ResolveTraceId(context.HttpContext);
        var response = BuildResponse(objectResult.Value, statusCode, traceId);
        var normalizedStatusCode = NormalizeStatusCode(statusCode);
        context.Result = new ObjectResult(response)
        {
            StatusCode = normalizedStatusCode
        };
        return true;
    }

    private bool TryWrapJsonResult(ResultExecutingContext context)
    {
        if (context.Result is not JsonResult jsonResult)
        {
            return false;
        }

        if (jsonResult.Value is ApiResponse)
        {
            return false;
        }

        var statusCode = ResolveStatusCode(context.HttpContext, jsonResult.StatusCode, jsonResult.Value);
        var traceId = ResolveTraceId(context.HttpContext);
        var response = BuildResponse(jsonResult.Value, statusCode, traceId);
        var normalizedStatusCode = NormalizeStatusCode(statusCode);
        context.Result = new ObjectResult(response)
        {
            StatusCode = normalizedStatusCode
        };
        return true;
    }

    private bool TryWrapContentResult(ResultExecutingContext context)
    {
        if (context.Result is not ContentResult contentResult)
        {
            return false;
        }

        var statusCode = ResolveStatusCode(context.HttpContext, contentResult.StatusCode, contentResult.Content);
        var traceId = ResolveTraceId(context.HttpContext);
        var response = BuildResponse(contentResult.Content, statusCode, traceId);
        var normalizedStatusCode = NormalizeStatusCode(statusCode);
        context.Result = new ObjectResult(response)
        {
            StatusCode = normalizedStatusCode
        };
        return true;
    }

    private bool TryWrapStatusCodeResult(ResultExecutingContext context)
    {
        if (context.Result is not StatusCodeResult statusResult)
        {
            return false;
        }

        var statusCode = statusResult.StatusCode;
        var traceId = ResolveTraceId(context.HttpContext);
        var response = BuildResponse(null, statusCode, traceId);
        var normalizedStatusCode = NormalizeStatusCode(statusCode);
        context.Result = new ObjectResult(response)
        {
            StatusCode = normalizedStatusCode
        };
        return true;
    }

    private bool TryWrapEmptyResult(ResultExecutingContext context)
    {
        if (context.Result is not EmptyResult)
        {
            return false;
        }

        var statusCode = context.HttpContext.Response.StatusCode;
        var traceId = ResolveTraceId(context.HttpContext);
        var response = BuildResponse(null, statusCode, traceId);
        var normalizedStatusCode = NormalizeStatusCode(statusCode);
        context.Result = new ObjectResult(response)
        {
            StatusCode = normalizedStatusCode
        };
        return true;
    }

    private static int ResolveStatusCode(HttpContext httpContext, int? statusCode, object? value)
    {
        if (statusCode.HasValue)
        {
            return statusCode.Value;
        }

        if (value is ProblemDetails problemDetails && problemDetails.Status.HasValue)
        {
            return problemDetails.Status.Value;
        }

        if (value is ValidationProblemDetails validationProblemDetails && validationProblemDetails.Status.HasValue)
        {
            return validationProblemDetails.Status.Value;
        }

        return httpContext.Response.StatusCode;
    }

    private static int NormalizeStatusCode(int statusCode)
    {
        return statusCode == StatusCodes.Status204NoContent
            ? StatusCodes.Status200OK
            : statusCode;
    }

    private ApiResponse BuildResponse(object? value, int statusCode, string traceId)
    {
        var response = statusCode >= StatusCodes.Status400BadRequest
            ? BuildErrorResponse(value, statusCode, traceId)
            : ApiResponse.Success(value, traceId);

        // 按请求文化本地化通用码描述（资源 ApiResponse / 键=语义码名，如 Success/BadRequest/Failed）；
        // 缺工厂或缺键时回退原 Message（GetDescription 默认中文）。异常路径另由 MapExceptionLocalized 处理。
        response.Message = LocalizeApiResponseMessage(response.Code.ToString(), response.Message);
        return response;
    }

    private static ApiResponse BuildErrorResponse(object? value, int statusCode, string traceId)
    {
        var code = (ApiResponseCodes)statusCode;
        var (message, data) = ResolveErrorMessageAndData(value, code, statusCode);
        return new ApiResponse
        {
            Code = code,
            Message = message,
            Data = data,
            TraceId = traceId
        };
    }

    private static (string Message, object? Data) ResolveErrorMessageAndData(
        object? value,
        ApiResponseCodes code,
        int statusCode)
    {
        if (value is string text && !string.IsNullOrWhiteSpace(text))
        {
            return (text, null);
        }

        if (value is ProblemDetails problemDetails)
        {
            var message = !string.IsNullOrWhiteSpace(problemDetails.Detail)
                ? problemDetails.Detail
                : problemDetails.Title ?? ResolveDefaultMessage(code, statusCode);
            return (message, problemDetails);
        }

        if (value is ValidationProblemDetails validationProblemDetails)
        {
            var message = !string.IsNullOrWhiteSpace(validationProblemDetails.Detail)
                ? validationProblemDetails.Detail
                : validationProblemDetails.Title ?? ResolveDefaultMessage(code, statusCode);
            return (message, validationProblemDetails);
        }

        return (ResolveDefaultMessage(code, statusCode), value);
    }

    private static string ResolveDefaultMessage(ApiResponseCodes code, int statusCode)
    {
        if (Enum.IsDefined(typeof(ApiResponseCodes), code))
        {
            return code.GetDescription();
        }

        return statusCode >= StatusCodes.Status500InternalServerError
            ? "服务端处理异常"
            : "请求失败";
    }

    /// <summary>
    /// 异常类型 → (HTTP 状态码, 统一响应)。业务/输入类错误归 4xx（客户端可纠正），仅真正未预期归 500（不泄露内部细节）。
    /// 统一经 <see cref="ApiResponse"/> 工厂构造（具体错误置于 Data，Message 为通用码描述），TraceId 由调用方补齐。
    /// </summary>
    /// <remarks>公开静态供异常中间件复用，保证异常→状态码/响应映射单一来源。</remarks>
    public static (int StatusCode, ApiResponse Response) MapException(Exception exception)
    {
        return exception switch
        {
            UserFriendlyException ex => (StatusCodes.Status400BadRequest, ApiResponse.BadRequest(ex.Message)),
            BusinessException ex => (StatusCodes.Status400BadRequest, ApiResponse.BadRequest(ex.Message)),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, ApiResponse.Unauthorized("未授权访问")),
            KeyNotFoundException => (StatusCodes.Status404NotFound, ApiResponse.NotFound()),
            ArgumentException ex => (StatusCodes.Status400BadRequest, ApiResponse.BadRequest(ex.Message)),
            // 业务规则冲突（输入合法但当前状态不允许，如密码不合规、验证码失效）→ 422，消息随 Data 回前端
            InvalidOperationException ex => (StatusCodes.Status422UnprocessableEntity, ApiResponse.UnprocessableEntity(ex.Message)),
            // 仅真正未预期的异常才 500，Data 留空、不泄露内部细节
            _ => (StatusCodes.Status500InternalServerError, ApiResponse.InternalServerError())
        };
    }

    private static string ResolveTraceId(HttpContext httpContext)
    {
        return httpContext.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
            ?? httpContext.TraceIdentifier;
    }

    /// <summary>
    /// 异常 → (状态码, 响应) 的本地化变体：
    /// 在 <see cref="MapException(Exception)"/> 基础上，若异常携带可本地化消息（<c>LocalizableMessage</c>），
    /// 则按当前请求文化解析其值覆盖 Message；否则保持原行为（回退到异常 <c>.Message</c>）。
    /// </summary>
    private (int StatusCode, ApiResponse Response) MapExceptionLocalized(Exception exception)
    {
        var (statusCode, response) = MapException(exception);

        // 业务/用户友好异常携带可本地化消息时，按请求文化解析并覆盖面向用户的消息（缺失键回退异常 .Message）。
        // 注意：4xx 工厂（BadRequest/UnprocessableEntity 等）把用户消息放在 Data、Message 仅为通用码描述，
        // 且前端 extractBackendMessage 优先读取 Data，故本地化值须覆盖 Data（字符串）；
        // 若该响应未把消息放 Data（Data 非字符串），则退而覆盖 Message。
        if (_stringLocalizerFactory is not null
            && exception is BusinessException { LocalizableMessage: ILocalizableString localizableMessage })
        {
            var localized = localizableMessage.LocalizeOrFallback(_stringLocalizerFactory, exception.Message);
            if (!string.IsNullOrWhiteSpace(localized))
            {
                if (response.Data is string)
                {
                    response.Data = localized;
                }
                else
                {
                    response.Message = localized;
                }
            }

            return (statusCode, response);
        }

        // 未携带本地化消息时，对未预期异常（500）的通用提示按请求文化本地化（资源 ApiResponse / 键 ServerError）
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            response.Message = LocalizeApiResponseMessage("ServerError", response.Message);
        }

        return (statusCode, response);
    }

    /// <summary>
    /// 按请求文化解析响应默认消息（资源名 ApiResponse，键用语义码名，如 ServerError/BadRequest）；
    /// 工厂缺失或键缺失时回退到 <paramref name="fallback"/>。
    /// </summary>
    private string LocalizeApiResponseMessage(string key, string fallback)
    {
        if (_stringLocalizerFactory is null || string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        // 本地化查找绝不能让响应构建崩溃：此方法在 BuildResponse 中对每个响应调用，
        // 任何本地化异常（工厂/资源存储/虚拟文件系统等）都回退到原文，保证响应管线稳定。
        try
        {
            var localizer = _stringLocalizerFactory.Create(ApiResponseResourceName, ApiResponseResourceName);
            var localized = localizer[key];
            return localized.ResourceNotFound || string.IsNullOrWhiteSpace(localized.Value)
                ? fallback
                : localized.Value;
        }
        catch
        {
            return fallback;
        }
    }
}
