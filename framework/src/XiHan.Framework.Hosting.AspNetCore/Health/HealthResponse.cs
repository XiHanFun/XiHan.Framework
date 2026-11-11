// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Health;

/// <summary>
/// 探针端点的响应体
/// </summary>
/// <remarks>
/// 成员都经初始化器给出，以后加字段只加属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record HealthResponse
{
    /// <summary>
    /// <c>pass</c> 或 <c>fail</c>
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// 应用聚合状态
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// 各特性状态
    /// </summary>
    public required IReadOnlyList<FeatureHealth> Features { get; init; }

    /// <summary>
    /// 各健康贡献条目（如插件）的状态
    /// </summary>
    public required IReadOnlyList<FeatureHealth> Contributions { get; init; }
}
