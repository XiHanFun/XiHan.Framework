#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanClaimsMapOptions
// Guid:80bd6346-1fda-4e8a-85e5-0ebfec6fc327
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/01 19:42:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
