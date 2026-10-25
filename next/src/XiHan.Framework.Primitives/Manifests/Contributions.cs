// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.


namespace XiHan.Framework.Primitives.Manifests;

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
