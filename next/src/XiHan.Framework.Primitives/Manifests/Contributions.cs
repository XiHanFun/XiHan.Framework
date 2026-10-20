// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

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
        ? throw new ArgumentException("中间件名不能为空。", nameof(Middleware))
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

/// <summary>
/// 清单 <c>contributes.config</c>：配置节与可选的 JSON Schema 路径。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial record ConfigContribution
{
    /// <summary>
    /// 创建配置贡献。
    /// </summary>
    /// <param name="section">配置节，格式 <c>XiHan:&lt;领域&gt;</c>。</param>
    /// <param name="schema">JSON Schema 的相对路径。</param>
    public ConfigContribution(string section, string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (!SectionPattern().IsMatch(section))
        {
            throw new ArgumentException($"配置节 '{section}' 必须是 XiHan:<领域> 形式。", nameof(section));
        }

        Section = section;
        Schema = schema;
    }

    /// <summary>
    /// 配置节。
    /// </summary>
    public string Section { get; }

    /// <summary>
    /// JSON Schema 的相对路径。
    /// </summary>
    public string? Schema { get; }

    [GeneratedRegex("^XiHan:[A-Z][A-Za-z0-9]*$")]
    private static partial Regex SectionPattern();
}

/// <summary>
/// 清单 <c>contributes</c>：包向宿主贡献的管道中间件、操作、配置、权限码与健康项。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record Contributions
{
    /// <summary>
    /// 没有任何贡献。
    /// </summary>
    public static Contributions Empty { get; } = new();

    /// <summary>
    /// 管道中间件。
    /// </summary>
    public IReadOnlyList<PipelineContribution> Pipeline { get; init; } = [];

    /// <summary>
    /// 操作 ID。
    /// </summary>
    public IReadOnlyList<string> Operations { get; init; } = [];

    /// <summary>
    /// 配置节。
    /// </summary>
    public ConfigContribution? Config { get; init; }

    /// <summary>
    /// 此包定义的权限码。
    /// </summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>
    /// 健康项名称。
    /// </summary>
    public IReadOnlyList<string> Health { get; init; } = [];
}
