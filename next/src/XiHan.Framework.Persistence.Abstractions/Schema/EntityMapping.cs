// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Events;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 一个已登记实体的映射
/// </summary>
/// <remarks>
/// Provider 给出表名、是否分表与列映射，协议与索引在这里按实体类型推出：协议看实体实现的领域接口，索引是 <see cref="EntityIndexAttribute"/> 的声明经 <see cref="EntityIndexes.Resolve"/> 按表名推出的结果（与 Provider 建出的同名；分表实体与 Provider 自己的索引特性不在内）。表名与列名是映射声明的写法，分表是逻辑名
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EntityMapping
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="entity">登记的实体</param>
    /// <param name="table">Provider 映射出的表名（分表是逻辑名）</param>
    /// <param name="isSplit">是否按时间分表</param>
    /// <param name="columns">列映射</param>
    /// <exception cref="InvalidOperationException">索引声明不合法</exception>
    public EntityMapping(RegisteredEntity entity, string table, bool isSplit, IReadOnlyList<EntityColumnMapping> columns)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentNullException.ThrowIfNull(columns);
        EntityType = entity.EntityType;
        KeyType = entity.KeyType;
        Table = table;
        IsSplit = isSplit;
        Columns = [.. columns];
        Protocols = ProtocolsOf(entity.EntityType);
        // 分表实体不经 [EntityIndex] 建索引（SqlSugar 拒绝它，分表用自己的 [SugarIndex]），这里也不推出
        Indexes = isSplit ? [] : EntityIndexes.Resolve(entity.EntityType, table);
    }

    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// 标识类型
    /// </summary>
    public Type KeyType { get; }

    /// <summary>
    /// 表名（映射声明的写法，分表是逻辑名）
    /// </summary>
    public string Table { get; }

    /// <summary>
    /// 是否按时间分表
    /// </summary>
    public bool IsSplit { get; }

    /// <summary>
    /// 实现的领域协议
    /// </summary>
    public EntityProtocols Protocols { get; }

    /// <summary>
    /// 映射到列的属性（不映射的属性不在内）
    /// </summary>
    public IReadOnlyList<EntityColumnMapping> Columns { get; }

    /// <summary>
    /// 主键列
    /// </summary>
    public IEnumerable<EntityColumnMapping> KeyColumns => Columns.Where(static c => c.IsKey);

    /// <summary>
    /// <see cref="EntityIndexAttribute"/> 声明的索引，按名字排序
    /// </summary>
    public IReadOnlyList<EntityIndex> Indexes { get; }

    /// <summary>
    /// 实体类型名与表名
    /// </summary>
    public override string ToString() => $"EntityMapping {{ EntityType = {EntityType.Name}, Table = {Table} }}";

    /// <summary>
    /// 按实现的领域接口推出协议
    /// </summary>
    private static EntityProtocols ProtocolsOf(Type type)
    {
        var protocols = EntityProtocols.None;
        protocols |= typeof(IHasDomainEvents).IsAssignableFrom(type) ? EntityProtocols.AggregateRoot : EntityProtocols.None;
        protocols |= typeof(IHasCreationTime).IsAssignableFrom(type) ? EntityProtocols.CreationTime : EntityProtocols.None;
        protocols |= typeof(IHasModificationTime).IsAssignableFrom(type) ? EntityProtocols.ModificationTime : EntityProtocols.None;
        protocols |= typeof(IHasConcurrencyStamp).IsAssignableFrom(type) ? EntityProtocols.ConcurrencyStamp : EntityProtocols.None;
        protocols |= typeof(ISoftDeletable).IsAssignableFrom(type) ? EntityProtocols.SoftDeletable : EntityProtocols.None;
        protocols |= typeof(IMultiTenant).IsAssignableFrom(type) ? EntityProtocols.MultiTenant : EntityProtocols.None;
        return protocols;
    }
}
