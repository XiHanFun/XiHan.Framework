// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
