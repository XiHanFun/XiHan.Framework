// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Contexts;

/// <summary>
/// 请求上下文访问器
/// </summary>
public interface IRequestContextAccessor
{
    /// <summary>
    /// 当前请求上下文
    /// </summary>
    RequestContext? Current { get; set; }
}
