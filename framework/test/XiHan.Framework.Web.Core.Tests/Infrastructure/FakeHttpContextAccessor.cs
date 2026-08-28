// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace XiHan.Framework.Web.Core.Tests.Infrastructure;

/// <summary>
/// 手写的 HttpContext 访问器替身
/// </summary>
/// <remarks>
/// 框架自带的 HttpContextAccessor 依靠 AsyncLocal 流转上下文，
/// 并发用例需要多个线程看到同一个上下文实例，因此这里用普通字段实现，
/// 同时也让用例可以在断言之间随时替换当前上下文。
/// </remarks>
public sealed class FakeHttpContextAccessor : IHttpContextAccessor
{
    /// <summary>
    /// 当前 HttpContext，未设置时为 null
    /// </summary>
    public HttpContext? HttpContext { get; set; }
}
