// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
