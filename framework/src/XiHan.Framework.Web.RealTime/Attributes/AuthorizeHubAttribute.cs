#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuthorizeHubAttribute
// Guid:ed8e9f1a-2b3c-4d5e-9f1a-eb6c7d8e9f1a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 05:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Authorization;

namespace XiHan.Framework.Web.RealTime.Attributes;

/// <summary>
/// Hub 授权特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class AuthorizeHubAttribute : AuthorizeAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public AuthorizeHubAttribute()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="policy">策略名称</param>
    public AuthorizeHubAttribute(string policy) : base(policy)
    {
    }
}
