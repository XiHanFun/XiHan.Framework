// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar Provider 以 SQLite 跑一致性套件
/// </summary>
/// <remarks>
/// 每个用例一个临时数据库文件，另一个临时文件作命名连接 <see cref="Leases"/>（套件把 <c>ConformanceLease</c> 登记在上面）；直接写入入口是 <see cref="SqlSugarUnitOfWork.EnsureTransactionAsync"/>
/// </remarks>
public sealed class SqlSugarRepositoryConformanceTests : RepositoryConformance, IDisposable
{
    /// <summary>
    /// 命名连接的名字
    /// </summary>
    private const string Leases = "leases";

    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 命名连接 <see cref="Leases"/> 的临时 SQLite 数据库
    /// </summary>
    /// <remarks>
    /// 与默认库分开：用例在业务事务占着默认库写锁时另开作用域写这个连接
    /// </remarks>
    private readonly SqliteDatabase _leases = new();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration =>
        [.. _database.Configuration, new($"{SqlSugarPersistenceOptions.ConfigurationSection}:Connections:{Leases}", _leases.ConnectionString)];

    /// <inheritdoc />
    protected override Func<IUnitOfWork, CancellationToken, ValueTask>? EnsureTransaction
        => static (unitOfWork, cancellationToken) => ((SqlSugarUnitOfWork)unitOfWork).EnsureTransactionAsync(cancellationToken);

    /// <inheritdoc />
    protected override string? NamedConnection => Leases;

    /// <inheritdoc />
    protected override IFeature CreateFeature() => SqliteDatabase.CreateFeature();

    /// <summary>
    /// 测试结束后删除两个临时数据库文件
    /// </summary>
    public void Dispose()
    {
        _database.Dispose();
        _leases.Dispose();
    }
}
