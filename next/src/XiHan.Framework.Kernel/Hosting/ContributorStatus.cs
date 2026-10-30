// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 一个健康贡献条目。
/// </summary>
/// <param name="Name">条目名（如 <c>plugins.xihan.samples.echo</c>）。</param>
/// <param name="State">条目状态；只有 <see cref="LifecycleState.Degraded"/> 与 <see cref="LifecycleState.Failed"/> 影响应用聚合状态。</param>
/// <param name="Error">Degraded / Failed 的原因；可空。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ContributorStatus(string Name, LifecycleState State, Error? Error = null)
{
    /// <summary>
    /// 条目名。
    /// </summary>
    public string Name { get; } = !string.IsNullOrWhiteSpace(Name) ? Name : throw new ArgumentException("The contributor name must not be empty.", nameof(Name));

    /// <summary>
    /// 条目检查的是什么。
    /// </summary>
    /// <remarks>
    /// 给运维看的说明；没有为 <see langword="null"/>（ADR-0054）。
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// 这次状态的检查时刻。
    /// </summary>
    /// <remarks>
    /// 周期探测的条目是最近一次探测完成的时刻，同步计算的条目是计算的时刻；没有为 <see langword="null"/>。
    /// </remarks>
    public DateTimeOffset? CheckedAt { get; init; }

    /// <summary>
    /// 检查耗时。
    /// </summary>
    /// <remarks>
    /// 没有为 <see langword="null"/>。
    /// </remarks>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// 条目的数据。
    /// </summary>
    /// <remarks>
    /// 名字 → 标量（字符串、布尔、整数、浮点数或 <see langword="null"/>），如容量类条目的已用量与上限；不是标量时抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Data
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var (key, item) in value)
            {
                if (string.IsNullOrWhiteSpace(key) || item is not (null or string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal))
                {
                    throw new ArgumentException($"Health data '{key}' must have a non-empty name and a string, boolean, numeric or null value.", nameof(value));
                }
            }

            field = new Dictionary<string, object?>(value, StringComparer.Ordinal);
        }
    } = new Dictionary<string, object?>(StringComparer.Ordinal);
}
