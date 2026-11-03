// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 从 Oracle 数据字典读一张表的列
/// </summary>
/// <remarks>
/// SqlSugar 的 Oracle 读列取 <c>GetSchemaTable</c>：类型名是 CLR 类型（<c>Decimal</c>、<c>String</c>）、读不到 identity、缺省值错位，大小写混合的表名还会被转成大写而查不到。这里改查连接用户自己的 <c>USER_TAB_COLUMNS</c>、<c>USER_COL_COMMENTS</c>、<c>USER_CONSTRAINTS</c> / <c>USER_CONS_COLUMNS</c>，表名按库里的原样绑定。<c>IDENTITY_COLUMN</c> 要 Oracle 12c 及以上，更早的版本查询失败、按读取失败报告。结果仍是 SqlSugar 的 <see cref="DbColumnInfo"/>，长度与精度的取舍与别的数据库走同一套映射
/// </remarks>
internal static partial class OracleColumns
{
    /// <summary>
    /// 列与注释，按列序
    /// </summary>
    private const string ColumnsSql = """
        SELECT c.COLUMN_NAME, c.DATA_TYPE, c.DATA_LENGTH, c.CHAR_LENGTH, c.DATA_PRECISION, c.DATA_SCALE, c.NULLABLE, c.IDENTITY_COLUMN, c.DATA_DEFAULT, cc.COMMENTS
        FROM USER_TAB_COLUMNS c
        LEFT JOIN USER_COL_COMMENTS cc ON cc.TABLE_NAME = c.TABLE_NAME AND cc.COLUMN_NAME = c.COLUMN_NAME
        WHERE c.TABLE_NAME = :t
        ORDER BY c.COLUMN_ID
        """;

    /// <summary>
    /// 主键列
    /// </summary>
    private const string KeysSql = """
        SELECT cols.COLUMN_NAME
        FROM USER_CONSTRAINTS cons
        JOIN USER_CONS_COLUMNS cols ON cols.CONSTRAINT_NAME = cons.CONSTRAINT_NAME AND cols.OWNER = cons.OWNER
        WHERE cons.CONSTRAINT_TYPE = 'P' AND cons.TABLE_NAME = :t
        """;

    /// <summary>
    /// 按字符计长度的类型
    /// </summary>
    /// <remarks>
    /// 长度取 <c>CHAR_LENGTH</c>：<c>NVARCHAR2(30)</c> 是 30 个字符、60 个字节
    /// </remarks>
    private static readonly HashSet<string> _characterTypes = new(StringComparer.OrdinalIgnoreCase) { "CHAR", "NCHAR", "VARCHAR", "VARCHAR2", "NVARCHAR2" };

    /// <summary>
    /// 读列
    /// </summary>
    /// <param name="client">连到该库的客户端</param>
    /// <param name="table">物理表名，库里的原样（不加引号的名字是大写）</param>
    public static List<DbColumnInfo> Read(ISqlSugarClient client, string table)
    {
        var parameter = new SugarParameter(":t", table);
        var keys = client.Ado.GetDataTable(KeysSql, parameter).Rows.Cast<DataRow>().Select(static row => (string)row["COLUMN_NAME"]).ToHashSet(StringComparer.Ordinal);
        var columns = new List<DbColumnInfo>();
        foreach (DataRow row in client.Ado.GetDataTable(ColumnsSql, new SugarParameter(":t", table)).Rows)
        {
            var name = (string)row["COLUMN_NAME"];
            // 类型名去掉括号里的位数（TIMESTAMP(6) → TIMESTAMP，INTERVAL DAY(2) TO SECOND(6) → INTERVAL DAY TO SECOND），与别的数据库一样不含长度与精度
            var dataType = Digits().Replace((string)row["DATA_TYPE"], string.Empty);
            var precision = Integer(row["DATA_PRECISION"]);
            columns.Add(new DbColumnInfo
            {
                TableName = table,
                DbColumnName = name,
                DataType = dataType,
                Length = LengthOf(row, dataType, precision),
                DecimalDigits = precision is null ? 0 : Integer(row["DATA_SCALE"]) ?? 0,
                IsNullable = string.Equals(row["NULLABLE"] as string, "Y", StringComparison.Ordinal),
                IsPrimarykey = keys.Contains(name),
                IsIdentity = string.Equals(row["IDENTITY_COLUMN"] as string, "YES", StringComparison.Ordinal),
                // 缺省值是数据库保存的原文，Oracle 会在末尾留空格
                DefaultValue = (row["DATA_DEFAULT"] as string)?.Trim(),
                ColumnDescription = row["COMMENTS"] as string,
            });
        }

        return columns;
    }

    /// <summary>
    /// 交给映射的长度位
    /// </summary>
    /// <remarks>
    /// 映射对定点数族把长度位当精度、对字符与二进制类型当长度、其余丢掉，所以这里按类型给：字符类型按字符数（<c>CHAR_LENGTH</c>）；有精度的（<c>NUMBER(p,s)</c>）给精度；不带精度的 <c>NUMBER</c> 给 0（<c>DATA_LENGTH</c> 的 22 是存储字节数，当成精度就错了）；其余给字节长度（<c>RAW(16)</c> 是 16，<c>CLOB</c>、<c>DATE</c> 由映射丢掉）
    /// </remarks>
    private static int LengthOf(DataRow row, string dataType, int? precision)
    {
        if (_characterTypes.Contains(dataType))
        {
            return Integer(row["CHAR_LENGTH"]) ?? 0;
        }

        if (precision is { } digits)
        {
            return digits;
        }

        return string.Equals(dataType, "NUMBER", StringComparison.OrdinalIgnoreCase) ? 0 : Integer(row["DATA_LENGTH"]) ?? 0;
    }

    /// <summary>
    /// 数字列的值
    /// </summary>
    /// <remarks>
    /// 空为 <see langword="null"/>
    /// </remarks>
    private static int? Integer(object value) => value is DBNull or null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);

    /// <summary>
    /// 类型名里括号括起来的位数
    /// </summary>
    [GeneratedRegex(@"\(\d+\)")]
    private static partial Regex Digits();
}
