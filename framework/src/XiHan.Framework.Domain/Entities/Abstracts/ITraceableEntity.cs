// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 链路追踪实体接口
/// 实现此接口的实体将携带 TraceId，用于在分布式链路中串联同一请求的所有日志
/// 中间件或拦截器可检测此接口并自动从 HttpContext / Activity 中填充 TraceId
/// </summary>
public interface ITraceableEntity
{
    /// <summary>
    /// 链路追踪ID
    /// 通常取自 W3C Trace Context 或 HttpContext.TraceIdentifier，用于串联整个请求生命周期
    /// </summary>
    string? TraceId { get; set; }
}
