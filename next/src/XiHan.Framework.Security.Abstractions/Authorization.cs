// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 一次调用的授权要求：策略名与可选的资源。
/// </summary>
/// <param name="Policy">策略名；<c>permission:&lt;code&gt;</c> 映射为权限策略。</param>
/// <param name="Resource">资源名；无具体资源时为 <see langword="null"/>。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuthorizationRequirement(string Policy, ResourceName? Resource = null)
{
    /// <summary>
    /// 策略名。
    /// </summary>
    public string Policy { get; } = !string.IsNullOrWhiteSpace(Policy) ? Policy : throw new ArgumentException("策略名不能为空。", nameof(Policy));

    /// <summary>
    /// 只要求已认证。
    /// </summary>
    public static AuthorizationRequirement Authenticated { get; } = new(AuthenticatedPolicy.PolicyName);

    /// <summary>
    /// 要求权限。
    /// </summary>
    public static AuthorizationRequirement Permission(string code, ResourceName? resource = null) => new(PermissionPolicy.NameFor(code), resource);
}

/// <summary>
/// 策略：主体已认证。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class AuthenticatedPolicy : IPolicy
{
    /// <summary>
    /// 策略名 <c>authenticated</c>。
    /// </summary>
    public const string PolicyName = "authenticated";

    /// <inheritdoc />
    public string Name => PolicyName;

    /// <inheritdoc />
    public ValueTask<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(request.Security.Principal.IsAuthenticated
            ? AuthorizationResult.Allow()
            : AuthorizationResult.Deny(Error.Denied(SecurityErrors.NotAuthenticated, "本调用需要认证。")));
    }
}

/// <summary>
/// 策略：主体持有某个权限（经 <see cref="IPermissionChecker"/>）。匿名主体先按未认证拒绝。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PermissionPolicy : IPolicy
{
    /// <summary>
    /// 策略名前缀 <c>permission:</c>。
    /// </summary>
    public const string Prefix = "permission:";

    private readonly IPermissionChecker _checker;

    /// <summary>
    /// 创建权限策略。
    /// </summary>
    public PermissionPolicy(string permissionCode, IPermissionChecker checker)
    {
        PermissionCode = !string.IsNullOrWhiteSpace(permissionCode) ? permissionCode : throw new ArgumentException("权限码不能为空。", nameof(permissionCode));
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
    }

    /// <summary>
    /// 权限码。
    /// </summary>
    public string PermissionCode { get; }

    /// <inheritdoc />
    public string Name => NameFor(PermissionCode);

    /// <summary>
    /// 权限码对应的策略名。
    /// </summary>
    public static string NameFor(string permissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        return Prefix + permissionCode;
    }

    /// <summary>
    /// 从策略名取权限码；不是权限策略返回 <see langword="null"/>。
    /// </summary>
    public static string? CodeOf(string policyName)
        => policyName is not null && policyName.StartsWith(Prefix, StringComparison.Ordinal) && policyName.Length > Prefix.Length
            ? policyName[Prefix.Length..]
            : null;

    /// <inheritdoc />
    public async ValueTask<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Security.Principal.IsAuthenticated)
        {
            return AuthorizationResult.Deny(Error.Denied(SecurityErrors.NotAuthenticated, "本调用需要认证。"));
        }

        return await _checker.HasPermissionAsync(request.Security, PermissionCode, cancellationToken).ConfigureAwait(false)
            ? AuthorizationResult.Allow()
            : AuthorizationResult.Deny(Error.Denied(SecurityErrors.PermissionMissing, $"缺少权限 '{PermissionCode}'。"));
    }
}

/// <summary>
/// 策略目录：注册的 <see cref="IPolicy"/> 按名查找；<c>permission:&lt;code&gt;</c> 在权限已登记于 <see cref="PermissionRegistry"/> 时动态创建权限策略。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PolicyCatalog
{
    private readonly Dictionary<string, IPolicy> _policies = new(StringComparer.Ordinal);
    private readonly PermissionRegistry _permissions;
    private readonly IPermissionChecker _checker;

    /// <summary>
    /// 创建目录；策略名重复抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public PolicyCatalog(IEnumerable<IPolicy> policies, PermissionRegistry permissions, IPermissionChecker checker)
    {
        ArgumentNullException.ThrowIfNull(policies);
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
        foreach (var policy in policies)
        {
            if (!_policies.TryAdd(policy.Name, policy))
            {
                throw new ArgumentException($"策略名 '{policy.Name}' 重复。", nameof(policies));
            }
        }
    }

    /// <summary>
    /// 已注册的策略名。
    /// </summary>
    public IReadOnlyCollection<string> Names => _policies.Keys;

    /// <summary>
    /// 按名查找。
    /// </summary>
    public Maybe<IPolicy> Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_policies.TryGetValue(name, out var policy))
        {
            return Maybe.Some(policy);
        }

        var code = PermissionPolicy.CodeOf(name);
        return code is not null && _permissions.Find(code).HasValue
            ? Maybe.Some<IPolicy>(new PermissionPolicy(code, _checker))
            : default;
    }
}

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

        if (context.GetAuthorizationRequirement().TryGetValue(out var requirement))
        {
            var catalog = context.Services.GetRequiredService<PolicyCatalog>();
            if (!catalog.Find(requirement.Policy).TryGetValue(out var policy))
            {
                context.SetOutcome(Error.NotFound(SecurityErrors.PolicyNotFound, $"策略 '{requirement.Policy}' 不存在。"));
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
}
