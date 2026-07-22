// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DynamicProxy;

/// <summary>
/// 曦寒拦截器接口
/// </summary>
public interface IXiHanInterceptor
{
    /// <summary>
    /// 异步拦截
    /// </summary>
    /// <param name="invocation"></param>
    /// <returns></returns>
    Task InterceptAsync(IXiHanMethodInvocation invocation);
}
