// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 权限码注册表。
/// </summary>
/// <remarks>
/// 有界、线程安全；每个权限码登记时记下归属（定义它的特性或插件的清单 ID）。特性在配置阶段登记自己定义的权限，插件宿主在登记插件的代理操作时替插件登记它清单声明的权限码（ADR-0080），卸载插件时按归属撤回（ADR-0085），安全子系统按它判定。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PermissionRegistry
{
    /// <summary>
    /// 默认容量。
    /// </summary>
    public const int DefaultCapacity = 10_000;

    /// <summary>
    /// 已登记的权限连同归属，按权限码索引。
    /// </summary>
    private readonly ConcurrentDictionary<string, (PermissionDefinition Definition, string OwnerId)> _permissions = new(StringComparer.Ordinal);

    /// <summary>
    /// 容量上限。
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 登记的互斥锁。
    /// </summary>
    /// <remarks>
    /// 只串行化写入（整批校验与插入、撤回）；读取走并发字典，不取锁。插件在运行期加载与卸载时也会登记与撤回，不能只靠"登记都在配置阶段串行发生"。
    /// </remarks>
    private readonly Lock _gate = new();

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
    /// 登记一个归属定义的权限。
    /// </summary>
    /// <remarks>
    /// 单条是 <see cref="AddRange"/> 的特例：重复的权限码返回 <see cref="KernelErrors.DuplicatePermission"/>，超出容量返回 <see cref="KernelErrors.PermissionRegistryFull"/>。
    /// </remarks>
    /// <param name="ownerId">归属：定义它的特性或插件的清单 ID。</param>
    /// <param name="permission">权限。</param>
    public Result Add(string ownerId, PermissionDefinition permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        return AddRange(ownerId, [permission]);
    }

    /// <summary>
    /// 一次登记一个归属定义的一组权限，全部登记或一个都不登记。
    /// </summary>
    /// <remarks>
    /// 任一项与已登记的（不论归属）或同一批里的重复（<see cref="KernelErrors.DuplicatePermission"/>）、整批超出容量（<see cref="KernelErrors.PermissionRegistryFull"/>），都返回第一项的失败且注册表不变。空集合返回成功；集合里有 <see langword="null"/> 抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    /// <param name="ownerId">归属：定义它们的特性或插件的清单 ID，<see cref="RemoveOwnedBy"/> 按它撤回。</param>
    /// <param name="permissions">权限。</param>
    public Result AddRange(string ownerId, IReadOnlyList<PermissionDefinition> permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(permissions);
        if (permissions.Any(static p => p is null))
        {
            throw new ArgumentException("Permission definitions must not contain null.", nameof(permissions));
        }

        // 校验与写入在同一个锁里：整批要么全进、要么不进，并发登记看不到半批，容量也不会被并发越过
        lock (_gate)
        {
            var batch = new HashSet<string>(StringComparer.Ordinal);
            foreach (var permission in permissions)
            {
                // 先判重复再判容量：重复是配置错误，不该被容量错误掩盖
                if (_permissions.ContainsKey(permission.Code) || !batch.Add(permission.Code))
                {
                    return new Error(KernelErrors.DuplicatePermission, KernelMessages.DuplicatePermission, MessageArguments.Of(("code", permission.Code)));
                }
            }

            if (_permissions.Count + batch.Count > _capacity)
            {
                return new Error(KernelErrors.PermissionRegistryFull, KernelMessages.PermissionRegistryFull, MessageArguments.Of(("capacity", _capacity)));
            }

            foreach (var permission in permissions)
            {
                _permissions[permission.Code] = (permission, ownerId);
            }

            return Result.Success();
        }
    }

    /// <summary>
    /// 查找权限。
    /// </summary>
    public Maybe<PermissionDefinition> Find(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _permissions.TryGetValue(code, out var entry) ? Maybe.Some(entry.Definition) : default;
    }

    /// <summary>
    /// 全部权限，按权限码排序。
    /// </summary>
    public IReadOnlyList<PermissionDefinition> All() => _permissions.Values.Select(static e => e.Definition).OrderBy(static p => p.Code, StringComparer.Ordinal).ToList();

    /// <summary>
    /// 撤回一个归属登记的全部权限码。
    /// </summary>
    /// <remarks>
    /// 在登记的锁里一次撤回，并发的登记看不到撤回了一半的状态；没有登记过的归属什么都不做。只撤回权限码：同一归属的操作要一起撤回时用 <see cref="Operations.OperationRegistry.RemoveOwnedBy"/>（先撤回操作再撤回权限码，ADR-0085）。别的归属的操作引用了被撤回的码时不受影响，授权判定时按策略不存在拒绝。
    /// </remarks>
    /// <param name="ownerId">归属：特性或插件的清单 ID。</param>
    /// <returns>撤回的权限码数。</returns>
    public int RemoveOwnedBy(string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        lock (_gate)
        {
            var owned = _permissions.Where(e => string.Equals(e.Value.OwnerId, ownerId, StringComparison.Ordinal)).Select(static e => e.Key).ToList();
            foreach (var code in owned)
            {
                _permissions.TryRemove(code, out _);
            }

            return owned.Count;
        }
    }

    /// <summary>
    /// 撤回刚由 <see cref="AddRange"/> 登记的一组权限。
    /// </summary>
    /// <remarks>
    /// 只供 <see cref="Operations.OperationRegistry"/> 在一组操作连同其权限码登记失败时回滚：调用方持有操作登记表的锁，这组权限码登记之后还没有任何操作引用它们。
    /// </remarks>
    internal void RemoveRange(IReadOnlyList<PermissionDefinition> permissions)
    {
        lock (_gate)
        {
            foreach (var permission in permissions)
            {
                _permissions.TryRemove(permission.Code, out _);
            }
        }
    }
}
