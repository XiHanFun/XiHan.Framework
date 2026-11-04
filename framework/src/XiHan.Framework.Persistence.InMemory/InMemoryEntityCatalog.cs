// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections;
using System.Reflection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.Schema;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 内存实现的实体目录
/// </summary>
/// <remarks>
/// 内存实现没有表：表名取实体类型名，列取有公开 setter 的非集合属性（字符串与字节数组除外），列名即属性名——这是它的存储形态，与 SqlSugar 不加约定时的缺省映射相同。主键是 <c>Id</c>；可空看可空值类型与可空引用类型标注
/// </remarks>
/// <param name="registrations">登记的实体</param>
internal sealed class InMemoryEntityCatalog(RepositoryRegistrations registrations) : IEntityCatalog
{
    /// <summary>
    /// 判定引用类型属性可空性的上下文
    /// </summary>
    /// <remarks>
    /// 不是线程安全的，只在锁里用
    /// </remarks>
    private readonly NullabilityInfoContext _nullability = new();

    /// <summary>
    /// 算好的目录
    /// </summary>
    private IReadOnlyList<EntityMapping>? _mappings;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<EntityMapping>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _mappings) is { } ready)
        {
            return ValueTask.FromResult(ready);
        }

        // 可空性上下文不是线程安全的：首次计算在锁里完成，之后只读缓存
        lock (_nullability)
        {
            if (_mappings is null)
            {
                var mappings = new List<EntityMapping>(registrations.Entities.Count);
                foreach (var entity in registrations.Entities)
                {
                    var columns = entity.EntityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(static p => p.SetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0 && !IsCollection(p.PropertyType))
                        .Select(p => new EntityColumnMapping(p.Name, p.Name, p.PropertyType, p.Name == "Id", IsNullable(p)))
                        .ToList();
                    mappings.Add(new EntityMapping(entity, entity.EntityType.Name, isSplit: false, columns));
                }

                Volatile.Write(ref _mappings, mappings);
            }

            return ValueTask.FromResult(_mappings);
        }
    }

    /// <summary>
    /// 是否是集合（字符串与字节数组不算）
    /// </summary>
    private static bool IsCollection(Type type) => type != typeof(string) && type != typeof(byte[]) && typeof(IEnumerable).IsAssignableFrom(type);

    /// <summary>
    /// 属性的值是否可空
    /// </summary>
    private bool IsNullable(PropertyInfo property)
        => property.PropertyType.IsValueType
            ? Nullable.GetUnderlyingType(property.PropertyType) is not null
            : _nullability.Create(property).WriteState != NullabilityState.NotNull;
}
