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
/// 操作 ID 唯一，容量有界。特性在配置阶段经 <c>FeatureContext.AddOperation</c> 登记，协议适配器按它发布端点。操作引用的权限码必须已登记于 <see cref="PermissionRegistry"/>：装配完成时由 <c>AppBuilder.Build</c> 一次核对（未登记即启动失败），此后登记的操作（插件握手之后）由 <see cref="Add"/> 在登记时核对。
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
    /// 装配完成后绑定的权限登记表。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时（装配阶段、单独构造的登记表）登记不核对权限码。只在 <see cref="_gate"/> 内读写。
    /// </remarks>
    private PermissionRegistry? _permissions;

    /// <summary>
    /// 登记的互斥锁。
    /// </summary>
    /// <remarks>
    /// 只串行化写入（整批校验与插入）；读取走并发字典，不取锁。
    /// </remarks>
    private readonly Lock _gate = new();

    /// <summary>
    /// 创建登记表。
    /// </summary>
    /// <param name="capacity">容量上限，至少 1。</param>
    public OperationRegistry(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
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
    /// 单条是 <see cref="AddRange"/> 的特例：重复 ID 返回 <see cref="KernelErrors.DuplicateOperation"/>；装配完成之后登记、引用了未登记权限码的返回 <see cref="KernelErrors.UnregisteredPermission"/>；超限返回 <see cref="KernelErrors.OperationRegistryFull"/>。
    /// </remarks>
    public Result Add(OperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return AddRange([descriptor]);
    }

    /// <summary>
    /// 一次登记一组操作，全部登记或一个都不登记。
    /// </summary>
    /// <remarks>
    /// 插件握手声明的操作经它登记：任一项与已登记的或同一批里的重复（<see cref="KernelErrors.DuplicateOperation"/>）、装配完成之后引用了未登记的权限码（<see cref="KernelErrors.UnregisteredPermission"/>）或整批超出容量（<see cref="KernelErrors.OperationRegistryFull"/>），都返回第一项的失败且登记表不变，不会留下被拒插件的半组操作。空集合返回成功；集合里有 <see langword="null"/> 抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    public Result AddRange(IReadOnlyList<OperationDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        if (descriptors.Any(static d => d is null))
        {
            throw new ArgumentException("Operation descriptors must not contain null.", nameof(descriptors));
        }

        // 校验与写入在同一个锁里：整批要么全进、要么不进，并发登记看不到半批。
        // 登记只发生在配置阶段与插件加载时，读取（Find / All）不取锁
        lock (_gate)
        {
            var batch = new Dictionary<string, OperationDescriptor>(StringComparer.Ordinal);
            var permissions = _permissions;
            foreach (var descriptor in descriptors)
            {
                // 先判重复与权限码再判容量：两者都是配置错误，不该被容量错误掩盖
                if (_operations.TryGetValue(descriptor.Id, out var existing) || batch.TryGetValue(descriptor.Id, out existing))
                {
                    return new Error(KernelErrors.DuplicateOperation, KernelMessages.DuplicateOperation, MessageArguments.Of(("id", descriptor.Id), ("owner", existing.OwnerId)));
                }

                // 装配完成之后才登记的操作（进程外 / Wasm 插件握手之后）没有第二次装配校验的机会：在这里拒绝，插件按登记失败处理
                if (permissions is not null && UnregisteredPermissionOf(descriptor, permissions) is { } code)
                {
                    return Error.Validation(KernelErrors.UnregisteredPermission, KernelMessages.OperationPermissionUnregistered, MessageArguments.Of(("id", descriptor.Id), ("owner", descriptor.OwnerId), ("permission", code)));
                }

                batch.Add(descriptor.Id, descriptor);
            }

            if (_operations.Count + batch.Count > _capacity)
            {
                return new Error(KernelErrors.OperationRegistryFull, KernelMessages.OperationRegistryFull, MessageArguments.Of(("capacity", _capacity)));
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
    internal void RequireRegisteredPermissions(PermissionRegistry permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        // 核对与绑定在同一个锁里：不会有操作在核对之后、绑定之前登记而漏过校验
        lock (_gate)
        {
            var unregistered = new List<string>();
            foreach (var descriptor in All())
            {
                if (UnregisteredPermissionOf(descriptor, permissions) is { } code)
                {
                    unregistered.Add($"'{descriptor.Id}' of '{descriptor.OwnerId}' requires '{code}'");
                }
            }

            if (unregistered.Count > 0)
            {
                throw new FrameworkException(KernelErrors.UnregisteredPermission, $"Operations reference unregistered permission codes: {string.Join("; ", unregistered)}. Register each code with 'FeatureContext.AddPermission'.");
            }

            _permissions = permissions;
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
    /// 操作要求、而登记表里没有的权限码。
    /// </summary>
    /// <remarks>
    /// 没有权限要求或已登记时为 <see langword="null"/>。
    /// </remarks>
    private static string? UnregisteredPermissionOf(OperationDescriptor descriptor, PermissionRegistry permissions)
        => descriptor.Contract.Permission is { } code && !permissions.Find(code).HasValue ? code : null;
}
