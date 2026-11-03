// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Conformance;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 发件箱一致性套件以 EF Core 跑在 SQLite 上。
/// </summary>
/// <remarks>
/// 每个用例一个临时数据库文件，开着 <c>EnsureCreated</c> 按 <see cref="OutboxDbContext"/> 的模型建表。
/// </remarks>
public sealed class EntityFrameworkCoreOutboxConformanceTests : OutboxConformance, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => _database.Configuration;

    /// <inheritdoc />
    protected override IFeature CreateFeature()
        => new EntityFrameworkCorePersistenceFeature<OutboxDbContext>(static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value));

    /// <summary>
    /// 测试结束后删除临时数据库文件。
    /// </summary>
    public void Dispose() => _database.Dispose();
}
