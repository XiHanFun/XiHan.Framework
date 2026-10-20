// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 权限定义：权限码与说明。权限码格式 <c>&lt;领域&gt;.&lt;资源&gt;.&lt;动作&gt;</c>，小写点分。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial record PermissionDefinition
{
    /// <summary>
    /// 创建权限定义。
    /// </summary>
    public PermissionDefinition(string code, string displayName, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (!CodePattern().IsMatch(code))
        {
            throw new ArgumentException($"权限码 '{code}' 必须是至少两段的小写点分标识。", nameof(code));
        }

        Code = code;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>
    /// 权限码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 显示名。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; }

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$")]
    private static partial Regex CodePattern();
}

/// <summary>
/// 权限码注册表。有界、线程安全；特性在配置阶段登记自己定义的权限，安全子系统与插件权限复用。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PermissionRegistry
{
    /// <summary>
    /// 默认容量。
    /// </summary>
    public const int DefaultCapacity = 10_000;

    private readonly ConcurrentDictionary<string, PermissionDefinition> _permissions = new(StringComparer.Ordinal);
    private readonly int _capacity;

    /// <summary>
    /// 创建注册表。
    /// </summary>
    /// <param name="capacity">容量上限。</param>
    public PermissionRegistry(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    /// <summary>
    /// 已登记数量。
    /// </summary>
    public int Count => _permissions.Count;

    /// <summary>
    /// 登记权限。重复的权限码返回 <see cref="KernelErrors.DuplicatePermission"/>，超出容量返回 <see cref="KernelErrors.PermissionRegistryFull"/>。
    /// </summary>
    public Result Add(PermissionDefinition permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        if (_permissions.Count >= _capacity)
        {
            return new Error(KernelErrors.PermissionRegistryFull, $"权限注册表已达容量上限 {_capacity}。");
        }

        return _permissions.TryAdd(permission.Code, permission)
            ? Result.Success()
            : new Error(KernelErrors.DuplicatePermission, $"权限码 '{permission.Code}' 已登记。");
    }

    /// <summary>
    /// 查找权限。
    /// </summary>
    public Maybe<PermissionDefinition> Find(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _permissions.TryGetValue(code, out var permission) ? Maybe.Some(permission) : default;
    }

    /// <summary>
    /// 全部权限，按权限码排序。
    /// </summary>
    public IReadOnlyList<PermissionDefinition> All() => _permissions.Values.OrderBy(static p => p.Code, StringComparer.Ordinal).ToList();
}
