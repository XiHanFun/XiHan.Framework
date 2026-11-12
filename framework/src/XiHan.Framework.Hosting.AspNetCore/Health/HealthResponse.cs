// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Health;

/// <summary>
/// 探针与健康详情端点的响应体
/// </summary>
/// <remarks>
/// 成员都经初始化器给出，以后加字段只加属性（ADR-0142）。探针只给 <see cref="Status"/> 与 <see cref="State"/>；<see cref="Features"/> 与 <see cref="Contributions"/> 只由能加访问要求的详情端点给出（ADR-0209）
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
    /// <remarks>
    /// 探针不给（<see langword="null"/>，不写出）
    /// </remarks>
    public IReadOnlyList<FeatureHealth>? Features { get; init; }

    /// <summary>
    /// 各健康贡献条目（如插件）的状态
    /// </summary>
    /// <remarks>
    /// 探针不给（<see langword="null"/>，不写出）
    /// </remarks>
    public IReadOnlyList<FeatureHealth>? Contributions { get; init; }
}
