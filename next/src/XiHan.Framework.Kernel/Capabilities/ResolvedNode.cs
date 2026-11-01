// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// 已解析图中的一个节点：清单、它所依赖的节点、缺失的可选依赖、被选中提供的能力。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ResolvedNode
{
    /// <summary>
    /// 依赖的节点，按清单 <c>requires</c> 顺序追加。
    /// </summary>
    private readonly List<ResolvedNode> _dependencies = [];

    /// <summary>
    /// 缺失的可选依赖。
    /// </summary>
    private readonly List<CapabilityRequirement> _missingOptional = [];

    /// <summary>
    /// 被选中提供的能力 ID。
    /// </summary>
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);

    /// <summary>
    /// 被选中且是被要求的能力 ID（<see cref="_selected"/> 的子集）。
    /// </summary>
    private readonly HashSet<string> _requested = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建节点。
    /// </summary>
    /// <remarks>
    /// 只由 <see cref="CapabilityResolver"/> 调用，依赖与选中结果在求解过程中追加。
    /// </remarks>
    /// <param name="manifest">清单。</param>
    internal ResolvedNode(Manifest manifest) => Manifest = manifest;

    /// <summary>
    /// 清单。
    /// </summary>
    public Manifest Manifest { get; }

    /// <summary>
    /// 本节点依赖的节点（按清单 <c>requires</c> 顺序）。
    /// </summary>
    public IReadOnlyList<ResolvedNode> Dependencies => _dependencies;

    /// <summary>
    /// 缺失的可选依赖。
    /// </summary>
    /// <remarks>
    /// 非空时对应特性启动后进入 <see cref="LifecycleState.Degraded"/>。
    /// </remarks>
    public IReadOnlyList<CapabilityRequirement> MissingOptional => _missingOptional;

    /// <summary>
    /// 是否因可选依赖缺失而降级。
    /// </summary>
    public bool IsDegraded => _missingOptional.Count > 0;

    /// <summary>
    /// 本节点被选中提供的能力 ID。
    /// </summary>
    /// <remarks>
    /// 声明了但未被选中的能力由别的节点提供，本节点不得为其注册服务或中间件。
    /// </remarks>
    public IReadOnlyCollection<string> SelectedCapabilities => _selected;

    /// <summary>
    /// 本节点声明了能力却一个都没被选中：宿主不装配、不启动它。
    /// </summary>
    public bool IsShadowed => Manifest.Provides.Count > 0 && _selected.Count == 0;

    /// <summary>
    /// 本节点是否被选中提供该能力。
    /// </summary>
    public bool IsSelectedFor(string capabilityId)
    {
        ArgumentNullException.ThrowIfNull(capabilityId);
        return _selected.Contains(capabilityId);
    }

    /// <summary>
    /// 本节点是否被选中提供该能力，且这一选择是被要求的。
    /// </summary>
    /// <remarks>
    /// 被要求：有清单依赖这项能力（必需或可选，且解析到了提供者），或配置 <c>XiHan:Providers:&lt;能力ID&gt;</c> / 代码的 <c>UseProvider</c> 指定了提供者。
    /// 没人依赖也没人指定、只按「没被依赖的能力也选出提供者」的规则选出的是兜底选出，返回 <see langword="false"/>；兜底选出照样算选中（<see cref="IsSelectedFor"/>）。
    /// 特性据此只为被要求的能力承担跨能力的一致性约束（RFC-0002 解析规则 4）。
    /// </remarks>
    [ApiLevel(Stability.Preview, "5.0")]
    public bool IsRequestedFor(string capabilityId)
    {
        ArgumentNullException.ThrowIfNull(capabilityId);
        return _requested.Contains(capabilityId);
    }

    /// <summary>
    /// 追加一个依赖节点。
    /// </summary>
    /// <remarks>
    /// 求解期调用。
    /// </remarks>
    internal void AddDependency(ResolvedNode node) => _dependencies.Add(node);

    /// <summary>
    /// 记录一条无法满足的可选依赖。
    /// </summary>
    /// <remarks>
    /// 求解期调用。
    /// </remarks>
    internal void AddMissingOptional(CapabilityRequirement requirement) => _missingOptional.Add(requirement);

    /// <summary>
    /// 标记本节点被选中提供某能力。
    /// </summary>
    /// <remarks>
    /// 求解期调用。
    /// </remarks>
    /// <param name="capabilityId">能力 ID。</param>
    /// <param name="requested">这一选择是否被要求（有清单依赖或配置 / 代码指定）。</param>
    internal void AddSelected(string capabilityId, bool requested)
    {
        _selected.Add(capabilityId);
        if (requested)
        {
            _requested.Add(capabilityId);
        }
    }

    /// <inheritdoc />
    public override string ToString() => Manifest.Id;
}
