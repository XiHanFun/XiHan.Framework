// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

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
