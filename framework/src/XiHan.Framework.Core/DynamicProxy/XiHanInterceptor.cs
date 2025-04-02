#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanInterceptor
// Guid:28703c92-0d23-43f3-8004-e72a21327f88
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 20:11:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DynamicProxy;

/// <summary>
/// 曦寒拦截器
/// </summary>
public abstract class XiHanInterceptor : IXiHanInterceptor
{
    /// <summary>
    /// 异步拦截
    /// </summary>
    /// <param name="invocation"></param>
    /// <returns></returns>
    public abstract Task InterceptAsync(IXiHanMethodInvocation invocation);
}
