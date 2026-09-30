// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Uow;
using XiHan.Framework.Web.Api.DynamicApi.Helpers;
using XiHan.Framework.Web.Api.Idempotency;

namespace XiHan.Framework.Web.Api.Filters;

/// <summary>
/// WebApi Action 幂等过滤器（外层）
/// </summary>
/// <remarks>
/// 注册在工作单元过滤器之外：校验幂等键、计算请求摘要、以独立连接取得幂等键，
/// 已完成则把快照还原为 <see cref="ObjectResult"/> 短路动作；动作失败或未写入完成时释放或标记不确定。
/// 完成写入由内层完成过滤器 <see cref="XiHanIdempotencyCompletionFilter"/> 在工作单元提交前完成。
/// </remarks>
public class XiHanIdempotencyFilter : IAsyncActionFilter
{
    /// <summary>
    /// 重播响应的标记头
    /// </summary>
    public const string ReplayedHeaderName = "Idempotency-Replayed";

    private readonly IIdempotencyStore _store;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly XiHanIdempotencyOptions _options;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<XiHanIdempotencyFilter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanIdempotencyFilter(
        IIdempotencyStore store,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IOptions<XiHanIdempotencyOptions> options,
        IOptions<JsonOptions> jsonOptions,
        ILogger<XiHanIdempotencyFilter> logger)
    {
        _store = store;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _options = options.Value;
        _serializerOptions = jsonOptions.Value.JsonSerializerOptions;
        _logger = logger;
    }

    /// <summary>
    /// 解析标注了幂等的动作方法，未标注或非控制器动作返回 null
    /// </summary>
    public static MethodInfo? ResolveIdempotentMethodOrNull(ActionDescriptor actionDescriptor)
    {
        if (actionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
        {
            return null;
        }

        var method = OriginalMethodResolver.Resolve(controllerActionDescriptor.MethodInfo);
        var isIdempotent = method.IsDefined(typeof(IdempotentAttribute), true) ||
                           method.DeclaringType?.IsDefined(typeof(IdempotentAttribute), true) == true;
        return isIdempotent ? method : null;
    }

    /// <summary>
    /// 把响应快照还原为动作结果
    /// </summary>
    public static IActionResult CreateReplayResult(StoredResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Body is not null)
        {
            return new ObjectResult(JsonSerializer.Deserialize<JsonElement>(response.Body))
            {
                StatusCode = response.StatusCode
            };
        }

        return response.StatusCode == StatusCodes.Status200OK
            ? new EmptyResult()
            : new StatusCodeResult(response.StatusCode);
    }

    /// <summary>
    /// Action 执行前后的幂等处理
    /// </summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = ResolveIdempotentMethodOrNull(context.ActionDescriptor);
        if (method is null)
        {
            await next();
            return;
        }

        var httpContext = context.HttpContext;
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            Reject(context, StatusCodes.Status401Unauthorized, "幂等接口要求已认证的调用方");
            return;
        }

        var key = httpContext.Request.Headers[_options.HeaderName].ToString();
        if (!IdempotencyKeyValidator.IsValid(key, _options.MaxKeyLength))
        {
            Reject(context, StatusCodes.Status400BadRequest, $"请求头 {_options.HeaderName} 缺失或无效");
            return;
        }

        bool hasUploadedFiles;
        try
        {
            hasUploadedFiles = await HasUploadedFilesAsync(httpContext.Request, httpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or BadHttpRequestException)
        {
            Reject(context, StatusCodes.Status400BadRequest, "请求表单无法读取");
            return;
        }

        if (RequestFingerprint.ContainsUnsupportedArgument(context.ActionArguments) ||
            HasUnsupportedParameter(context.ActionDescriptor) ||
            hasUploadedFiles)
        {
            Reject(context, StatusCodes.Status415UnsupportedMediaType, "幂等接口不支持文件或流参数");
            return;
        }

        var request = httpContext.Request;
        var path = (request.Path.Value ?? string.Empty).ToLowerInvariant();
        if (!RequestFingerprint.TryCompute(request.Method, path, request.QueryString.Value,
                context.ActionArguments, _serializerOptions, _options.MaxRequestBytes, out var fingerprint))
        {
            Reject(context, StatusCodes.Status413PayloadTooLarge, "请求内容超过幂等摘要上限");
            return;
        }

        var recordKey = new IdempotencyRecordKey(
            _currentTenant.Id?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            _currentUser.UserId.Value.ToString(CultureInfo.InvariantCulture),
            request.Method,
            path,
            key);
        var isTransactional = IsTransactional(httpContext.RequestServices, method);

        var acquired = await _store.TryAcquireAsync(recordKey, fingerprint, isTransactional, httpContext.RequestAborted);
        switch (acquired.Status)
        {
            case IdempotencyAcquireStatus.Replay:
                httpContext.Response.Headers[ReplayedHeaderName] = "true";
                context.Result = CreateReplayResult(acquired.Response!);
                return;
            case IdempotencyAcquireStatus.InProgress:
                Reject(context, StatusCodes.Status409Conflict, "相同幂等键的请求正在处理");
                return;
            case IdempotencyAcquireStatus.Conflict:
                Reject(context, StatusCodes.Status409Conflict, "幂等键已用于内容不同的请求");
                return;
            case IdempotencyAcquireStatus.Indeterminate:
                Reject(context, StatusCodes.Status409Conflict, "该幂等键对应的请求结果不确定，不会自动重试");
                return;
            case IdempotencyAcquireStatus.CapacityExceeded:
                Reject(context, StatusCodes.Status503ServiceUnavailable, "幂等存储容量已满");
                return;
        }

        var execution = new IdempotencyExecution(recordKey, acquired.OwnerToken, isTransactional);
        httpContext.Items[IdempotencyExecution.ItemKey] = execution;

        ActionExecutedContext executedContext;
        try
        {
            executedContext = await next();
        }
        catch
        {
            await AbandonAsync(execution, actionFailed: true);
            throw;
        }

        if (executedContext.Exception is null && execution.IsCompleted)
        {
            return;
        }

        await AbandonAsync(execution, executedContext.Exception is not null);
    }

    /// <summary>
    /// 动作失败或未写入完成时收尾：事务型失败释放，其余标记不确定；存储调用失败时记录错误日志后忽略
    /// </summary>
    private async Task AbandonAsync(IdempotencyExecution execution, bool actionFailed)
    {
        try
        {
            if (actionFailed && execution.IsTransactional)
            {
                await _store.ReleaseAsync(execution.Key, execution.OwnerToken, CancellationToken.None);
                return;
            }

            if (execution.IsCompleted)
            {
                return;
            }

            _logger.LogWarning("幂等请求 {Method} {Endpoint} 未写入完成，已标记为结果不确定", execution.Key.Method, execution.Key.Endpoint);
            await _store.MarkIndeterminateAsync(execution.Key, execution.OwnerToken, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "幂等请求 {Method} {Endpoint} 收尾时存储调用失败", execution.Key.Method, execution.Key.Endpoint);
        }
    }

    private static bool HasUnsupportedParameter(ActionDescriptor actionDescriptor)
    {
        return actionDescriptor.Parameters.Any(parameter =>
            parameter.BindingInfo?.BindingSource == BindingSource.FormFile ||
            RequestFingerprint.IsUnsupportedParameterType(parameter.ParameterType));
    }

    private static async Task<bool> HasUploadedFilesAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        return request.HasFormContentType && (await request.ReadFormAsync(cancellationToken)).Files.Count > 0;
    }

    private static bool IsTransactional(IServiceProvider serviceProvider, MethodInfo method)
    {
        return UnitOfWorkHelper.IsUnitOfWorkMethod(method, out var attribute) &&
               UnitOfWorkHelper.CreateOptions(serviceProvider, method, attribute).IsTransactional;
    }

    private static void Reject(ActionExecutingContext context, int statusCode, string message)
    {
        context.Result = new ObjectResult(ApiResponse.Failure((ApiResponseCodes)statusCode, message, context.HttpContext.TraceIdentifier))
        {
            StatusCode = statusCode
        };
    }
}
