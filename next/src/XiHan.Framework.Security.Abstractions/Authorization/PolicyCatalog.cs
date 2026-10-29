// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Abstractions.Authorization;

/// <summary>
/// 策略目录。
/// </summary>
/// <remarks>
/// 注册的 <see cref="IPolicy"/> 按名查找；<c>permission:&lt;code&gt;</c> 在权限已登记于 <see cref="PermissionRegistry"/> 时动态创建权限策略。
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
}
