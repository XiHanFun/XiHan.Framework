// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 清单 <c>contributes.pipeline</c> 中的一项：一个中间件及其阶段与相对位置约束
/// </summary>
/// <remarks>
/// 与 <see cref="Binding"/> 同形：必需的字段进构造，可选的位置约束经初始化器设置，以后加字段只加属性（ADR-0137）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record PipelineContribution
{
    /// <summary>
    /// 创建一项贡献
    /// </summary>
    /// <param name="middleware">中间件类型的完全限定名</param>
    /// <param name="stage">所属阶段</param>
    public PipelineContribution(string middleware, PipelineStage stage)
    {
        Middleware = string.IsNullOrWhiteSpace(middleware)
            ? throw new ArgumentException("The middleware name must not be empty.", nameof(middleware))
            : middleware;
        Stage = stage;
    }

    /// <summary>
    /// 中间件类型的完全限定名
    /// </summary>
    public string Middleware { get; }

    /// <summary>
    /// 所属阶段
    /// </summary>
    public PipelineStage Stage { get; }

    /// <summary>
    /// 必须位于这些中间件之前（完全限定名）
    /// </summary>
    /// <remarks>
    /// 缺省为空；赋 <see langword="null"/> 按空处理
    /// </remarks>
    [AllowNull]
    public IReadOnlyList<string> Before
    {
        get;
        init => field = value ?? [];
    } = [];

    /// <summary>
    /// 必须位于这些中间件之后（完全限定名）
    /// </summary>
    /// <remarks>
    /// 缺省为空；赋 <see langword="null"/> 按空处理
    /// </remarks>
    [AllowNull]
    public IReadOnlyList<string> After
    {
        get;
        init => field = value ?? [];
    } = [];
}
