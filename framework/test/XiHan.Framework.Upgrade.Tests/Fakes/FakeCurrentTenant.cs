// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Upgrade.Tests.Fakes;

/// <summary>
/// 当前租户的手写替身
/// </summary>
/// <remarks>
/// 升级模块以租户作为版本状态与迁移历史的隔离维度，测试需要一个可确定切换的租户上下文。
/// <see cref="Change"/> 按真实语义返回可还原的作用域句柄。
/// </remarks>
public sealed class FakeCurrentTenant : ICurrentTenant
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">租户标识</param>
    /// <param name="name">租户名称</param>
    public FakeCurrentTenant(long? id = null, string? name = null)
    {
        Id = id;
        Name = name;
    }

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
    /// 租户切换次数
    /// </summary>
    public int ChangeCount { get; private set; }

    /// <summary>
    /// 临时切换当前租户
    /// </summary>
    /// <param name="id">目标租户标识</param>
    /// <param name="name">目标租户名称</param>
    /// <returns>还原句柄</returns>
    public IDisposable Change(long? id, string? name = null)
    {
        ChangeCount++;
        var scope = new ChangeScope(this, Id, Name);
        Id = id;
        Name = name;
        return scope;
    }

    /// <summary>
    /// 租户切换还原句柄
    /// </summary>
    private sealed class ChangeScope : IDisposable
    {
        private readonly FakeCurrentTenant _owner;
        private readonly long? _previousId;
        private readonly string? _previousName;

        public ChangeScope(FakeCurrentTenant owner, long? previousId, string? previousName)
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
