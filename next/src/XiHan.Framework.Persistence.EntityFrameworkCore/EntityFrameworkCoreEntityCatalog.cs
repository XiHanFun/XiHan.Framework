// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.Schema;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// EF Core 的实体目录。
/// </summary>
/// <remarks>
/// 表名与列取应用 DbContext 的模型：表名 <c>GetTableName()</c>（没有映射到表时取类型名，与索引命名同一规则），列名 <c>GetColumnName(表)</c>，影子属性与从属类型不列。在独立作用域里取 DbContext，只读模型、不连库；首次调用时算出并缓存。
/// </remarks>
/// <param name="services">根容器：在独立作用域里取 DbContext。</param>
/// <param name="registrations">登记的实体。</param>
internal sealed class EntityFrameworkCoreEntityCatalog(IServiceProvider services, RepositoryRegistrations registrations) : IEntityCatalog
{
    /// <summary>
    /// 只算一次：并发的首次调用排队等同一个结果。
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 算好的目录。
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

        // 只读模型、不连库，同步算；作用域里只解析 DbContext（可同步释放）
        lock (_gate)
        {
            if (_mappings is { } computed)
            {
                return ValueTask.FromResult(computed);
            }

            using var scope = services.CreateScope();
            var model = scope.ServiceProvider.GetRequiredService<DbContext>().Model;
            var mappings = new List<EntityMapping>(registrations.Entities.Count);
            foreach (var entity in registrations.Entities)
            {
                // 登记了却不在模型里：仓储一用就失败的装配错误，这里同样明确报出
                var entityType = model.FindEntityType(entity.EntityType)
                    ?? throw new InvalidOperationException($"Entity '{entity.EntityType.FullName}' is registered with AddRepository but is not part of the DbContext model.");
                var table = entityType.GetTableName() ?? entity.EntityType.Name;
                var store = StoreObjectIdentifier.Table(table, entityType.GetSchema());
                var columns = entityType.GetProperties()
                    .Where(static p => !p.IsShadowProperty())
                    .Select(p => new EntityColumnMapping(p.Name, p.GetColumnName(store) ?? p.Name, p.ClrType, p.IsPrimaryKey(), p.IsNullable))
                    .ToList();
                mappings.Add(new EntityMapping(entity, table, isSplit: false, columns));
            }

            Volatile.Write(ref _mappings, mappings);
            return ValueTask.FromResult<IReadOnlyList<EntityMapping>>(mappings);
        }
    }
}
