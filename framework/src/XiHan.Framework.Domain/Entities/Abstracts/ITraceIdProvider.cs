#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITraceIdProvider
// Guid:4c5d6e7f-8091-a2b3-c4d5-e6f708192031
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/08 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 链路追踪标识提供者
/// 为不依赖 ASP.NET Core 的层提供当前请求的 TraceId
/// </summary>
public interface ITraceIdProvider
{
    /// <summary>
    /// 获取当前请求的链路追踪标识
    /// </summary>
    /// <returns>TraceId，如果不在请求上下文中则返回 null</returns>
    string? GetCurrentTraceId();
}
