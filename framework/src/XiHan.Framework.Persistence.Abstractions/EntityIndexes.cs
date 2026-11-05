// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 把实体上的 <see cref="EntityIndexAttribute"/> 解析成 Provider 要建的索引
/// </summary>
/// <remarks>
/// <para>Provider 共用：SqlSugar 与 EF Core 都经它取得索引，名字与列在两边一致</para>
/// <para>命名：<c>IX_</c>（唯一为 <c>UX_</c>）+ 表名 + 每列一段缩写，以下划线连接，如 <c>UX_MessageTranslation_Ke_Lo</c>。缩写取属性名每个词的前两个字符（<c>TenantId → TeId</c>、<c>NextFireAt → NeFiAt</c>）；词从首字符、小写或数字之后的大写字母、连续大写里后跟小写的那个大写字母开始，下划线只分词不计入。表名里字母、数字、下划线以外的字符（如架构名的点）换成下划线。全名不超过 <see cref="MaxNameLength"/>（PostgreSQL 的标识符上限；MySQL 64、SQL Server 128 都容得下），超过不截断、直接报错——PostgreSQL 会静默截断超长的名字，下一次建表按全名查不到已有索引，再建就撞名</para>
/// <para>软删除：实体实现 <see cref="ISoftDeletable"/> 时，唯一索引是只约束未删除行的部分唯一索引（<see cref="EntityIndex.ExcludesDeleted"/>）：列就是声明的列，条件 <c>IsDeleted = false</c> 由 Provider 按数据库方言写出；未删除的行之间唯一，已删除的行不受约束，同一业务键删除、重建、再删除任意多次都不冲突。声明的列里显式写了 <c>IsDeleted</c> 时不加条件，是普通唯一索引（同一业务键最多一行已删除）——不支持部分索引的数据库上要唯一约束，只能这样显式选择。只有 PostgreSQL、SQLite、SQL Server 支持部分唯一索引，其余数据库上 Provider 在启动时失败（<see cref="PersistenceErrors.NotSupported"/>）</para>
/// <para>写入撞上唯一索引：关系型 Provider 在写入失败后核对，按 <see cref="PersistenceErrors.UniqueKeyConflict"/> 报告，工作单元随后只能回滚</para>
/// <para>声明错误在 Provider 建模或建表时以 <see cref="InvalidOperationException"/> 报出：没有列、列名空白或重复、列不是公开实例属性、名字超长、同一实体两个索引同名或同列</para>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class EntityIndexes
{
    /// <summary>
    /// 索引名的最大长度：63
    /// </summary>
    public const int MaxNameLength = 63;

    /// <summary>
    /// 实体声明的全部索引，按名字排序
    /// </summary>
    /// <remarks>
    /// 没有声明返回空列表。<paramref name="table"/> 是 Provider 映射出的表名，参与索引名
    /// </remarks>
    /// <exception cref="InvalidOperationException">声明不合法</exception>
    public static IReadOnlyList<EntityIndex> Resolve([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type entityType, string table)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        var declared = entityType.GetCustomAttributes<EntityIndexAttribute>(inherit: true).ToList();
        if (declared.Count == 0)
        {
            return [];
        }

        var softDeletable = typeof(ISoftDeletable).IsAssignableFrom(entityType);
        var resolved = new List<EntityIndex>(declared.Count);
        // 名字按不区分大小写去重：PostgreSQL 把不加引号的索引名折成小写，只差大小写的两个名字在那里是同一个
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var columnSets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in declared)
        {
            Validate(entityType, declaration.Properties);
            var name = NameOf(table, declaration.Properties, declaration.IsUnique);
            if (name.Length > MaxNameLength)
            {
                throw new InvalidOperationException($"Index '{name}' of entity '{entityType.FullName}' is longer than {MaxNameLength} characters; shorten the table name or index fewer columns.");
            }

            // 软删除实体的唯一索引只约束未删除的行：删除标记放进条件而不是列，已删除的行不占业务键，同一键可以反复删除
            // 声明里显式写了删除标记的，是应用选择的普通唯一索引（同一键最多一行已删除），不加条件
            var excludesDeleted = declaration.IsUnique && softDeletable && !declaration.Properties.Contains(nameof(ISoftDeletable.IsDeleted), StringComparer.Ordinal);
            List<string> properties = [.. declaration.Properties];
            if (!names.Add(name))
            {
                throw new InvalidOperationException($"Entity '{entityType.FullName}' declares more than one index named '{name}'.");
            }

            // 同一组列只建一个索引：EF Core 按列合并同列的 HasIndex，SqlSugar 却会建两个，两边会不一致（部分唯一索引与同列的普通索引也算同列）
            if (!columnSets.Add(string.Join('\0', properties)))
            {
                throw new InvalidOperationException($"Entity '{entityType.FullName}' declares more than one index on ({string.Join(", ", properties.Select(static p => $"'{p}'"))}).");
            }

            resolved.Add(new EntityIndex(name, properties, declaration.IsUnique, excludesDeleted));
        }

        resolved.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        return resolved;
    }

    /// <summary>
    /// 按命名规则推出索引名
    /// </summary>
    /// <remarks>
    /// 前缀、表名（非法字符换成下划线）、各列缩写，以下划线连接；不检查长度
    /// </remarks>
    internal static string NameOf(string table, IReadOnlyList<string> properties, bool isUnique)
    {
        var builder = new StringBuilder(isUnique ? "UX_" : "IX_");
        foreach (var c in table)
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_');
        }

        foreach (var property in properties)
        {
            builder.Append('_');
            AppendAbbreviation(builder, property);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 校验一个声明的列
    /// </summary>
    private static void Validate([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type entityType, IReadOnlyList<string> properties)
    {
        if (properties.Count == 0)
        {
            throw new InvalidOperationException($"Entity '{entityType.FullName}' declares an index without properties.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            if (string.IsNullOrWhiteSpace(property))
            {
                throw new InvalidOperationException($"Entity '{entityType.FullName}' declares an index with a blank property name.");
            }

            if (!seen.Add(property))
            {
                throw new InvalidOperationException($"Entity '{entityType.FullName}' declares an index that repeats property '{property}'.");
            }

            if (entityType.GetProperty(property, BindingFlags.Public | BindingFlags.Instance) is null)
            {
                throw new InvalidOperationException($"Entity '{entityType.FullName}' declares an index on '{property}', which is not a public instance property.");
            }
        }
    }

    /// <summary>
    /// 追加属性名的缩写：每个词的前两个字符
    /// </summary>
    private static void AppendAbbreviation(StringBuilder builder, string property)
    {
        // 当前词已经读过的字符数，到 2 之后的字符不再追加
        var taken = 0;
        for (var index = 0; index < property.Length; index++)
        {
            var c = property[index];
            if (c == '_')
            {
                taken = 0;
                continue;
            }

            if (index > 0 && StartsWord(property, index))
            {
                taken = 0;
            }

            if (taken < 2)
            {
                builder.Append(c);
            }

            taken++;
        }
    }

    /// <summary>
    /// 该位置的字符是否开始一个新词
    /// </summary>
    /// <remarks>
    /// 大写字母跟在非大写字符之后（<c>tenantId</c> 的 <c>I</c>），或在连续大写里后跟小写（<c>HTTPServer</c> 的 <c>S</c>）
    /// </remarks>
    private static bool StartsWord(string property, int index)
    {
        if (!char.IsUpper(property[index]))
        {
            return false;
        }

        return !char.IsUpper(property[index - 1]) || (index + 1 < property.Length && char.IsLower(property[index + 1]));
    }
}
