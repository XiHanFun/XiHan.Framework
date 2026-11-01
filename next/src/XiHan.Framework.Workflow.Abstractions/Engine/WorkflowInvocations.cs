// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 工作流自己开的调用。
/// </summary>
/// <remarks>
/// 没有可加入的调用方事务时（轮询、后台恢复、调用之外的直接使用），引擎在新的服务作用域里经完整管道开一次调用：租户写进 Baggage 并预先放进安全上下文——装配了多租户时 Tenant 阶段照常解析、校验并绑定分片，没有装配时预先放进的租户原样保留；Transaction 阶段开启的工作单元就是这次操作的事务。处理器返回失败时调用以失败结束、事务回滚；应用不在服务状态时返回 <see cref="WorkflowErrors.StoreUnavailable"/>。
/// </remarks>
internal static class WorkflowInvocations
{
    /// <summary>
    /// 调用上下文里的标记：这次调用是工作流自己开的。
    /// </summary>
    /// <remarks>
    /// 以上下文条目而不是作用域服务标记：调用之前在新作用域里解析任何服务都可能提前建出持久化客户端，那时还没有调用、也就没有租户的分片。
    /// </remarks>
    public static readonly ContextItemKey<bool> OwnedKey = new("xihan.workflow.owned");

    /// <summary>
    /// 在新作用域的一次调用里以 <paramref name="tenantId"/> 执行 <paramref name="handler"/>，返回调用的结果。
    /// </summary>
    /// <param name="scopes">根作用域工厂。</param>
    /// <param name="app">应用。</param>
    /// <param name="tenantId">调用的租户，空为平台。</param>
    /// <param name="target">记进调用来源的目标（操作名），供审计与追踪辨认。</param>
    /// <param name="handler">在调用里执行的处理器；返回失败即让事务回滚。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static async ValueTask<Result> InvokeAsync(IServiceScopeFactory scopes, App app, string? tenantId, string target, Func<IServiceProvider, ValueTask<Result>> handler, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(handler);
        Result outcome;
        try
        {
            // 新作用域：同一作用域里再经管道会撞上已经开始过的工作单元（RFC-0005「嵌套调用」）
            await using var scope = scopes.CreateAsyncScope();
            var baggage = tenantId is null ? null : new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Tenant] = tenantId };
            var context = App.CreateContext(scope.ServiceProvider, cancellationToken, baggage: baggage);
            if (tenantId is not null)
            {
                // 预先放进安全上下文：没有装配多租户（没有 Tenant 阶段）时租户也在，工作单元按它开启；装配了时 Tenant 阶段按 Baggage 解析出同一个租户并校验
                context.SetTenant(new TenantContext(tenantId));
            }

            context.SetOrigin(new InvocationOrigin { Protocol = InvocationProtocols.Workflow, Target = target });
            context.Set(OwnedKey, true);
            outcome = await app.InvokeAsync(context, async ctx =>
            {
                var handled = await handler(ctx.Services).ConfigureAwait(false);
                // 处理器失败写成调用结果：Transaction 阶段据此回滚，这次操作的写入一条也不落库
                if (handled.IsFailure)
                {
                    ctx.SetOutcome(handled);
                }
            }).ConfigureAwait(false);
        }
        catch (FrameworkException exception) when (string.Equals(exception.Code, KernelErrors.InvalidAppState, StringComparison.Ordinal))
        {
            // 应用不在服务状态（启动前、排空中）：管道不接受调用，按存储不可达报告
            return Unavailable(exception.Message);
        }

        // 取消不以失败结果掩盖：调用方按异步约定收到 OperationCanceledException
        cancellationToken.ThrowIfCancellationRequested();
        return outcome;
    }

    /// <summary>
    /// 存储不可达。
    /// </summary>
    public static Error Unavailable(string reason)
        => Error.Unavailable(WorkflowErrors.StoreUnavailable, WorkflowMessages.StoreUnavailable, MessageArguments.Of(("reason", reason)));
}
