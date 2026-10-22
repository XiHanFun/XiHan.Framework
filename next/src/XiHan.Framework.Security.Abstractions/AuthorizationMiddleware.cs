// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// Authorize 阶段的中间件：调用带 <see cref="AuthorizationRequirement"/> 时按名取策略判定；没有要求则放行。
/// 策略不存在以 <see cref="SecurityErrors.PolicyNotFound"/> 拒绝（宁可拒绝，不可放行）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class AuthorizationMiddleware : IPipelineMiddleware
{
    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        // 显式要求（宿主端点元数据）优先；没有时按当前调用的操作契约推导；都没有的调用直接放行
        var required = context.GetAuthorizationRequirement();
        if (!required.HasValue && context.GetOperation().TryGetValue(out var operation))
        {
            required = RequirementOf(operation.Contract);
        }

        if (required.TryGetValue(out var requirement))
        {
            var catalog = context.Services.GetRequiredService<PolicyCatalog>();
            // 策略不存在按拒绝处理而不是放行：配置错误不能变成越权
            if (!catalog.Find(requirement.Policy).TryGetValue(out var policy))
            {
                context.SetOutcome(Error.NotFound(SecurityErrors.PolicyNotFound, SecurityMessages.PolicyNotFound, MessageArguments.Of(("policy", requirement.Policy))));
                return;
            }

            var decision = await policy.EvaluateAsync(new AuthorizationRequest(context.GetSecurity(), requirement.Policy, requirement.Resource), context.CancellationToken).ConfigureAwait(false);
            if (!decision.IsAllowed)
            {
                context.SetOutcome(decision.ToResult());
                return;
            }
        }

        await nextStep(context).ConfigureAwait(false);
    }

    /// <summary>
    /// 由操作契约推导授权要求：权限码优先于策略名；都没有则无要求。
    /// </summary>
    private static Maybe<AuthorizationRequirement> RequirementOf(OperationContract contract)
    {
        if (contract.Permission is not null)
        {
            return Maybe.Some(AuthorizationRequirement.Permission(contract.Permission));
        }

        return contract.Policy is not null ? Maybe.Some(new AuthorizationRequirement(contract.Policy)) : default;
    }
}
