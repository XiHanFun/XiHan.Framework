// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

public sealed class EntityFrameworkCoreRepositoryConformanceTests : RepositoryConformance, IDisposable
{
    private readonly SqliteDatabase _database = new();

    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => _database.Configuration;

    protected override IFeature CreateFeature() => SqliteDatabase.CreateFeature();

    public void Dispose() => _database.Dispose();
}
