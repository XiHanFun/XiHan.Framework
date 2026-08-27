// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace XiHan.Framework.Web.Core.Security.Claims;

/// <summary>
/// 曦寒框架声明转换
/// </summary>
public class XiHanClaimsTransformation : IClaimsTransformation
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="claimsMapOptions"></param>
    public XiHanClaimsTransformation(IOptions<XiHanClaimsMapOptions> claimsMapOptions)
    {
        XiHanClaimsMapOptions = claimsMapOptions;
    }

    /// <summary>
    /// 映射选项
    /// </summary>
    protected IOptions<XiHanClaimsMapOptions> XiHanClaimsMapOptions { get; }

    /// <summary>
    /// 转换
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    /// <remarks>
    /// 必须幂等：ASP.NET Core 不保证每个请求只调用一次 <see cref="IClaimsTransformation"/>，
    /// 认证握手与后续鉴权都可能各调一次。早期实现每调一次就无条件 AddIdentity 一份映射声明，
    /// 同一 principal 上的映射声明会随调用次数累积，下游按类型取单值的代码会拿到重复项，
    /// 令牌体积也随之膨胀。这里在写入前剔除已存在的同类型同值声明。
    /// </remarks>
    public virtual Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var maps = XiHanClaimsMapOptions.Value.Maps;

        var mappedClaims = principal.Claims
            .Where(claim => maps.ContainsKey(claim.Type))
            .Select(claim => new Claim(maps[claim.Type](), claim.Value, claim.ValueType, claim.Issuer))
            .Where(claim => !principal.HasClaim(claim.Type, claim.Value))
            .ToArray();

        if (mappedClaims.Length > 0)
        {
            principal.AddIdentity(new ClaimsIdentity(mappedClaims));
        }

        return Task.FromResult(principal);
    }
}
