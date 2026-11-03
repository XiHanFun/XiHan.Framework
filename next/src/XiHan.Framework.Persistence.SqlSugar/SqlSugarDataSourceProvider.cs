// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using SqlSugar;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 按定义建 <see cref="SqlSugarDataSource"/> 的 Provider
/// </summary>
/// <remarks>
/// 数据库类型是 SqlSugar <see cref="DbType"/> 枚举的名字（不区分大小写；<see cref="DbType.Custom"/> 与数字不认）。建实例时由驱动解析连接串（只构造连接对象，不打开），格式错即以 <see cref="ArgumentException"/> 拒绝；探测打开原生 <see cref="DbConnection"/> 再关闭，驱动的异常消息脱敏后作为原因
/// </remarks>
internal sealed class SqlSugarDataSourceProvider : IDataSourceProvider
{
    /// <summary>
    /// Provider 名
    /// </summary>
    public const string ProviderName = "sqlsugar";

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public bool Supports(string databaseType) => TryParse(databaseType, out _);

    /// <inheritdoc />
    public ValueTask<IDataSource> CreateAsync(DataSourceDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParse(definition.DatabaseType, out var databaseType))
        {
            throw new ArgumentException($"Database type '{definition.DatabaseType}' is not a SqlSugar DbType name.", nameof(definition));
        }

        // 登记时就让驱动解析连接串：格式错在这里拒绝，不等第一次使用；取 Ado.Connection 只构造连接对象、不打开
        // SqlSugar 把驱动的解析异常包成自己的异常，原因取消息链并脱敏
        try
        {
            using var client = new SqlSugarClient(CreateConfig(definition, databaseType));
            _ = client.Ado.Connection;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ArgumentException(ConnectionStrings.Describe(exception, definition.ConnectionString), nameof(definition));
        }

        return ValueTask.FromResult<IDataSource>(new SqlSugarDataSource(definition, databaseType));
    }

    /// <inheritdoc />
    public async ValueTask<Result> ProbeAsync(IDataSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var sugar = source as SqlSugarDataSource ?? throw new ArgumentException($"Data source '{source.Name}' was not created by the SqlSugar provider.", nameof(source));
        try
        {
            // 另建客户端探测：不碰共享客户端所在调用链上的事务；打开原生连接拿到驱动自己的错误，SqlSugar 的 IsValidConnection 会吞掉原因
            using var client = sugar.CreateClient();
            var connection = client.Ado.Connection;
            if (connection is DbConnection db)
            {
                await db.OpenAsync(cancellationToken).ConfigureAwait(false);
                await db.CloseAsync().ConfigureAwait(false);
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                connection.Open();
                connection.Close();
            }

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error.Unavailable(PersistenceErrors.DataSourceUnavailable, PersistenceMessages.DataSourceUnavailable, MessageArguments.Of(("name", sugar.Name), ("reason", ConnectionStrings.Describe(exception, sugar.Definition.ConnectionString))));
        }
    }

    /// <summary>
    /// 按定义建连接配置（每次一份新的）
    /// </summary>
    /// <remarks>
    /// <c>ConfigId</c> 是数据源名；每次操作自动开关连接；不施加实体约定；PostgreSQL 不自动转小写、Oracle 不自动转大写——外部库里的标识符可能是加了引号的大小写混合名，改了大小写就查不到
    /// </remarks>
    internal static ConnectionConfig CreateConfig(DataSourceDefinition definition, DbType databaseType) => new()
    {
        ConfigId = definition.Name,
        DbType = databaseType,
        ConnectionString = definition.ConnectionString,
        IsAutoCloseConnection = true,
        MoreSettings = new ConnMoreSettings { PgSqlIsAutoToLower = false, PgSqlIsAutoToLowerCodeFirst = false, IsAutoToUpper = false },
    };

    /// <summary>
    /// 把数据库类型名解析为 <see cref="DbType"/>
    /// </summary>
    /// <remarks>
    /// 只认枚举名（字母，不区分大小写），不认数字与 <see cref="DbType.Custom"/>（它要应用另外提供驱动程序集）
    /// </remarks>
    internal static bool TryParse(string? databaseType, out DbType value)
    {
        value = default;
        return !string.IsNullOrWhiteSpace(databaseType)
            && databaseType.All(char.IsAsciiLetter)
            && Enum.TryParse(databaseType, ignoreCase: true, out value)
            && Enum.IsDefined(value)
            && value != DbType.Custom;
    }
}
