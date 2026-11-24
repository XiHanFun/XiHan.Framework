// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing;

/// <summary>
/// <see cref="MetricCapture"/> 收到的一个测量值
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record MetricCaptureEntry
{
    /// <summary>
    /// 仪表所属的 Meter 的名字
    /// </summary>
    public required string Meter { get; init; }

    /// <summary>
    /// 仪表的名字
    /// </summary>
    public required string Instrument { get; init; }

    /// <summary>
    /// 测量值
    /// </summary>
    /// <remarks>
    /// 各种数值类型的仪表都换成 <see cref="double"/>：整数在 2^53 以内不失真
    /// </remarks>
    public required double Value { get; init; }

    /// <summary>
    /// 测量时带的标签
    /// </summary>
    /// <remarks>
    /// 同一个键给了几次时取最后一次
    /// </remarks>
    public required IReadOnlyDictionary<string, object?> Tags { get; init; }
}
