// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Authorization;

/// <summary>
/// Authorize 阶段的中间件
/// </summary>
/// <remarks>
/// 判定的要求是写入上下文的全部 <see cref="AuthorizationRequirement"/>（宿主端点元数据等）加上操作契约推导出的那一条，按顺序逐条判定，全部满足才放行，第一条不满足即结束；都没有则放行（ADR-0174）。策略不存在以 <see cref="SecurityErrors.PolicyNotFound"/> 拒绝（宁可拒绝，不可放行）；策略判定失败（如 <see cref="SecurityErrors.StoreUnavailable"/>）以该失败结束调用，不当作拒绝
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class AuthorizationMiddleware : IPipelineMiddleware
{
    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        // 写入的要求与契约的要求叠加，不是二选一：端点或组上加一条「要登录」不能让操作自己的权限失效（ADR-0174）；相同的要求只判定一次
        var requirements = new List<AuthorizationRequirement>(context.GetAuthorizationRequirements());
        if (context.FindOperation().TryGetValue(out var operation) && RequirementOf(operation.Contract).TryGetValue(out var contractRequirement))
        {
            requirements.Add(contractRequirement);
        }

        foreach (var requirement in requirements.Distinct())
        {
            var catalog = context.Services.GetRequiredService<PolicyCatalog>();
            // 策略不存在按拒绝处理而不是放行：配置错误不能变成越权
            if (!catalog.Find(requirement.Policy).TryGetValue(out var policy))
            {
                context.SetOutcome(Error.NotFound(SecurityErrors.PolicyNotFound, SecurityMessages.PolicyNotFound, MessageArguments.Of(("policy", requirement.Policy))));
                return;
            }

            var evaluated = await policy.EvaluateAsync(new AuthorizationRequest(context.GetSecurity(), requirement.Policy)
            {
                Resource = requirement.Resource,
            }, context.CancellationToken).ConfigureAwait(false);
            // 判定失败（授予存储不可达等）以该失败结束调用：既不放行，也不改写成拒绝（403），HTTP 按错误种类回 503
            if (!evaluated.TryGetValue(out var decision))
            {
                context.SetOutcome(evaluated.ToResult());
                return;
            }

            if (!decision.IsAllowed)
            {
                context.SetOutcome(decision.ToResult());
                return;
            }
        }

        await nextStep(context).ConfigureAwait(false);
    }

    /// <summary>
    /// 由操作契约推导授权要求
    /// </summary>
    /// <remarks>
    /// 权限码优先于策略名；都没有则无要求
    /// </remarks>
    private static Maybe<AuthorizationRequirement> RequirementOf(OperationContract contract)
    {
        if (contract.Permission is not null)
        {
            return Maybe.Some(AuthorizationRequirement.Permission(contract.Permission));
        }

        return contract.Policy is not null ? Maybe.Some(new AuthorizationRequirement(contract.Policy)) : default;
    }
}
