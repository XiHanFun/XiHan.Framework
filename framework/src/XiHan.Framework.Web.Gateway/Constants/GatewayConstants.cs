#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GatewayConstants
// Guid:5c6d7e8f-9a0b-1c2d-3e4f-5a6b7c8d9e0f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Gateway.Constants;

/// <summary>
/// 网关常量
/// </summary>
public static class GatewayConstants
{
    /// <summary>
    /// TraceId 键
    /// </summary>
    public const string TraceIdKey = "XiHan.Gateway.TraceId";

    /// <summary>
    /// 灰度决策键
    /// </summary>
    public const string GrayDecisionKey = "XiHan.Gateway.GrayDecision";

    /// <summary>
    /// 限流键
    /// </summary>
    public const string RateLimitKey = "XiHan.Gateway.RateLimit";

    /// <summary>
    /// 熔断键
    /// </summary>
    public const string CircuitBreakerKey = "XiHan.Gateway.CircuitBreaker";

    /// <summary>
    /// 请求上下文键
    /// </summary>
    public const string RequestContextKey = "XiHan.Gateway.RequestContext";

    /// <summary>
    /// Header 前缀
    /// </summary>
    public static class Headers
    {
        /// <summary>
        /// TraceId Header
        /// </summary>
        public const string TraceId = "X-Trace-Id";

        /// <summary>
        /// 灰度版本 Header
        /// </summary>
        public const string GrayVersion = "X-Gray-Version";

        /// <summary>
        /// 用户ID Header
        /// </summary>
        public const string UserId = "X-User-Id";

        /// <summary>
        /// 租户ID Header
        /// </summary>
        public const string TenantId = "X-Tenant-Id";
    }
}
