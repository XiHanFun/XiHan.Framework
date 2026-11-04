// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// SqlSugar 的运行时数据源
/// </summary>
/// <remarks>
/// 由 <see cref="DataSourceRegistry"/> 经 SqlSugar 的数据源 Provider 建出并持有，释放归注册表。<see cref="Client"/> 是这个数据源自己的 <see cref="SqlSugarScope"/>：线程安全，每次操作自动开关连接（<c>IsAutoCloseConnection</c>），连接由驱动的连接池复用。外部库不是本应用的实体：不施加 <see cref="SqlSugarConventions"/>、不挂过滤器与 AOP、不进工作单元（不参与本地事务，要事务自己经 <c>Client.Ado</c> 开）；PostgreSQL 上不自动转小写，标识符按库里的原样引用
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SqlSugarDataSource : IDataSource, IDisposable
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="definition">定义</param>
    /// <param name="databaseType">解析出的 SqlSugar 数据库类型</param>
    internal SqlSugarDataSource(DataSourceDefinition definition, DbType databaseType)
    {
        Definition = definition;
        SugarType = databaseType;
        Client = new SqlSugarScope(SqlSugarDataSourceProvider.CreateConfig(definition, databaseType));
    }

    /// <inheritdoc />
    public string Name => Definition.Name;

    /// <inheritdoc />
    public string DatabaseType => Definition.DatabaseType;

    /// <summary>
    /// 这个数据源的客户端（<see cref="SqlSugarScope"/>，可在多个调用间共享）
    /// </summary>
    /// <remarks>
    /// 数据源被移除或替换后它会被释放，不要在注册表之外长期持有它：每次使用前经 <see cref="DataSourceRegistry.GetOrAddAsync"/> 以记录里当前的定义取
    /// </remarks>
    public ISqlSugarClient Client { get; }

    /// <summary>
    /// 定义：脱敏要用到连接串
    /// </summary>
    internal DataSourceDefinition Definition { get; }

    /// <summary>
    /// 解析出的 SqlSugar 数据库类型
    /// </summary>
    internal DbType SugarType { get; }

    /// <summary>
    /// 另建一个不共享的客户端（调用方释放）
    /// </summary>
    /// <remarks>
    /// 探测与读库结构用它：每次一份新的连接配置，不碰 <see cref="Client"/> 所在调用链上可能开着的事务
    /// </remarks>
    internal SqlSugarClient CreateClient() => new(SqlSugarDataSourceProvider.CreateConfig(Definition, SugarType));

    /// <inheritdoc />
    public void Dispose() => Client.Dispose();

    /// <summary>
    /// 名字与数据库类型，不含连接串
    /// </summary>
    public override string ToString() => $"SqlSugarDataSource {{ Name = {Name}, DatabaseType = {DatabaseType} }}";
}
