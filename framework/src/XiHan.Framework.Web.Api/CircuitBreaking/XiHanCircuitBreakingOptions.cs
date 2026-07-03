#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCircuitBreakingOptions
// Guid:fc07bb7c-d95f-427f-91ff-489f4e32fe9b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.CircuitBreaking;

/// <summary>
/// 入站熔断选项
/// </summary>
/// <remarks>
/// 服务端过载保护熔断器：滑动窗口统计 5xx/未处理异常占比，超阈值后熔断（Open），
/// 熔断期间直接返回 503 + Retry-After，到期后进入半开（HalfOpen）放行少量探测请求，
/// 探测全成功恢复闭合（Closed）、任一失败重新熔断。默认关闭（<see cref="IsEnabled"/>=false），
/// 生产环境应按真实流量与错误基线调参后再开启（阈值过低会误熔断，样本下限过小会被少量错误触发）。
/// 多实例部署时各实例独立统计、独立熔断，无分布式协调。
/// </remarks>
public class XiHanCircuitBreakingOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string SectionName = "XiHan:Web:CircuitBreaking";

    /// <summary>
    /// 是否启用（默认关闭）
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// 滑动统计窗口秒数（默认 60）
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// 窗口内样本下限（默认 50；窗口内请求数不足时不评估熔断，避免低流量误判）
    /// </summary>
    public int MinimumRequests { get; set; } = 50;

    /// <summary>
    /// 失败率阈值（默认 0.5；失败=响应 5xx 或未处理异常，窗口内失败占比达到该值即熔断）
    /// </summary>
    public double FailureRateThreshold { get; set; } = 0.5;

    /// <summary>
    /// 熔断持续秒数（默认 30；到期后进入半开态放行探测）
    /// </summary>
    public int BreakSeconds { get; set; } = 30;

    /// <summary>
    /// 半开态放行的最大探测请求数（默认 5；探测全部成功恢复闭合，任一失败重新熔断）
    /// </summary>
    public int HalfOpenMaxProbes { get; set; } = 5;

    /// <summary>
    /// 豁免路径前缀（默认放行 /health 探活；豁免请求既不被熔断拦截也不参与统计）
    /// </summary>
    public string[] ExemptPathPrefixes { get; set; } = ["/health"];
}
