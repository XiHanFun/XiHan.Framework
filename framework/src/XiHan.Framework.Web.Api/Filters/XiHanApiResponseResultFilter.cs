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
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Web.Api.Constants;

namespace XiHan.Framework.Web.Api.Filters;

/// <summary>
/// 统一返回结果过滤器
/// </summary>
public class XiHanApiResponseResultFilter : IAsyncResultFilter
{
    /// <inheritdoc />
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (ShouldIgnore(context))
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

    private static bool TryWrapObjectResult(ResultExecutingContext context)
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

    private static bool TryWrapJsonResult(ResultExecutingContext context)
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

    private static bool TryWrapContentResult(ResultExecutingContext context)
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

    private static bool TryWrapStatusCodeResult(ResultExecutingContext context)
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

    private static bool TryWrapEmptyResult(ResultExecutingContext context)
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

    private static ApiResponse BuildResponse(object? value, int statusCode, string traceId)
    {
        return statusCode >= StatusCodes.Status400BadRequest
            ? BuildErrorResponse(value, statusCode, traceId)
            : ApiResponse.Success(value, traceId);
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

    private static string ResolveTraceId(HttpContext httpContext)
    {
        return httpContext.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
            ?? httpContext.TraceIdentifier;
    }
}
