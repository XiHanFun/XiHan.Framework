// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Health;

/// <summary>
/// 探针端点中的单个特性状态。
/// </summary>
/// <param name="Id">清单 ID。</param>
/// <param name="State">生命周期状态。</param>
/// <param name="Error">失败原因的错误码。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record FeatureHealth(string Id, string State, string? Error)
{
    /// <summary>
    /// 条目检查的是什么。
    /// </summary>
    /// <remarks>
    /// 只有健康贡献条目有（ADR-0054）。
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// 检查时刻。
    /// </summary>
    public DateTimeOffset? CheckedAt { get; init; }

    /// <summary>
    /// 检查耗时（毫秒）。
    /// </summary>
    public double? DurationMs { get; init; }

    /// <summary>
    /// 条目的数据（名字 → 标量）。
    /// </summary>
    /// <remarks>
    /// 没有数据时不写。
    /// </remarks>
    public IReadOnlyDictionary<string, object?>? Data { get; init; }
}
