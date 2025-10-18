#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ThreadCurrentPrincipalAccessor
// Guid:96cff22b-89b2-4cfd-804b-2bbda58f6114
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 6:41:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Claims;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Security.Claims;

/// <summary>
/// 线程当前主体访问器
/// </summary>
public class ThreadCurrentPrincipalAccessor : CurrentPrincipalAccessorBase, ISingletonDependency
{
    /// <summary>
    /// 获取声明主体
    /// </summary>
    /// <returns></returns>
    protected override ClaimsPrincipal GetClaimsPrincipal()
    {
        return (Thread.CurrentPrincipal as ClaimsPrincipal)!;
    }
}
