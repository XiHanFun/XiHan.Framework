// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
