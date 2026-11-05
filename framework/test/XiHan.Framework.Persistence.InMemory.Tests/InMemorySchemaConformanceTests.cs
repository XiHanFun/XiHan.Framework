// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.InMemory.Tests;

/// <summary>
/// 内存实现跑库结构读取与实体目录的一致性套件
/// </summary>
/// <remarks>
/// 实体目录的表名取类型名；库结构读取不支持，返回明确的错误码
/// </remarks>
public sealed class InMemorySchemaConformanceTests : SchemaConformance
{
    /// <inheritdoc />
    protected override bool ReadsSchema => false;

    /// <inheritdoc />
    protected override IFeature CreateFeature() => new InMemoryPersistenceFeature();

    /// <inheritdoc />
    protected override DataSourceDefinition? CreateDefinition(string name) => null;
}
