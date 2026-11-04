// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 关系库结构的读取：表、列、注释，分表折叠成逻辑表
/// </summary>
/// <remarks>
/// <para>数据源：<see langword="null"/> 是应用的缺省连接（错误里称作 <see cref="DataSourceDefinition.DefaultName"/>），读取另开一个不在事务里的连接；否则是运行时数据源的定义，读取前经 <see cref="DataSourceRegistry.GetOrAddAsync"/> 登记（同定义复用、变了替换），读到的总是记录里当前的连接</para>
/// <para>失败：连接或读取失败 <see cref="PersistenceErrors.DataSourceUnavailable"/>（参数 <c>name</c>、<c>reason</c>，已脱敏）；运行时数据源的定义不合法、登记已满按 <see cref="DataSourceRegistry"/> 的错误；Provider 不支持 <see cref="PersistenceErrors.NotSupported"/>（EF Core 与内存实现），不返回空结果冒充</para>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface ISchemaReader
{
    /// <summary>
    /// 列出表（不含列）
    /// </summary>
    /// <remarks>
    /// 按名字排序（不区分大小写）；<see cref="TableListQuery.Keyword"/> 在名字与注释里不区分大小写地包含匹配；按 <see cref="TableListQuery.Skip"/> / <see cref="TableListQuery.Take"/> 分页，<see cref="TablePage.Total"/> 是过滤后的总数。库里的表一次读出、在内存里过滤与分页。先经 <see cref="TableListQuery.Validate"/> 核对分页，越界返回 <see cref="PersistenceErrors.ListPageInvalid"/> 或 <see cref="PersistenceErrors.TakeExceeded"/>，不连库、不登记数据源；不读结构的 Provider 返回 <see cref="PersistenceErrors.NotSupported"/>
    /// </remarks>
    /// <param name="dataSource">运行时数据源；<see langword="null"/> 为应用的缺省连接</param>
    /// <param name="query">过滤与分页</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Result<TablePage>> ListTablesAsync(DataSourceDefinition? dataSource, TableListQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 按表名批量读结构（含列）
    /// </summary>
    /// <remarks>
    /// 外层结果是整批的：一次最多 <see cref="SchemaLimits.MaxTablesPerRead"/> 张，超过返回 <see cref="PersistenceErrors.TakeExceeded"/>（参数 <c>count</c>、<c>max</c>）；连接或读取失败整批失败。成功时逐项结果与入参一一对应：名字先按精确匹配、再按不区分大小写的唯一匹配找（分表用逻辑名），找不到该项 <see cref="PersistenceErrors.TableNotFound"/>；重复的名字各自返回同一结构。空集合不连库，返回空列表。每张表读列之前检查取消
    /// </remarks>
    /// <param name="dataSource">运行时数据源；<see langword="null"/> 为应用的缺省连接</param>
    /// <param name="tables">表名（<see cref="ListTablesAsync"/> 返回的名字）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Result<IReadOnlyList<Result<TableSchema>>>> GetTablesAsync(DataSourceDefinition? dataSource, IReadOnlyList<string> tables, CancellationToken cancellationToken);
}
