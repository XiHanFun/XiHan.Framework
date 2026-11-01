// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// <c>[EntityIndex]</c> 到 EF Core 索引的映射（ADR-0072）。
/// </summary>
/// <remarks>
/// 经 <c>ApplyPersistenceConventions()</c> 映射为 <c>HasIndex</c>（RFC-0005「索引声明」）。
/// 模型里的索引名、列与唯一性与 SqlSugar 建出的一致；<c>EnsureCreated</c> 在 SQLite 上建出，唯一索引真实拒绝重复，软删除实体的唯一索引末尾带删除标记。
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
        // 软删除实体：约定已有的删除标记索引照旧，声明的唯一索引末尾带删除标记
        Assert.Equal(
            [("IX_SoftDeletedIndexedRow_IsDeleted", false, nameof(ISoftDeletable.IsDeleted)), ("UX_SoftDeletedIndexedRow_Co", true, "Code,IsDeleted")],
            Indexes(context, typeof(SoftDeletedIndexedRow)));
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
            // 删掉的行不占编码
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
