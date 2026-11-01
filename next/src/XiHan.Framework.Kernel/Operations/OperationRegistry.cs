// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 应用内全部操作的登记表。
/// </summary>
/// <remarks>
/// 操作 ID 唯一，容量有界。特性在配置阶段经 <c>FeatureContext.AddOperation</c> 登记，协议适配器按它发布端点。登记表在构造时绑定它核对的 <see cref="PermissionRegistry"/>：操作引用的权限码必须已登记，装配完成时由 <c>AppBuilder.Build</c> 一次核对（未登记即启动失败），此后登记的操作（插件握手之后）由 <see cref="AddRange"/> 在登记时核对。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class OperationRegistry
{
    /// <summary>
    /// 默认容量。
    /// </summary>
    public const int DefaultCapacity = 10_000;

    /// <summary>
    /// 操作 ID → 描述。
    /// </summary>
    private readonly ConcurrentDictionary<string, OperationDescriptor> _operations = new(StringComparer.Ordinal);

    /// <summary>
    /// 容量上限。
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 核对操作引用的权限码、并随一组操作登记归属定义的权限码的权限登记表。
    /// </summary>
    private readonly PermissionRegistry _permissions;

    /// <summary>
    /// 每次登记的每一项都要经过的核对。
    /// </summary>
    /// <remarks>
    /// 装配阶段为空（定义权限码的特性可能按拓扑排在后面，此时核对会误报）；装配完成时挂上权限码核对。只在 <see cref="_gate"/> 内读写。
    /// </remarks>
    private readonly List<Func<OperationDescriptor, Error?>> _checks = [];

    /// <summary>
    /// 登记的互斥锁。
    /// </summary>
    /// <remarks>
    /// 只串行化写入（整组校验与插入、挂核对）；读取走并发字典，不取锁。
    /// </remarks>
    private readonly Lock _gate = new();

    /// <summary>
    /// 创建登记表。
    /// </summary>
    /// <param name="permissions">核对操作引用的权限码、登记一组操作的归属定义的权限码所用的权限登记表。</param>
    /// <param name="capacity">容量上限，至少 1。</param>
    public OperationRegistry(PermissionRegistry permissions, int capacity = DefaultCapacity)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _permissions = permissions;
        _capacity = capacity;
    }

    /// <summary>
    /// 已登记的数量。
    /// </summary>
    public int Count => _operations.Count;

    /// <summary>
    /// 登记操作。
    /// </summary>
    /// <remarks>
    /// 是 <see cref="AddRange"/> 只有一个操作、没有权限码的特例：重复 ID 返回 <see cref="KernelErrors.DuplicateOperation"/>；装配完成之后登记、引用了未登记权限码的返回 <see cref="KernelErrors.UnregisteredPermission"/>；超限返回 <see cref="KernelErrors.OperationRegistryFull"/>。
    /// </remarks>
    public Result Add(OperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return AddRange([descriptor], []);
    }

    /// <summary>
    /// 一次登记一组操作连同它们的归属定义的权限码，全部登记或一个都不登记。
    /// </summary>
    /// <remarks>
    /// 插件握手声明的操作与清单声明的权限码经它登记（ADR-0080）。权限码先进权限登记表，同组的操作可以引用它们；任一项不过——操作与已登记的或同一批里的重复（<see cref="KernelErrors.DuplicateOperation"/>）、整批超出容量（<see cref="KernelErrors.OperationRegistryFull"/>）、权限码与已登记的重复或超出容量（<see cref="KernelErrors.DuplicatePermission"/> / <see cref="KernelErrors.PermissionRegistryFull"/>）、装配完成之后引用了未登记的权限码（<see cref="KernelErrors.UnregisteredPermission"/>）——都返回第一项的失败，已写进的权限码撤回，两个登记表都不变，不会留下被拒插件的半组登记。都为空返回成功；集合里有 <see langword="null"/> 抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    /// <param name="descriptors">操作。</param>
    /// <param name="permissions">这组操作的归属定义的权限码；没有时传空集合。</param>
    public Result AddRange(IReadOnlyList<OperationDescriptor> descriptors, IReadOnlyList<PermissionDefinition> permissions)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(permissions);
        if (descriptors.Any(static d => d is null))
        {
            throw new ArgumentException("Operation descriptors must not contain null.", nameof(descriptors));
        }

        // 校验与写入在同一个锁里：整组要么全进、要么不进，并发登记看不到半组。
        // 登记只发生在配置阶段与插件加载时，读取（Find / All）不取锁
        lock (_gate)
        {
            var batch = new Dictionary<string, OperationDescriptor>(StringComparer.Ordinal);
            foreach (var descriptor in descriptors)
            {
                if (_operations.TryGetValue(descriptor.Id, out var existing) || batch.TryGetValue(descriptor.Id, out existing))
                {
                    return new Error(KernelErrors.DuplicateOperation, KernelMessages.DuplicateOperation, MessageArguments.Of(("id", descriptor.Id), ("owner", existing.OwnerId)));
                }

                batch.Add(descriptor.Id, descriptor);
            }

            if (_operations.Count + batch.Count > _capacity)
            {
                return new Error(KernelErrors.OperationRegistryFull, KernelMessages.OperationRegistryFull, MessageArguments.Of(("capacity", _capacity)));
            }

            // 归属定义的权限码先登记：同组操作引用自己定义的码时，下面的核对要看得到它们。
            // 权限登记表自己保证这一批全进或不进；之后的核对不过时在本锁内撤回，此时还没有任何操作引用这些码
            var registered = _permissions.AddRange(permissions);
            if (registered.IsFailure)
            {
                return registered;
            }

            foreach (var descriptor in batch.Values)
            {
                foreach (var check in _checks)
                {
                    if (check(descriptor) is { } error)
                    {
                        _permissions.RemoveRange(permissions);
                        return error;
                    }
                }
            }

            foreach (var (id, descriptor) in batch)
            {
                _operations[id] = descriptor;
            }

            return Result.Success();
        }
    }

    /// <summary>
    /// 按操作 ID 查找。
    /// </summary>
    public Maybe<OperationDescriptor> Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _operations.TryGetValue(id, out var descriptor) ? Maybe.Some(descriptor) : default;
    }

    /// <summary>
    /// 装配完成时核对操作引用的权限码，并对此后的登记逐个核对。
    /// </summary>
    /// <remarks>
    /// 由 <c>AppBuilder.Build</c> 在全部特性配置完之后调用一次；有操作引用未登记的权限码时抛出携带 <see cref="KernelErrors.UnregisteredPermission"/> 的 <see cref="FrameworkException"/>，一次列出全部。
    /// </remarks>
    internal void RequireRegisteredPermissions()
    {
        var unregistered = Require(UnregisteredPermissionOf);
        if (unregistered.Count > 0)
        {
            var violations = unregistered.Select(static d => $"'{d.Id}' of '{d.OwnerId}' requires '{d.Contract.Permission}'");
            throw new FrameworkException(KernelErrors.UnregisteredPermission, $"Operations reference unregistered permission codes: {string.Join("; ", violations)}. Register each code with 'FeatureContext.AddPermission'.");
        }
    }

    /// <summary>
    /// 全部操作，按 ID 排序。
    /// </summary>
    public IReadOnlyList<OperationDescriptor> All() => _operations.Values.OrderBy(static d => d.Id, StringComparer.Ordinal).ToList();

    /// <summary>
    /// 暴露给指定协议的操作，按 ID 排序。
    /// </summary>
    public IReadOnlyList<OperationDescriptor> ExposedTo(Expose protocol)
        => _operations.Values.Where(d => d.Contract.IsExposedTo(protocol)).OrderBy(static d => d.Id, StringComparer.Ordinal).ToList();

    /// <summary>
    /// 核对已登记的全部操作并挂上核对，返回不通过的操作（按 ID 排序）。
    /// </summary>
    private List<OperationDescriptor> Require(Func<OperationDescriptor, Error?> check)
    {
        // 核对与挂上在同一个锁里：不会有操作在核对之后、挂上之前登记而漏过
        lock (_gate)
        {
            var failing = All().Where(descriptor => check(descriptor) is not null).ToList();
            _checks.Add(check);
            return failing;
        }
    }

    /// <summary>
    /// 操作要求的权限码未登记时的错误。
    /// </summary>
    /// <remarks>
    /// 没有权限要求或已登记时为 <see langword="null"/>。
    /// </remarks>
    private Error? UnregisteredPermissionOf(OperationDescriptor descriptor)
        => descriptor.Contract.Permission is { } code && !_permissions.Find(code).HasValue
            ? Error.Validation(KernelErrors.UnregisteredPermission, KernelMessages.OperationPermissionUnregistered, MessageArguments.Of(("id", descriptor.Id), ("owner", descriptor.OwnerId), ("permission", code)))
            : null;
}
