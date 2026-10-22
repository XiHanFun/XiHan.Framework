// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 应用内全部操作的登记表：操作 ID 唯一，容量有界。特性在配置阶段经 <c>FeatureContext.AddOperation</c> 登记，协议适配器按它发布端点。
/// </summary>
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
    /// 登记操作；重复 ID 返回 <see cref="KernelErrors.DuplicateOperation"/>，超限返回 <see cref="KernelErrors.OperationRegistryFull"/>。
    /// </summary>
    public Result Add(OperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        // 先判重复再判容量：重复登记是配置错误，不该被容量错误掩盖
        if (_operations.TryGetValue(descriptor.Id, out var existing))
        {
            return new Error(KernelErrors.DuplicateOperation, KernelMessages.DuplicateOperation, MessageArguments.Of(("id", descriptor.Id), ("owner", existing.OwnerId)));
        }

        // 容量检查与插入不是一个原子操作：登记只发生在配置阶段且按拓扑顺序串行执行，这里不为此加锁
        if (_operations.Count >= _capacity)
        {
            return new Error(KernelErrors.OperationRegistryFull, KernelMessages.OperationRegistryFull, MessageArguments.Of(("capacity", _capacity)));
        }

        return _operations.TryAdd(descriptor.Id, descriptor)
            ? Result.Success()
            : new Error(KernelErrors.DuplicateOperation, KernelMessages.DuplicateOperation, MessageArguments.Of(("id", descriptor.Id), ("owner", _operations[descriptor.Id].OwnerId)));
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
    /// 全部操作，按 ID 排序。
    /// </summary>
    public IReadOnlyList<OperationDescriptor> All() => _operations.Values.OrderBy(static d => d.Id, StringComparer.Ordinal).ToList();

    /// <summary>
    /// 暴露给指定协议的操作，按 ID 排序。
    /// </summary>
    public IReadOnlyList<OperationDescriptor> ExposedTo(Expose protocol)
        => _operations.Values.Where(d => d.Contract.IsExposedTo(protocol)).OrderBy(static d => d.Id, StringComparer.Ordinal).ToList();
}
