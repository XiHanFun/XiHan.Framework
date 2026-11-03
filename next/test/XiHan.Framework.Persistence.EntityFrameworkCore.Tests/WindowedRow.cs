// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 把时刻放在复杂类型里的实体
/// </summary>
public sealed class WindowedRow : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 时间窗口（复杂类型，列在本表里）
    /// </summary>
    public TimeWindow Window { get; set; } = new();
}
