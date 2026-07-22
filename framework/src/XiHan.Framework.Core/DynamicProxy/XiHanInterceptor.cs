// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
