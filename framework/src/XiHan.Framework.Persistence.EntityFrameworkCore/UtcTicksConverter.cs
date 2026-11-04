// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// <see cref="DateTimeOffset"/> 与 UTC ticks 整数之间的值转换
/// </summary>
/// <remarks>
/// SQLite 上由 <see cref="ModelBuilderExtensions.ApplyPersistenceConventions"/> 施加
/// 写入取 <see cref="DateTimeOffset.UtcTicks"/>（100 纳秒精度，与 SqlSugar 在 SQLite 上存的 UTC 日期时间文本同精度），读取还原为偏移为零的同一时刻（与 SqlSugar 的 UTC 往返一致）。UTC ticks 随时刻单调，不论写入时的偏移是多少，整数的比较与排序就是时刻的比较与排序
/// </remarks>
internal sealed class UtcTicksConverter : ValueConverter<DateTimeOffset, long>
{
    /// <summary>
    /// 创建
    /// </summary>
    private UtcTicksConverter()
        : base(value => value.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero))
    {
    }

    /// <summary>
    /// 共享实例：转换器无状态
    /// </summary>
    public static UtcTicksConverter Instance { get; } = new();
}
