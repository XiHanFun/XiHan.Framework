// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道中一个中间件的描述：类型、阶段、位置、归属与相对约束
/// </summary>
/// <remarks>
/// 类型、阶段、位置与归属是必填的初始化成员，没有位置构造与解构：以后加字段只加可选的初始化成员，不破坏已有的构造处（ADR-0142、ADR-0267）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MiddlewareDescriptor
{
    /// <summary>
    /// 中间件类型
    /// </summary>
    public required Type Middleware { get; init; }

    /// <summary>
    /// 所属阶段
    /// </summary>
    public required PipelineStage Stage { get; init; }

    /// <summary>
    /// 阶段内位置
    /// </summary>
    public required Placement Placement { get; init; }

    /// <summary>
    /// 注册者（清单 ID 或 <c>app</c>）
    /// </summary>
    public required string Owner { get; init; }

    /// <summary>
    /// 实例名字
    /// </summary>
    /// <remarks>
    /// 按类型登记的中间件为空，按名字登记的实例（<see cref="PipelineBuilder.UseNamedInstance(string, IPipelineMiddleware, PipelineStage)"/>）为其名字
    /// </remarks>
    public string? Name { get; init; }

    /// <summary>
    /// 必须位于这些中间件之前
    /// </summary>
    public IReadOnlyList<Type> Before { get; init; } = [];

    /// <summary>
    /// 必须位于这些中间件之后
    /// </summary>
    public IReadOnlyList<Type> After { get; init; } = [];
}
