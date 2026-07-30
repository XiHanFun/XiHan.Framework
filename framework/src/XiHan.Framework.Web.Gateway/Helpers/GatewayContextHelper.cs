// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Web.Gateway.Constants;

namespace XiHan.Framework.Web.Gateway.Helpers;

/// <summary>
/// 网关上下文帮助类
/// </summary>
public static class GatewayContextHelper
{
    /// <summary>
    /// 获取 TraceId
    /// </summary>
    public static string? GetTraceId(this HttpContext context)
    {
        return context.Items[GatewayConstants.TraceIdKey]?.ToString();
    }

    /// <summary>
    /// 获取灰度决策
    /// </summary>
    public static IGrayDecision? GetGrayDecision(this HttpContext context)
    {
        return context.Items[GatewayConstants.GrayDecisionKey] as IGrayDecision;
    }

    /// <summary>
    /// 判断是否命中灰度
    /// </summary>
    public static bool IsGrayRequest(this HttpContext context)
    {
        return context.GetGrayDecision()?.IsGray ?? false;
    }

    /// <summary>
    /// 获取目标版本
    /// </summary>
    public static string? GetTargetVersion(this HttpContext context)
    {
        return context.GetGrayDecision()?.TargetVersion;
    }
}
