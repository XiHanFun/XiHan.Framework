#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanInterceptor
// Guid:6b094250-9ad9-4b7c-a44d-84e100a154e1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 01:34:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
