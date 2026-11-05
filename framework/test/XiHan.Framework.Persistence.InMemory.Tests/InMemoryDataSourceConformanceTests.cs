// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.InMemory.Tests;

/// <summary>
/// 内存实现跑运行时数据源的一致性套件
/// </summary>
/// <remarks>
/// 不支持：取用与测试连接都返回明确的错误码
/// </remarks>
public sealed class InMemoryDataSourceConformanceTests : DataSourceConformance
{
    /// <inheritdoc />
    protected override IFeature CreateFeature() => new InMemoryPersistenceFeature();

    /// <inheritdoc />
    protected override DataSourceDefinition? CreateDefinition(string name) => null;

    /// <inheritdoc />
    protected override DataSourceDefinition? CreateUnreachableDefinition(string name) => null;
}
