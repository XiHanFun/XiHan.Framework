// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Pipeline;

/// <summary>
/// Observe 阶段末尾的审计中间件：调用结束后，按显式动作或选择规则决定是否把这次调用写成一条 <see cref="AuditEvent"/>。接收器的故障只记日志，不影响调用结果。
/// 主体与租户优先取安全上下文（认证后的 <see cref="Principal"/>、解析后的 <see cref="TenantContext"/>），没有时回退到 Baggage 的 <see cref="BaggageKeys.Principal"/> / <see cref="BaggageKeys.Tenant"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class AuditMiddleware : IPipelineMiddleware
{
    /// <summary>
    /// 接收器。
    /// </summary>
    private readonly IAuditSink _sink;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly AuditingOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建。
    /// </summary>
    public AuditMiddleware(IAuditSink sink, AuditingOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<AuditMiddleware>();
    }

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);
        Error? thrown = null;
        try
        {
            await nextStep(context).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 异常继续向外传给 ObserveMiddleware 处理；这里只记住它，让审计事件能反映失败原因
            thrown = new Error(KernelErrors.Cancelled, KernelMessages.Cancelled);
            throw;
        }
        catch (Exception exception)
        {
            thrown = Error.Unexpected(KernelErrors.Unexpected, KernelMessages.UnhandledException, exception is FrameworkException framework ? framework.ToError() : null);
            throw;
        }
        finally
        {
            await RecordAsync(context, thrown).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 决定并写入审计事件。
    /// </summary>
    private async ValueTask RecordAsync(InvocationContext context, Error? thrown)
    {
        var action = ResolveAction(context);
        if (action is null)
        {
            return;
        }

        // 结果优先取管道里已写入的；没有写入而又抛出了异常，按异常记失败；正常返回却没写结果视为成功
        Result outcome;
        if (context.GetOutcome().TryGetValue(out var written))
        {
            outcome = written;
        }
        else if (thrown is not null)
        {
            outcome = thrown;
        }
        else
        {
            outcome = Result.Success();
        }

        if (outcome.IsSuccess && !_options.IncludeSuccesses)
        {
            return;
        }

        // 主体与租户先看安全上下文：Observe 在 Authenticate 之外、事件在调用结束时构造，认证后的主体与解析后的租户此时都已就绪，
        // 而 HTTP + JWT 这条路上没有中间件写 Baggage；匿名调用或只带 Baggage 的调用（插件协议、消息派发、命令行）再回退到 Baggage
        var security = context.GetSecurity();
        var auditEvent = new AuditEvent(_clock.GetUtcNow(), action, outcome, context.TraceId)
        {
            Resource = context.GetAuditResource().TryGetValue(out var resource) ? resource : null,
            PrincipalId = security.Principal.IsAuthenticated ? security.Principal.Id : context.TryGetBaggage(BaggageKeys.Principal, out var principal) ? principal : null,
            TenantId = security.Tenant?.Id ?? (context.TryGetBaggage(BaggageKeys.Tenant, out var tenant) ? tenant : null),
            Details = context.GetAuditDetails(),
        };

        // 不带调用的取消令牌：被取消的调用同样要记录；接收器的故障不能让业务调用失败
        try
        {
            await _sink.WriteAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogWriteFailed(_logger, exception, action, context.TraceId);
        }
    }

    /// <summary>
    /// 动作：显式声明优先；否则按选择规则看当前操作。
    /// </summary>
    private string? ResolveAction(InvocationContext context)
    {
        if (context.GetAuditAction().TryGetValue(out var explicitAction))
        {
            return explicitAction;
        }

        if (!context.GetOperation().TryGetValue(out var operation))
        {
            return null;
        }

        return _options.Selection switch
        {
            AuditSelection.All => operation.Id,
            AuditSelection.Protected when operation.Contract.Permission is not null => operation.Id,
            _ => null,
        };
    }

    /// <summary>
    /// 接收器写入失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "The audit sink failed to record '{Action}' for invocation '{TraceId}'.")]
    private static partial void LogWriteFailed(ILogger logger, Exception exception, string action, string traceId);
}
