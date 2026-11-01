// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Abstractions.Authorization;

/// <summary>
/// 策略目录。
/// </summary>
/// <remarks>
/// 注册的 <see cref="IPolicy"/> 按名查找；<c>permission:&lt;code&gt;</c> 在权限已登记于 <see cref="PermissionRegistry"/> 时动态创建权限策略。授权能力启动时经 <see cref="EnforceOn"/> 核对操作契约按名引用的策略（ADR-0080）。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PolicyCatalog
{
    /// <summary>
    /// 显式注册的策略，按名索引（区分大小写）。
    /// </summary>
    private readonly Dictionary<string, IPolicy> _policies = new(StringComparer.Ordinal);

    /// <summary>
    /// 权限登记表。
    /// </summary>
    /// <remarks>
    /// 只为已登记的权限码动态创建权限策略。
    /// </remarks>
    private readonly PermissionRegistry _permissions;

    /// <summary>
    /// 动态创建的权限策略所用的检查器。
    /// </summary>
    private readonly IPermissionChecker _checker;

    /// <summary>
    /// 创建目录。
    /// </summary>
    /// <remarks>
    /// 策略名重复抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    public PolicyCatalog(IEnumerable<IPolicy> policies, PermissionRegistry permissions, IPermissionChecker checker)
    {
        ArgumentNullException.ThrowIfNull(policies);
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
        foreach (var policy in policies)
        {
            if (!_policies.TryAdd(policy.Name, policy))
            {
                throw new ArgumentException($"Policy name '{policy.Name}' is duplicated.", nameof(policies));
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

        // 注册表里没有再看是否为权限策略：只为已登记的权限码动态创建，未登记的权限码视为策略不存在
        var code = PermissionPolicy.CodeOf(name);
        return code is not null && _permissions.Find(code).HasValue
            ? Maybe.Some<IPolicy>(new PermissionPolicy(code, _checker))
            : default;
    }

    /// <summary>
    /// 核对操作契约按名引用的策略都在目录里，并让此后登记的操作逐个经过同一核对。
    /// </summary>
    /// <remarks>
    /// 由 <c>security.authorization@1</c> 的 Provider 在启动时调用一次（ADR-0080）：策略是容器里的 <see cref="IPolicy"/>，容器建好才知道全部策略名；配置阶段登记的操作（含配置阶段发现的插件的代理操作）此时都已在登记表里。契约的权限码已在装配完成时由内核核对，这里只看 <see cref="OperationContract.Policy"/>（<c>permission:&lt;code&gt;</c> 形式的策略名同样要求权限码已登记）。有不存在的策略时抛出携带 <see cref="SecurityErrors.UnregisteredPolicy"/> 的 <see cref="FrameworkException"/>，一次列出全部；此后登记的操作（插件握手之后）引用了不存在的策略，登记时返回同一错误码，整组不登记。
    /// </remarks>
    public void EnforceOn(OperationRegistry operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var failing = operations.Enforce(UnregisteredPolicyOf);
        if (failing.Count > 0)
        {
            var violations = failing.Select(static d => $"'{d.Id}' of '{d.OwnerId}' requires policy '{d.Contract.Policy}'");
            throw new FrameworkException(SecurityErrors.UnregisteredPolicy, $"Operations reference policies that do not exist: {string.Join("; ", violations)}. Register each policy as an 'IPolicy' service; a 'permission:<code>' policy needs the code registered with 'FeatureContext.AddPermission'.");
        }
    }

    /// <summary>
    /// 操作契约引用的策略不存在时的错误。
    /// </summary>
    /// <remarks>
    /// 没有策略要求或策略存在时为 <see langword="null"/>。
    /// </remarks>
    private Error? UnregisteredPolicyOf(OperationDescriptor descriptor)
        => descriptor.Contract.Policy is { } policy && !Find(policy).HasValue
            ? Error.Validation(SecurityErrors.UnregisteredPolicy, SecurityMessages.OperationPolicyUnregistered, MessageArguments.Of(("id", descriptor.Id), ("owner", descriptor.OwnerId), ("policy", policy)))
            : null;
}
