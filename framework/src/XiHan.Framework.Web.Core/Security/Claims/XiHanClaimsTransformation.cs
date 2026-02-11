#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanClaimsTransformation
// Guid:bf65e21c-9742-4a18-aac0-3b8a01122177
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/01 19:41:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    public virtual Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var mapClaims = principal.Claims.Where(claim => XiHanClaimsMapOptions.Value.Maps.ContainsKey(claim.Type));

        principal.AddIdentity(new ClaimsIdentity(mapClaims.Select(
                    claim => new Claim(
                        XiHanClaimsMapOptions.Value.Maps[claim.Type](),
                        claim.Value,
                        claim.ValueType,
                        claim.Issuer))));

        return Task.FromResult(principal);
    }
}
