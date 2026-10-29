// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 权限码注册表。
/// </summary>
/// <remarks>
/// 有界、线程安全；特性在配置阶段登记自己定义的权限，安全子系统与插件权限复用。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PermissionRegistry
{
    /// <summary>
    /// 默认容量。
    /// </summary>
    public const int DefaultCapacity = 10_000;

    /// <summary>
    /// 已登记的权限，按权限码索引。
    /// </summary>
    private readonly ConcurrentDictionary<string, PermissionDefinition> _permissions = new(StringComparer.Ordinal);

    /// <summary>
    /// 容量上限。
    /// </summary>
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
    /// 登记权限。
    /// </summary>
    /// <remarks>
    /// 重复的权限码返回 <see cref="KernelErrors.DuplicatePermission"/>，超出容量返回 <see cref="KernelErrors.PermissionRegistryFull"/>。
    /// </remarks>
    public Result Add(PermissionDefinition permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        // 容量检查与插入不是一个原子操作：并发登记在临界点可能略微越过上限。
        // 登记只发生在配置阶段且按拓扑顺序串行执行，这里不为此加锁。
        if (_permissions.Count >= _capacity)
        {
            return new Error(KernelErrors.PermissionRegistryFull, KernelMessages.PermissionRegistryFull, MessageArguments.Of(("capacity", _capacity)));
        }

        return _permissions.TryAdd(permission.Code, permission)
            ? Result.Success()
            : new Error(KernelErrors.DuplicatePermission, KernelMessages.DuplicatePermission, MessageArguments.Of(("code", permission.Code)));
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
