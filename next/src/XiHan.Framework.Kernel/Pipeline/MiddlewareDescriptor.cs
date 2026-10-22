// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道中一个中间件的描述：类型、阶段、位置、归属与相对约束。
/// </summary>
/// <param name="Middleware">中间件类型。</param>
/// <param name="Stage">所属阶段。</param>
/// <param name="Placement">阶段内位置。</param>
/// <param name="Owner">注册者（清单 ID 或 <c>app</c>）。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MiddlewareDescriptor(Type Middleware, PipelineStage Stage, Placement Placement, string Owner)
{
    /// <summary>
    /// 实例名字；按类型登记的中间件为空，按名字登记的实例（<see cref="PipelineBuilder.UseNamedInstance"/>）为其名字。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 必须位于这些中间件之前。
    /// </summary>
    public IReadOnlyList<Type> Before { get; init; } = [];

    /// <summary>
    /// 必须位于这些中间件之后。
    /// </summary>
    public IReadOnlyList<Type> After { get; init; } = [];
}
