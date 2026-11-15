// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 带各种日期时间列的实体
/// </summary>
internal sealed class TimestampRecord : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>
    /// 时刻
    /// </summary>
    public DateTimeOffset At { get; set; }

    /// <summary>
    /// 可空的时刻
    /// </summary>
    public DateTimeOffset? Due { get; set; }

    /// <summary>
    /// 墙钟时间
    /// </summary>
    public DateTime Local { get; set; }
}
