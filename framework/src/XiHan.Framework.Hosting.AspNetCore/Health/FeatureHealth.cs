// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Health;

/// <summary>
/// 探针端点中的单个特性状态
/// </summary>
/// <remarks>
/// 成员都经初始化器给出，以后加字段只加属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record FeatureHealth
{
    /// <summary>
    /// 清单 ID 或健康贡献条目名
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 生命周期状态
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// 失败原因的错误码
    /// </summary>
    /// <remarks>
    /// 没有失败为 <see langword="null"/>
    /// </remarks>
    public string? Error { get; init; }

    /// <summary>
    /// 条目检查的是什么
    /// </summary>
    /// <remarks>
    /// 只有健康贡献条目有（ADR-0054）
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// 检查时刻
    /// </summary>
    public DateTimeOffset? CheckedAt { get; init; }

    /// <summary>
    /// 检查耗时（毫秒）
    /// </summary>
    public double? DurationMs { get; init; }

    /// <summary>
    /// 条目的数据（名字 → 标量）
    /// </summary>
    /// <remarks>
    /// 没有数据时不写
    /// </remarks>
    public IReadOnlyDictionary<string, object?>? Data { get; init; }
}
