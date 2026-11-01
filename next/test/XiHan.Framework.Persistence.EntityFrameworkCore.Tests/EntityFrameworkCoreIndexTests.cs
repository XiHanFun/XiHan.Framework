// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// <c>[EntityIndex]</c> 到 EF Core 索引的映射（ADR-0072、ADR-0081）。
/// </summary>
/// <remarks>
/// 经 <c>ApplyPersistenceConventions(Database)</c> 映射为 <c>HasIndex</c>（RFC-0005「索引声明」）。
/// 模型里的索引名、列与唯一性与 SqlSugar 建出的一致；<c>EnsureCreated</c> 在 SQLite 上建出，唯一索引真实拒绝重复。软删除实体的唯一索引是只约束未删除行的部分唯一索引（<c>HasFilter</c>，条件按 Provider 写方言）：同一编码可以有任意多行已删除；Provider 不支持部分唯一索引时建模失败、特性启动失败。
/// </remarks>
public sealed class EntityFrameworkCoreIndexTests : IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 测试结束后删除临时数据库文件。
    /// </summary>
    public void Dispose() => _database.Dispose();

    [Fact]
    public void Model_HasDeclaredIndexes_NamedFromTheMappedTable()
    {
        using var context = Context();

        Assert.Equal(
            [("IX_indexed_row_Ca_At", false, "Category,At"), ("UX_indexed_row_TeId_Co", true, "TenantId,Code")],
            Indexes(context, typeof(IndexedRow)));
        // 软删除实体：约定已有的删除标记索引照旧，声明的唯一索引只有编码一列、条件是未删除
        Assert.Equal(
            [("IX_SoftDeletedIndexedRow_IsDeleted", false, nameof(ISoftDeletable.IsDeleted)), ("UX_SoftDeletedIndexedRow_Co", true, "Code")],
            Indexes(context, typeof(SoftDeletedIndexedRow)));
        var partial = context.Model.FindEntityType(typeof(SoftDeletedIndexedRow))!.GetIndexes().Single(static i => i.IsUnique);
        Assert.Equal("\"IsDeleted\" = 0", partial.GetFilter());
    }

    [Fact]
    public async Task EnsureCreated_BuildsUniqueIndexes_ThatRejectDuplicates()
    {
        await using (var context = Context())
        {
            await context.Database.EnsureCreatedAsync();
            context.Add(Row("acme", "A"));
            // 另一个租户的同一编码不冲突
            context.Add(Row("other", "A"));
            await context.SaveChangesAsync();
        }

        await using (var context = Context())
        {
            context.Add(Row("acme", "A"));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("UNIQUE", error.InnerException?.Message, StringComparison.Ordinal);
        }

        await using (var context = Context())
        {
            // 删掉的行不占编码，同一编码可以有任意多行已删除
            context.Add(new SoftDeletedIndexedRow { Id = Guid.CreateVersion7(), Code = "A", IsDeleted = true });
            context.Add(new SoftDeletedIndexedRow { Id = Guid.CreateVersion7(), Code = "A", IsDeleted = true });
            context.Add(new SoftDeletedIndexedRow { Id = Guid.CreateVersion7(), Code = "A" });
            await context.SaveChangesAsync();
        }

        await using (var context = Context())
        {
            // 未删除的行之间唯一
            context.Add(new SoftDeletedIndexedRow { Id = Guid.CreateVersion7(), Code = "A" });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        Assert.Equal(["IX_indexed_row_Ca_At", "UX_indexed_row_TeId_Co"], await IndexNamesAsync("indexed_row"));
    }

    [Fact]
    public void PartialUniqueIndex_FilterIsWrittenInEachSupportedProvidersDialect()
    {
        // Npgsql 与 SQL Server 的 Provider 没有引用：用带关系型约定的模型构建器，按 Provider 名施加约定
        Assert.Equal("\"IsDeleted\" = false", PartialFilter(ModelBuilderExtensions.NpgsqlProvider, typeof(SoftDeletedIndexedRow)));
        Assert.Equal("\"IsDeleted\" = 0", PartialFilter(ModelBuilderExtensions.SqliteProvider, typeof(SoftDeletedIndexedRow)));
        Assert.Equal("[IsDeleted] = 0", PartialFilter(ModelBuilderExtensions.SqlServerProvider, typeof(SoftDeletedIndexedRow)));
        // SQL Server 的唯一索引把 NULL 当作相等：键里可空的列另加 IS NOT NULL，与它对唯一索引的缺省一致
        Assert.Equal("[IsDeleted] = 0 AND [Region] IS NOT NULL", PartialFilter(ModelBuilderExtensions.SqlServerProvider, typeof(SoftDeletedRegionalRow)));
        Assert.Equal("\"IsDeleted\" = false", PartialFilter(ModelBuilderExtensions.NpgsqlProvider, typeof(SoftDeletedRegionalRow)));
    }

    [Fact]
    public void PartialUniqueIndex_OnAnUnsupportedProvider_FailsTheModel()
    {
        var modelBuilder = new ModelBuilder(SqliteConventionSetBuilder.Build());
        modelBuilder.Entity<IndexedRow>();
        modelBuilder.Entity<SoftDeletedIndexedRow>();
        modelBuilder.Entity<SoftDeletedRegionalRow>();

        var error = Assert.Throws<FrameworkException>(() => ModelBuilderExtensions.Apply(modelBuilder, "Pomelo.EntityFrameworkCore.MySql", relational: true));

        // 一次列出全部要部分唯一索引的实体，不支持就不建模，不退化成普通唯一索引
        Assert.Equal(PersistenceErrors.NotSupported, error.Code);
        Assert.Contains("(UX_SoftDeletedIndexedRow_Co)", error.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(SoftDeletedRegionalRow).FullName!, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(IndexedRow).FullName + "'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialUniqueIndex_OnANonRelationalProvider_HasNoFilter()
    {
        // 非关系型 Provider 本来不建索引：不加条件、不报错
        var modelBuilder = new ModelBuilder(SqliteConventionSetBuilder.Build());
        modelBuilder.Entity<SoftDeletedIndexedRow>();

        ModelBuilderExtensions.Apply(modelBuilder, "Microsoft.EntityFrameworkCore.InMemory", relational: false);

        Assert.Null(modelBuilder.Model.FindEntityType(typeof(SoftDeletedIndexedRow))!.GetIndexes().Single(static i => i.IsUnique).GetFilter());
    }

    [Fact]
    public async Task Startup_WithAProviderWithoutPartialIndexes_Fails_EvenWithoutEnsureCreated()
    {
        // 启动时建一次模型：生产库不开 EnsureCreated 时，建模错误同样在启动时失败，不等第一次请求
        var error = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(
            static builder => builder.UseFeature(new EntityFrameworkCorePersistenceFeature<UnsupportedProviderDbContext>(static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value))),
            [new("XiHan:Bindings:db", _database.ConnectionString)],
            static services => services.AddRepository<SoftDeletedIndexedRow, Guid>()));

        // 宿主把特性启动失败包成 FeatureStartFailed，不支持的原因在内部异常里
        var cause = Assert.IsType<FrameworkException>(error.InnerException);
        Assert.Equal(PersistenceErrors.NotSupported, cause.Code);
    }

    /// <summary>
    /// 按 Provider 名施加约定后，一个实体的部分唯一索引的条件。
    /// </summary>
    private static string? PartialFilter(string provider, Type entityType)
    {
        // 每次新建模型构建器：条件按这一次的 Provider 写出
        var modelBuilder = new ModelBuilder(SqliteConventionSetBuilder.Build());
        modelBuilder.Entity(entityType);
        ModelBuilderExtensions.Apply(modelBuilder, provider, relational: true);
        return modelBuilder.Model.FindEntityType(entityType)!.GetIndexes().Single(static i => i.IsUnique).GetFilter();
    }

    /// <summary>
    /// 连到本测试数据库的上下文。
    /// </summary>
    private IndexedDbContext Context()
        => new(new DbContextOptionsBuilder<IndexedDbContext>().UseSqlite(_database.ConnectionString).Options);

    /// <summary>
    /// 数据库里一张表上显式建出的索引名（不含主键的自动索引），按名字排序。
    /// </summary>
    private async Task<List<string>> IndexNamesAsync(string table)
    {
        await using var connection = new SqliteConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = $table AND sql IS NOT NULL ORDER BY name";
        command.Parameters.AddWithValue("$table", table);
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    /// <summary>
    /// 模型里一个实体的索引：名字、是否唯一、逗号分隔的列，按名字排序。
    /// </summary>
    private static List<(string Name, bool IsUnique, string Columns)> Indexes(DbContext context, Type entityType)
        => [.. context.Model.FindEntityType(entityType)!.GetIndexes()
            .Select(static index => (Name: index.GetDatabaseName()!, index.IsUnique, Columns: string.Join(',', index.Properties.Select(static p => p.Name))))
            .OrderBy(static index => index.Name, StringComparer.Ordinal)];

    /// <summary>
    /// 一行记录。
    /// </summary>
    private static IndexedRow Row(string tenantId, string code)
        => new() { Id = Guid.CreateVersion7(), TenantId = tenantId, Code = code, Category = "c", At = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero) };
}
