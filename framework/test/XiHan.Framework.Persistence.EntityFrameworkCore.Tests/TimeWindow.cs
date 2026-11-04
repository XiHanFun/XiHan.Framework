// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 复杂类型：一段时间窗口
/// </summary>
public sealed class TimeWindow
{
    /// <summary>
    /// 开始时刻
    /// </summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>
    /// 结束时刻：可以没有
    /// </summary>
    public DateTimeOffset? End { get; set; }
}
