// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Conformance.Knowledge;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// VectorData 知识库存储通过存储套件。
/// </summary>
public sealed class VectorDataKnowledgeStoreConformanceTests : KnowledgeStoreConformance
{
    /// <inheritdoc />
    protected override async Task<KnowledgeStoreHost> StartAsync()
    {
        var host = await VectorDataTestHost.StartAsync(new TestVectorStore());
        return new KnowledgeStoreHost(host.Services.GetRequiredService<IKnowledgeStore>(), VectorDataTestHost.Dimensions, host);
    }
}
