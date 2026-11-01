// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 解析后的索引：名字、列、唯一性与是否只约束未删除的行。
/// </summary>
/// <remarks>
/// 由 <see cref="EntityIndexes.Resolve"/> 从 <see cref="EntityIndexAttribute"/> 推出，Provider 原样建索引。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EntityIndex
{
    /// <summary>
    /// 由解析过程创建。
    /// </summary>
    internal EntityIndex(string name, IReadOnlyList<string> properties, bool isUnique, bool excludesDeleted)
    {
        Name = name;
        Properties = properties;
        IsUnique = isUnique;
        ExcludesDeleted = excludesDeleted;
    }

    /// <summary>
    /// 索引名。
    /// </summary>
    /// <remarks>
    /// 不超过 <see cref="EntityIndexes.MaxNameLength"/> 个字符。
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// 列（属性名），按声明顺序。
    /// </summary>
    /// <remarks>
    /// 就是声明的列：部分唯一索引的删除标记在条件里，不在列里。
    /// </remarks>
    public IReadOnlyList<string> Properties { get; }

    /// <summary>
    /// 是否唯一。
    /// </summary>
    public bool IsUnique { get; }

    /// <summary>
    /// 是否只约束未删除的行（部分唯一索引）。
    /// </summary>
    /// <remarks>
    /// 软删除实体（<c>ISoftDeletable</c>）的唯一索引、声明的列里没有删除标记时为真：Provider 建成带条件 <c>IsDeleted = false</c> 的唯一索引（PostgreSQL、SQLite 的 <c>WHERE</c>，SQL Server 的筛选索引），已删除的行不受约束，同一业务键删除、重建、再删除任意多次都不冲突。数据库不支持部分索引时 Provider 在启动时失败（<see cref="PersistenceErrors.NotSupported"/>），不退化成普通唯一索引。
    /// </remarks>
    public bool ExcludesDeleted { get; }
}
