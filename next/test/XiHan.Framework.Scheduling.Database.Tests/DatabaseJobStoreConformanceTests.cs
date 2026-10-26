// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Scheduling.Abstractions.Storage;
using XiHan.Framework.Scheduling.Conformance;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 数据库作业存储对 SQLite 通过存储套件；引擎的轮询周期设得很长，存储里的状态只由用例改动。
/// </summary>
public sealed class DatabaseJobStoreConformanceTests : JobStoreConformance
{
    /// <inheritdoc />
    protected override async Task<(IJobStore Store, IAsyncDisposable Host)> CreateAsync()
    {
        var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        return (host.Services.GetRequiredService<IJobStore>(), database);
    }
}
