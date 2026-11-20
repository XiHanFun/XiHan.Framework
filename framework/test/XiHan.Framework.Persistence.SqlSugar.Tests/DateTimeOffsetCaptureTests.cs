// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 按时刻比较的条件里，嵌套闭包捕获的变量照样作为参数写入
/// </summary>
/// <remarks>
/// 方法参数与循环变量各在一层闭包时，表达式里的时刻是成员的成员；SqlSugar 对它转换两次，第二次收到第一次写出的 UTC 时间，以前被写成 NULL、什么都查不到
/// </remarks>
public sealed class DateTimeOffsetCaptureTests
{
    private static readonly DateTimeOffset _moment = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Comparison_WithANestedCapturedInstant_FindsTheRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"xihan-capture-{Guid.NewGuid():N}.db");
        try
        {
            var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = $"Data Source={path}", IsAutoCloseConnection = true }));
            SqlSugarConventions.InitTables(client, typeof(TimestampRecord));
            var (early, late) = (Guid.NewGuid(), Guid.NewGuid());
            client.Insertable(new List<TimestampRecord>
            {
                new() { Id = early, At = _moment.AddDays(-1), Due = _moment.AddDays(-1), Local = _moment.UtcDateTime },
                new() { Id = late, At = _moment.AddDays(1), Due = _moment.AddDays(1), Local = _moment.UtcDateTime },
            }).ExecuteCommand();

            Assert.Equal([early], Before(client, _moment.ToOffset(TimeSpan.FromHours(8)), [early, late]));
            Assert.Equal([late], DueAfter(client, _moment, [early, late]));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    /// <summary>
    /// 逐个查时刻早于 <paramref name="cutoff"/> 的行
    /// </summary>
    /// <remarks>
    /// <paramref name="cutoff"/> 在方法这一层闭包，循环变量在里面一层：条件里的 <paramref name="cutoff"/> 是嵌套的成员访问
    /// </remarks>
    private static List<Guid> Before(SqlSugarClient client, DateTimeOffset cutoff, Guid[] ids)
    {
        var found = new List<Guid>();
        foreach (var id in ids)
        {
            if (client.Queryable<TimestampRecord>().Where(r => r.Id == id && r.At < cutoff).Any())
            {
                found.Add(id);
            }
        }

        return found;
    }

    /// <summary>
    /// 逐个查可空时刻晚于 <paramref name="cutoff"/> 的行
    /// </summary>
    private static List<Guid> DueAfter(SqlSugarClient client, DateTimeOffset cutoff, Guid[] ids)
    {
        var found = new List<Guid>();
        foreach (var id in ids)
        {
            if (client.Queryable<TimestampRecord>().Where(r => r.Id == id && r.Due >= cutoff).Any())
            {
                found.Add(id);
            }
        }

        return found;
    }
}
