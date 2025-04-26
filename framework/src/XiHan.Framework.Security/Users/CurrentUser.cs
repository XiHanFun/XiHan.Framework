#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CurrentUser
// Guid:28c606c9-39f9-4080-92ef-b71119f827b2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/25 5:20:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Claims;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Security.Extensions;

namespace XiHan.Framework.Security.Users;

/// <summary>
/// 当前用户
/// </summary>
public class CurrentUser : ICurrentUser, ITransientDependency
{
    private static readonly Claim[] EmptyClaimsArray = [];

    private readonly ICurrentPrincipalAccessor _principalAccessor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="principalAccessor"></param>
    public CurrentUser(ICurrentPrincipalAccessor principalAccessor)
    {
        _principalAccessor = principalAccessor;
    }

    /// <summary>
    /// 是否认证
    /// </summary>
    public virtual bool IsAuthenticated => Id.HasValue;

    /// <summary>
    /// 用户标识
    /// </summary>
    public virtual Guid? Id => _principalAccessor.Principal?.FindUserId();

    /// <summary>
    /// 用户名
    /// </summary>
    public virtual string? UserName => this.FindClaimValue(XiHanClaimTypes.UserName);

    /// <summary>
    /// 名称
    /// </summary>
    public virtual string? Name => this.FindClaimValue(XiHanClaimTypes.Name);

    /// <summary>
    /// 姓
    /// </summary>
    public virtual string? SurName => this.FindClaimValue(XiHanClaimTypes.SurName);

    /// <summary>
    /// 手机号
    /// </summary>
    public virtual string? PhoneNumber => this.FindClaimValue(XiHanClaimTypes.PhoneNumber);

    /// <summary>
    /// 手机号是否验证
    /// </summary>
    public virtual bool PhoneNumberVerified => string.Equals(this.FindClaimValue(XiHanClaimTypes.PhoneNumberVerified), "true", StringComparison.InvariantCultureIgnoreCase);

    /// <summary>
    /// 邮箱
    /// </summary>
    public virtual string? Email => this.FindClaimValue(XiHanClaimTypes.Email);

    /// <summary>
    /// 邮箱是否验证
    /// </summary>
    public virtual bool EmailVerified => string.Equals(this.FindClaimValue(XiHanClaimTypes.EmailVerified), "true", StringComparison.InvariantCultureIgnoreCase);

    /// <summary>
    /// 租户标识
    /// </summary>
    public virtual Guid? TenantId => _principalAccessor.Principal?.FindTenantId();

    /// <summary>
    /// 角色
    /// </summary>
    public virtual string[] Roles => [.. FindClaims(XiHanClaimTypes.Role).Select(c => c.Value).Distinct()];

    /// <summary>
    /// 获取声明
    /// </summary>
    /// <param name="claimType"></param>
    /// <returns></returns>
    public virtual Claim? FindClaim(string claimType)
    {
        return _principalAccessor.Principal?.Claims.FirstOrDefault(c => c.Type == claimType);
    }

    /// <summary>
    /// 获取所有声明
    /// </summary>
    /// <param name="claimType"></param>
    /// <returns></returns>
    public virtual Claim[] FindClaims(string claimType)
    {
        return _principalAccessor.Principal?.Claims.Where(c => c.Type == claimType).ToArray() ?? EmptyClaimsArray;
    }

    /// <summary>
    /// 获取所有声明
    /// </summary>
    /// <returns></returns>
    public virtual Claim[] GetAllClaims()
    {
        return _principalAccessor.Principal?.Claims.ToArray() ?? EmptyClaimsArray;
    }

    /// <summary>
    /// 是否在角色中
    /// </summary>
    /// <param name="roleName"></param>
    /// <returns></returns>
    public virtual bool IsInRole(string roleName)
    {
        return FindClaims(XiHanClaimTypes.Role).Any(c => c.Value == roleName);
    }
}
