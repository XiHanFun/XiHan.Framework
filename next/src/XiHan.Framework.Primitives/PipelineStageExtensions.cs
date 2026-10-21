// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// <see cref="PipelineStage"/> 与清单中小写阶段名的互转。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class PipelineStageExtensions
{
    private static readonly string[] _names =
    [
        "ingress", "observe", "resilience", "authenticate", "tenant", "authorize", "validate", "idempotency", "transaction", "handle", "egress",
    ];

    /// <summary>
    /// 全部阶段，按执行顺序。
    /// </summary>
    public static IReadOnlyList<PipelineStage> All { get; } =
        [PipelineStage.Ingress, PipelineStage.Observe, PipelineStage.Resilience, PipelineStage.Authenticate, PipelineStage.Tenant, PipelineStage.Authorize, PipelineStage.Validate, PipelineStage.Idempotency, PipelineStage.Transaction, PipelineStage.Handle, PipelineStage.Egress];

    /// <summary>
    /// 清单中的小写阶段名。
    /// </summary>
    public static string ToManifestName(this PipelineStage stage)
    {
        var index = (int)stage;
        return index >= 0 && index < _names.Length
            ? _names[index]
            : throw new ArgumentOutOfRangeException(nameof(stage), stage, "未知的管道阶段。");
    }

    /// <summary>
    /// 从清单中的小写阶段名解析。
    /// </summary>
    public static bool TryParse(string? name, out PipelineStage stage)
    {
        var index = name is null ? -1 : Array.IndexOf(_names, name);
        stage = index >= 0 ? (PipelineStage)index : default;
        return index >= 0;
    }
}
