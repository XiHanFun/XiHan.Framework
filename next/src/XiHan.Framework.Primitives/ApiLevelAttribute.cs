// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 标记公开类型或成员的稳定性级别与引入版本。所有公开类型都必须标注，由分析器强制。
/// </summary>
/// <param name="stability">稳定性级别。</param>
/// <param name="since">首次引入的框架版本，例如 <c>5.0</c>。</param>
[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ApiLevelAttribute(Stability stability, string since) : Attribute
{
    /// <summary>
    /// 稳定性级别。
    /// </summary>
    public Stability Stability { get; } = stability;

    /// <summary>
    /// 首次引入的框架版本。
    /// </summary>
    public string Since { get; } = since ?? throw new ArgumentNullException(nameof(since));

    /// <summary>
    /// 计划转为 <see cref="Stability.Stable"/> 的版本；仅 <see cref="Stability.Preview"/> 适用。
    /// </summary>
    public string? ScheduledStable { get; init; }
}
