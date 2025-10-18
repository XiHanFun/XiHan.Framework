#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CurrentTenant
// Guid:80fcd01b-d311-498b-912a-3cbd37370ee4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 6:27:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 当前租户
/// </summary>
public class CurrentTenant : ICurrentTenant, ITransientDependency
{
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="currentTenantAccessor">当前租户访问器</param>
    public CurrentTenant(ICurrentTenantAccessor currentTenantAccessor)
    {
        _currentTenantAccessor = currentTenantAccessor;
    }

    /// <summary>
    /// 是否可用
    /// </summary>
    /// <returns>是否可用</returns>
    public virtual bool IsAvailable => Id.HasValue;

    /// <summary>
    /// 租户唯一标识
    /// </summary>
    /// <returns>租户唯一标识</returns>
    public virtual Guid? Id => _currentTenantAccessor.Current?.TenantId;

    /// <summary>
    /// 租户名称
    /// </summary>
    /// <returns>租户名称</returns>
    public string? Name => _currentTenantAccessor.Current?.Name;

    /// <summary>
    /// 更改当前租户
    /// </summary>
    /// <param name="id">租户唯一标识</param>
    /// <param name="name">租户名称</param>
    /// <returns>IDisposable</returns>
    public IDisposable Change(Guid? id, string? name = null)
    {
        return SetCurrent(id, name);
    }

    /// <summary>
    /// 设置当前租户
    /// </summary>
    /// <param name="tenantId">租户唯一标识</param>
    /// <param name="name">租户名称</param>
    /// <returns>IDisposable</returns>
    private IDisposable SetCurrent(Guid? tenantId, string? name = null)
    {
        var parentScope = _currentTenantAccessor.Current;
        _currentTenantAccessor.Current = new BasicTenantInfo(tenantId, name);

        return new DisposeAction<ValueTuple<ICurrentTenantAccessor, BasicTenantInfo?>>(static (state) =>
        {
            var (currentTenantAccessor, parentScope) = state;
            currentTenantAccessor.Current = parentScope;
        }, (_currentTenantAccessor, parentScope));
    }
}
