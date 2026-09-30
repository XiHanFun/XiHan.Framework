// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using XiHan.Framework.Web.Api.Idempotency;

namespace XiHan.Framework.Web.Api.Filters;

/// <summary>
/// WebApi Action 幂等完成过滤器（内层）
/// </summary>
/// <remarks>
/// 注册在工作单元过滤器之内、最贴近动作：动作正常返回后把结果序列化为快照并写入完成，
/// 事务型工作单元内与业务同一事务提交；写入失败时异常向外传播，工作单元不提交。
/// </remarks>
public class XiHanIdempotencyCompletionFilter : IAsyncActionFilter
{
    private readonly IIdempotencyStore _store;
    private readonly XiHanIdempotencyOptions _options;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<XiHanIdempotencyCompletionFilter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanIdempotencyCompletionFilter(
        IIdempotencyStore store,
        IOptions<XiHanIdempotencyOptions> options,
        IOptions<JsonOptions> jsonOptions,
        ILogger<XiHanIdempotencyCompletionFilter> logger)
    {
        _store = store;
        _options = options.Value;
        _serializerOptions = jsonOptions.Value.JsonSerializerOptions;
        _logger = logger;
    }

    /// <summary>
    /// 动作返回后写入完成
    /// </summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Items[IdempotencyExecution.ItemKey] is not IdempotencyExecution execution)
        {
            await next();
            return;
        }

        var executedContext = await next();
        if (executedContext.Exception is not null)
        {
            return;
        }

        if (!TryCreateSnapshot(executedContext.Result, out var response))
        {
            _logger.LogWarning("幂等请求 {Method} {Endpoint} 的结果无法保存为快照", execution.Key.Method, execution.Key.Endpoint);
            return;
        }

        await _store.CompleteAsync(execution.Key, execution.OwnerToken, response, CancellationToken.None);
        execution.IsCompleted = true;
    }

    private bool TryCreateSnapshot(IActionResult? result, [NotNullWhen(true)] out StoredResponse? response)
    {
        switch (result)
        {
            case ObjectResult { Value: Stream }:
                break;
            case ObjectResult objectResult:
                var body = JsonSerializer.SerializeToUtf8Bytes(
                    objectResult.Value, objectResult.Value?.GetType() ?? typeof(object), _serializerOptions);
                if (body.Length <= _options.MaxResponseBytes)
                {
                    response = new StoredResponse(objectResult.StatusCode ?? 200, body);
                    return true;
                }

                break;
            case null:
            case EmptyResult:
                response = new StoredResponse(200, null);
                return true;
            case StatusCodeResult statusCodeResult:
                response = new StoredResponse(statusCodeResult.StatusCode, null);
                return true;
        }

        response = null;
        return false;
    }
}
