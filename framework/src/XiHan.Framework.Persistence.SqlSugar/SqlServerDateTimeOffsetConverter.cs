// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// SQL Server 上 <see cref="DateTimeOffset"/> 列的值转换
/// </summary>
/// <remarks>
/// 与 <see cref="DateTimeOffsetConverter"/> 相同，只是写入参数按 <c>datetime2</c> 传：SqlClient 把 <see cref="System.Data.DbType.DateTime"/> 的参数当 <c>datetime</c>，写进列之前就只剩 1/300 秒
/// </remarks>
internal sealed class SqlServerDateTimeOffsetConverter : DateTimeOffsetConverter
{
    /// <inheritdoc />
    protected override System.Data.DbType ParameterType => System.Data.DbType.DateTime2;
}
