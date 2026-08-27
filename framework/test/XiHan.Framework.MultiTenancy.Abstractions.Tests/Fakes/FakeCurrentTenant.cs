// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

/// <summary>
/// 当前租户的手写替身
/// </summary>
/// <remarks>
/// 按 <see cref="ICurrentTenant"/> 文档所描述的语义实现：
/// <see cref="Change"/> 把新的租户信息压入访问器，返回的释放器负责把访问器还原到进入作用域之前的那一份快照。
/// 释放器实现为幂等，重复释放不会二次还原——这是嵌套作用域能正确工作的前提。
/// </remarks>
internal sealed class FakeCurrentTenant : ICurrentTenant
{
    private readonly ICurrentTenantAccessor _accessor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="accessor">当前租户访问器</param>
    public FakeCurrentTenant(ICurrentTenantAccessor accessor)
    {
        _accessor = accessor;
    }

    /// <summary>
    /// 当前租户是否可用
    /// </summary>
    public bool IsAvailable => Id.HasValue;

    /// <summary>
    /// 当前租户唯一标识
    /// </summary>
    public long? Id => _accessor.Current?.TenantId;

    /// <summary>
    /// 当前租户名称
    /// </summary>
    public string? Name => _accessor.Current?.Name;

    /// <summary>
    /// 临时切换当前租户
    /// </summary>
    /// <param name="id">租户唯一标识，传 null 表示切换到无租户（宿主）状态</param>
    /// <param name="name">租户名称</param>
    /// <returns>用于还原上一层租户上下文的释放器</returns>
    public IDisposable Change(long? id, string? name = null)
    {
        var parent = _accessor.Current;
        _accessor.Current = new BasicTenantInfo(id, name);
        return new TenantScope(_accessor, parent);
    }

    /// <summary>
    /// 租户切换作用域
    /// </summary>
    private sealed class TenantScope : IDisposable
    {
        private readonly ICurrentTenantAccessor _accessor;
        private readonly BasicTenantInfo? _parent;
        private bool _disposed;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="accessor">当前租户访问器</param>
        /// <param name="parent">进入作用域之前的租户快照</param>
        public TenantScope(ICurrentTenantAccessor accessor, BasicTenantInfo? parent)
        {
            _accessor = accessor;
            _parent = parent;
        }

        /// <summary>
        /// 还原上一层租户上下文
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _accessor.Current = _parent;
        }
    }
}
