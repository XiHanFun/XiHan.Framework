#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRequestContextAccessor
// Guid:1f6c402b-6cc6-4f89-9c8b-58d515f5f8a0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
