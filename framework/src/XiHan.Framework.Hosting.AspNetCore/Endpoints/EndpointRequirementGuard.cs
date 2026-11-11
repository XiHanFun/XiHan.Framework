// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace XiHan.Framework.Hosting.AspNetCore.Endpoints;

/// <summary>
/// 确认端点上的授权要求已由 <c>UseXiHan()</c> 判定的守卫
/// </summary>
/// <remarks>
/// 宿主没调 <c>UseXiHan()</c>，或者它排在路由之前（读不到端点），端点上的 <c>RequireXiHan*</c> 就不会被判定；守卫在端点执行前看请求里有没有判定过的标记，没有就抛出而不执行端点，宁可失败不可放行（ADR-0175）。同时充当端点元数据，标明端点已经包过一层
/// </remarks>
internal sealed class EndpointRequirementGuard
{
    /// <summary>
    /// 请求条目里「端点要求已写进调用上下文」的标记键
    /// </summary>
    private static readonly object _evaluatedKey = new();

    /// <summary>
    /// 不允许外部构造
    /// </summary>
    private EndpointRequirementGuard()
    {
    }

    /// <summary>
    /// 共享实例
    /// </summary>
    public static EndpointRequirementGuard Instance { get; } = new();

    /// <summary>
    /// <c>UseXiHan()</c> 把端点上的要求写进调用上下文之后标记请求
    /// </summary>
    public static void MarkEvaluated(HttpContext httpContext) => httpContext.Items[_evaluatedKey] = true;

    /// <summary>
    /// 给端点包一层守卫
    /// </summary>
    /// <remarks>
    /// 作为最终约定执行（端点的处理委托此时已经生成）；绕过宿主管道的端点要么由处理器自己判定（gRPC），要么已被启动核对拒绝，不包；同一端点只包一次
    /// </remarks>
    public static void Apply(EndpointBuilder endpoint)
    {
        if (endpoint.RequestDelegate is not { } inner
            || endpoint.Metadata.OfType<PipelineBypassMetadata>().Any()
            || endpoint.Metadata.OfType<EndpointRequirementGuard>().Any())
        {
            return;
        }

        var name = endpoint.DisplayName ?? endpoint.ToString() ?? string.Empty;
        endpoint.Metadata.Add(Instance);
        endpoint.RequestDelegate = httpContext => httpContext.Items.ContainsKey(_evaluatedKey)
            ? inner(httpContext)
            : throw new InvalidOperationException($"Endpoint '{name}' carries XiHan authorization requirements, but UseXiHan() did not evaluate them. Call app.UseXiHan() after routing (UseRouting) so that it sees the endpoint.");
    }
}
