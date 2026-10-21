// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.InMemory.Tests;

/// <summary>
/// 内存 Provider 跑一致性套件；每个用例一个新的特性实例，因而存储也是全新的。
/// </summary>
public sealed class InMemoryRepositoryConformanceTests : RepositoryConformance
{
    /// <inheritdoc />
    protected override IFeature CreateFeature() => new InMemoryPersistenceFeature();
}
