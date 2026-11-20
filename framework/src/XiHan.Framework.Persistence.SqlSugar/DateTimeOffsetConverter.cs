// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data;
using System.Globalization;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// <see cref="DateTimeOffset"/> 列的值转换
/// </summary>
/// <remarks>
/// 写入时取 UTC 时刻存成不带偏移的日期时间，读取时按 UTC 还原，保证时刻在各数据库上一致。SqlSugar 默认把 <see cref="DateTimeOffset"/> 当本地时间往返，跨时区会改变时刻；由 <see cref="SqlSugarConventions"/> 登记到每个此类型的列
/// </remarks>
internal class DateTimeOffsetConverter : ISugarDataConverter
{
    /// <summary>
    /// 写入参数的类型
    /// </summary>
    protected virtual System.Data.DbType ParameterType => System.Data.DbType.DateTime;

    /// <inheritdoc />
    /// <remarks>
    /// 条件右边是嵌套闭包里的变量（如方法参数与循环变量各在一层）时，SqlSugar 先转换一次取出值，按列追加参数时再转换一次，第二次收到的是上一次写出的 UTC <see cref="DateTime"/>；
    /// <see cref="DateTime"/> 按 C# 隐式转换成 <see cref="DateTimeOffset"/> 的规则解释（Utc 原样，Local 与 Unspecified 按本地时间），所以重复转换不改变时刻。
    /// 别的类型直接抛出，不当成空值写入：与 NULL 比较的条件什么都查不到
    /// </remarks>
    public SugarParameter ParameterConverter<T>(object value, int columnIndex)
    {
        // 参数名按列序号区分，SqlSugar 要求同一语句内唯一；空值按数据库空值写入
        object parameterValue = value switch
        {
            DateTimeOffset moment => moment.UtcDateTime,
            DateTime dateTime => new DateTimeOffset(dateTime).UtcDateTime,
            null or DBNull => DBNull.Value,
            _ => throw new NotSupportedException($"A value of type '{value.GetType().FullName}' cannot be written to a DateTimeOffset column."),
        };
        return new SugarParameter($"@xihan_dto_{columnIndex}", parameterValue, ParameterType);
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
        // DateTime 要看 Kind：Npgsql 的 legacy 时间戳模式（SqlSugar 的 PostgreSQL 提供器自己打开）把 timestamptz 换算成本地时间、Kind=Local 交出，
        // 直接当 UTC 会多出本地时区偏移，而且经实体回写后逐次累加；Local 先换回 UTC，Unspecified 按写入时的 UTC 解释
        var utc = dataRecord.GetValue(dataRecordIndex) switch
        {
            DateTime { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
            DateTime dateTime => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            DateTimeOffset offset => offset.UtcDateTime,
            string text => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
            var other => DateTime.SpecifyKind(Convert.ToDateTime(other, CultureInfo.InvariantCulture), DateTimeKind.Utc),
        };
        return (T)(object)new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
