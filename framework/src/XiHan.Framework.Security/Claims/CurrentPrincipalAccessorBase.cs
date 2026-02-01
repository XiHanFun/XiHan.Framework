#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CurrentPrincipalAccessorBase
// Guid:db713f1c-0e90-4360-b592-3a559a0b3877
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/03 07:04:27
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Claims;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.Security.Claims;

/// <summary>
/// 当前线程上下文中访问 ClaimsPrincipal 的抽象基类
/// 提供了获取当前用户身份信息的功能，并支持临时切换用户上下文
/// </summary>
public abstract class CurrentPrincipalAccessorBase : ICurrentPrincipalAccessor
{
    private readonly AsyncLocal<ClaimsPrincipal> _currentPrincipal = new();

    /// <summary>
    /// 获取当前的 ClaimsPrincipal(用户身份信息)
    /// 如果未显式设置，则通过抽象方法 GetClaimsPrincipal 获取默认值
    /// </summary>
    public ClaimsPrincipal Principal => _currentPrincipal.Value ?? GetClaimsPrincipal();

    /// <summary>
    /// 临时更改当前上下文的 ClaimsPrincipal
    /// 返回 IDisposable 对象，用于在释放时恢复原有的 Principal
    /// </summary>
    /// <param name="principal">要设置的新 ClaimsPrincipal</param>
    /// <returns>一个 IDisposable，用于恢复原有状态</returns>
    public virtual IDisposable Change(ClaimsPrincipal principal)
    {
        return SetCurrent(principal);
    }

    /// <summary>
    /// 获取默认的 ClaimsPrincipal，需由子类实现
    /// </summary>
    /// <returns>默认的 ClaimsPrincipal</returns>
    protected abstract ClaimsPrincipal GetClaimsPrincipal();

    /// <summary>
    /// 设置当前上下文的 ClaimsPrincipal，并返回一个在释放时可恢复原始状态的 DisposeAction
    /// </summary>
    /// <param name="principal">要设置的新 ClaimsPrincipal</param>
    /// <returns>一个 DisposeAction，用于恢复原始的 Principal 状态</returns>
    private IDisposable SetCurrent(ClaimsPrincipal principal)
    {
        var parent = Principal;
        _currentPrincipal.Value = principal;

        return new DisposeAction<(AsyncLocal<ClaimsPrincipal>, ClaimsPrincipal)>(static state =>
        {
            var (currentPrincipal, parent) = state;
            currentPrincipal.Value = parent;
        }, (_currentPrincipal, parent));
    }
}
