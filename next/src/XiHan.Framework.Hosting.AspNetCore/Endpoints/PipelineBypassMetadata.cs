// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Endpoints;

/// <summary>
/// 端点元数据。
/// </summary>
/// <remarks>
/// 带有它的端点不经过内核管道，应用未就绪时也可访问。探针端点自带；静态资源等可用 <see cref="BypassXiHanPipeline"/> 标注。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PipelineBypassMetadata
{
    /// <summary>
    /// 不允许外部构造。
    /// </summary>
    /// <remarks>
    /// 元数据无状态，统一使用 <see cref="Instance"/>。
    /// </remarks>
    private PipelineBypassMetadata()
    {
    }

    /// <summary>
    /// 共享实例。
    /// </summary>
    public static PipelineBypassMetadata Instance { get; } = new();

    /// <summary>
    /// 让端点绕过内核管道。
    /// </summary>
    public static TBuilder BypassXiHanPipeline<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WithMetadata(Instance);
        return builder;
    }
}
