// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Security.Claims;

namespace XiHan.Framework.Web.Core.Security.Claims;

/// <summary>
/// 曦寒框架声明映射选项
/// </summary>
public class XiHanClaimsMapOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanClaimsMapOptions()
    {
        Maps = new Dictionary<string, Func<string>>
        {
            { "sub", () => XiHanClaimTypes.UserId },
            { "role", () => XiHanClaimTypes.Role },
            { "email", () => XiHanClaimTypes.Email },
            { "name", () => XiHanClaimTypes.UserName },
            { "family_name", () => XiHanClaimTypes.SurName },
            { "given_name", () => XiHanClaimTypes.Name }
        };
    }

    /// <summary>
    /// 映射
    /// </summary>
    public Dictionary<string, Func<string>> Maps { get; }
}
