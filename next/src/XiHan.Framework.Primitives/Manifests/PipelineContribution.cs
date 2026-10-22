// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 清单 <c>contributes.pipeline</c> 中的一项：一个中间件及其阶段与相对位置约束。
/// </summary>
/// <param name="Middleware">中间件类型的完全限定名。</param>
/// <param name="Stage">所属阶段。</param>
/// <param name="Before">必须位于这些中间件之前（完全限定名）。</param>
/// <param name="After">必须位于这些中间件之后（完全限定名）。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record PipelineContribution(
    string Middleware,
    PipelineStage Stage,
    IReadOnlyList<string>? Before = null,
    IReadOnlyList<string>? After = null)
{
    /// <summary>
    /// 中间件类型的完全限定名。
    /// </summary>
    public string Middleware { get; } = string.IsNullOrWhiteSpace(Middleware)
        ? throw new ArgumentException("The middleware name must not be empty.", nameof(Middleware))
        : Middleware;

    /// <summary>
    /// 必须位于这些中间件之前。
    /// </summary>
    public IReadOnlyList<string> Before { get; } = Before ?? [];

    /// <summary>
    /// 必须位于这些中间件之后。
    /// </summary>
    public IReadOnlyList<string> After { get; } = After ?? [];
}
