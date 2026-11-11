// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Endpoints;

/// <summary>
/// 端点元数据
/// </summary>
/// <remarks>
/// 带有它的端点不经过宿主的内核管道，应用未就绪时也可访问。探针端点自带；静态资源等可用 <see cref="BypassXiHanPipeline"/> 标注。这类端点上的 <c>RequireXiHan*</c> 要求不会被判定，<c>UseXiHan()</c> 发现即启动失败；处理器自己以调用上下文走完整管道并带上端点要求的（gRPC 操作方法）用 <see cref="SelfInvoking"/> 标注（ADR-0175）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PipelineBypassMetadata
{
    /// <summary>
    /// 不允许外部构造
    /// </summary>
    /// <remarks>
    /// 只有两种：<see cref="Instance"/> 与 <see cref="SelfInvoking"/>
    /// </remarks>
    private PipelineBypassMetadata(bool evaluatesEndpointRequirements)
    {
        EvaluatesEndpointRequirements = evaluatesEndpointRequirements;
    }

    /// <summary>
    /// 共享实例
    /// </summary>
    public static PipelineBypassMetadata Instance { get; } = new(false);

    /// <summary>
    /// 处理器自己走内核管道的端点
    /// </summary>
    /// <remarks>
    /// 端点绕过宿主管道，由处理器以自己的调用上下文走完整管道，并把端点上的全部授权要求带进去（gRPC 操作方法）；启动核对不把这类端点上的要求当成不会判定
    /// </remarks>
    public static PipelineBypassMetadata SelfInvoking { get; } = new(true);

    /// <summary>
    /// 端点上的授权要求是否由处理器自己判定
    /// </summary>
    public bool EvaluatesEndpointRequirements { get; }

    /// <summary>
    /// 让端点绕过内核管道
    /// </summary>
    public static TBuilder BypassXiHanPipeline<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WithMetadata(Instance);
        return builder;
    }
}
