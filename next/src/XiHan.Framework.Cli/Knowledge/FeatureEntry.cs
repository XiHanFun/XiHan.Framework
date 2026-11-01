// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 一个特性的清单。
/// </summary>
/// <remarks>
/// 字段对应 RFC-0002 的清单：身份、种类与角色、提供与依赖的能力、资源绑定、贡献的配置节、管道中间件、操作、权限码与健康项。
/// </remarks>
internal sealed record FeatureEntry
{
    /// <summary>
    /// 特性类型的完全限定名（泛型写作 <c>Name&lt;T&gt;</c>）。
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 所属包 ID。
    /// </summary>
    public string Package { get; set; } = string.Empty;

    /// <summary>
    /// 清单 ID。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 种类：<c>capability</c>、<c>provider</c> 等。
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 角色：<c>provider</c> 或 <c>default</c>。
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 稳定性。
    /// </summary>
    public string Stability { get; set; } = string.Empty;

    /// <summary>
    /// 提供的能力，形如 <c>messaging.queue@1</c>。
    /// </summary>
    public IReadOnlyList<string> Provides { get; set; } = [];

    /// <summary>
    /// 依赖的能力。
    /// </summary>
    public IReadOnlyList<RequirementEntry> Requires { get; set; } = [];

    /// <summary>
    /// 需要宿主注入的资源绑定。
    /// </summary>
    public IReadOnlyList<BindingEntry> Bindings { get; set; } = [];

    /// <summary>
    /// 贡献的领域配置节。
    /// </summary>
    public string? Config { get; set; }

    /// <summary>
    /// 贡献的管道中间件。
    /// </summary>
    public IReadOnlyList<PipelineEntry> Pipeline { get; set; } = [];

    /// <summary>
    /// 贡献的操作 ID。
    /// </summary>
    public IReadOnlyList<string> Operations { get; set; } = [];

    /// <summary>
    /// 定义的权限码。
    /// </summary>
    public IReadOnlyList<string> Permissions { get; set; } = [];

    /// <summary>
    /// 登记的健康项。
    /// </summary>
    public IReadOnlyList<string> Health { get; set; } = [];
}
