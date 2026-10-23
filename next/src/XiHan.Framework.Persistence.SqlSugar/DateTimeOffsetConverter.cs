// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data;
using System.Globalization;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// <see cref="DateTimeOffset"/> 列的值转换：写入时取 UTC 时刻存成不带偏移的日期时间，读取时按 UTC 还原，保证时刻在各数据库上一致。
/// SqlSugar 默认把 <see cref="DateTimeOffset"/> 当本地时间往返，跨时区会改变时刻；由 <see cref="SqlSugarConventions"/> 登记到每个此类型的列。
/// </summary>
internal sealed class DateTimeOffsetConverter : ISugarDataConverter
{
    /// <inheritdoc />
    public SugarParameter ParameterConverter<T>(object value, int columnIndex)
    {
        // 参数名按列序号区分，SqlSugar 要求同一语句内唯一；空值按数据库空值写入
        object parameterValue = value is DateTimeOffset moment ? moment.UtcDateTime : DBNull.Value;
        return new SugarParameter($"@xihan_dto_{columnIndex}", parameterValue, System.Data.DbType.DateTime);
    }

    /// <inheritdoc />
    public T QueryConverter<T>(IDataRecord dataRecord, int dataRecordIndex)
    {
        ArgumentNullException.ThrowIfNull(dataRecord);
        if (dataRecord.IsDBNull(dataRecordIndex))
        {
            return default!;
        }

        // 不同驱动把日期时间列读成 DateTime、DateTimeOffset 或文本（SQLite），统一按 UTC 解释
        var utc = dataRecord.GetValue(dataRecordIndex) switch
        {
            DateTime dateTime => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            DateTimeOffset offset => offset.UtcDateTime,
            string text => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
            var other => DateTime.SpecifyKind(Convert.ToDateTime(other, CultureInfo.InvariantCulture), DateTimeKind.Utc),
        };
        return (T)(object)new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
