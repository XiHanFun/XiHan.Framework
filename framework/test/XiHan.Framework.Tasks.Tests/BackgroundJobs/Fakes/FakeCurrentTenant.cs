// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 可记录切换轨迹的当前租户替身
/// </summary>
/// <remarks>
/// 后台作业执行前会用入队时的租户标识切换上下文，执行完必须还原；
/// 本替身把每次 Change 的入参记下来，用来验证租户上下文确实被携带并复位。
/// </remarks>
public sealed class FakeCurrentTenant : ICurrentTenant
{
    private readonly object _gate = new();
    private readonly List<long?> _changedIds = [];

    /// <summary>
    /// 当前租户是否可用
    /// </summary>
    public bool IsAvailable => Id.HasValue;

    /// <summary>
    /// 当前租户标识
    /// </summary>
    public long? Id { get; private set; }

    /// <summary>
    /// 当前租户名称
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>
    /// 历次切换传入的租户标识
    /// </summary>
    public IReadOnlyList<long?> ChangedIds
    {
        get
        {
            lock (_gate)
            {
                return [.. _changedIds];
            }
        }
    }

    /// <summary>
    /// 临时切换当前租户
    /// </summary>
    /// <param name="id">租户标识</param>
    /// <param name="name">租户名称</param>
    /// <returns>还原作用域</returns>
    public IDisposable Change(long? id, string? name = null)
    {
        lock (_gate)
        {
            _changedIds.Add(id);
        }

        var previousId = Id;
        var previousName = Name;

        Id = id;
        Name = name;

        return new TenantScope(this, previousId, previousName);
    }

    /// <summary>
    /// 租户还原作用域
    /// </summary>
    private sealed class TenantScope : IDisposable
    {
        private readonly FakeCurrentTenant _owner;
        private readonly long? _previousId;
        private readonly string? _previousName;

        public TenantScope(FakeCurrentTenant owner, long? previousId, string? previousName)
        {
            _owner = owner;
            _previousId = previousId;
            _previousName = previousName;
        }

        public void Dispose()
        {
            _owner.Id = _previousId;
            _owner.Name = _previousName;
        }
    }
}
