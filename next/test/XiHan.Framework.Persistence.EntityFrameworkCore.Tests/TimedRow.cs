// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 带时刻列的实体：一个必填、一个可空。
/// </summary>
public sealed class TimedRow : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 发生时刻。
    /// </summary>
    public DateTimeOffset At { get; set; }

    /// <summary>
    /// 到期时刻：可以没有。
    /// </summary>
    public DateTimeOffset? Due { get; set; }
}
